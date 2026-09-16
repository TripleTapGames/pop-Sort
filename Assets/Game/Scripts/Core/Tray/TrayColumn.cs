using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    public class TrayColumn : MonoBehaviour
    {
        public readonly struct RemainingTrayRequirement
        {
            public RemainingTrayRequirement(int colorId, int capacity)
            {
                ColorId = colorId;
                Capacity = capacity;
            }

            public int ColorId { get; }
            public int Capacity { get; }
        }
        [SerializeField] private TraySlot[] traySlots; // ordered front (active first) to back

        [Header("Tray Transition Timing")]
        [SerializeField, Min(0f)] private float filledAnimationHold = .45f;
        [SerializeField, Min(0f)] private float forwardSlideDuration = 0.35f;

        private int activeIndex;
        private Vector3[] slotPositions; // cached original front-to-back layout positions
        private bool isTransitioning;

        private void Awake()
        {
            CacheLayoutPositions();
        }

        public void Configure(TraySlot[] traySlots)
        {
            this.traySlots = traySlots;
            activeIndex = 0;
            isTransitioning = false;
            CacheLayoutPositions();

            // The initial tray is already visible; start it in its idle pose.
            if (this.traySlots != null && this.traySlots.Length > 0 && this.traySlots[0] != null)
            {
                this.traySlots[0].PlayTrayIdle();
            }
        }

        public bool IsComplete
        {
            get
            {
                return traySlots == null || traySlots.Length == 0 ||
                    (!isTransitioning && activeIndex >= traySlots.Length);
            }
        }

        public bool CanAccept(int colorId) =>
            !isTransitioning &&
            traySlots != null &&
            activeIndex < traySlots.Length &&
            traySlots[activeIndex] != null &&
            traySlots[activeIndex].ColorId == colorId &&
            traySlots[activeIndex].HasSpace;

        public bool TryAcceptBall(Ball ball)
        {
            if (!CanAccept(ball.ColorId)) return false;

            TraySlot activeSlot = traySlots[activeIndex];
            bool becameFull = activeSlot.Fill(ball);
            if (becameFull)
            {
                isTransitioning = true;
                StartCoroutine(ConsumeActiveTray(activeSlot));
            }

            return true;
        }

        // Ordered front-to-back, with already-filled capacity removed. This is a
        // snapshot only; it does not alter tray state or transitions.
        public void AddRemainingRequirements(List<RemainingTrayRequirement> requirements)
        {
            if (requirements == null || traySlots == null) return;

            for (int index = activeIndex; index < traySlots.Length; index++)
            {
                TraySlot slot = traySlots[index];
                if (slot == null || slot.RemainingCapacity <= 0) continue;
                requirements.Add(new RemainingTrayRequirement(slot.ColorId, slot.RemainingCapacity));
            }
        }

        private IEnumerator ConsumeActiveTray(TraySlot activeSlot)
        {
            yield return activeSlot.WaitForLastLanding();

            activeSlot.InvokeTrayFilled();

            if (filledAnimationHold > 0f)
            {
                yield return new WaitForSeconds(filledAnimationHold);
            }

            activeSlot.ClearBalls();
            activeSlot.gameObject.SetActive(false);

            int nextIndex = activeIndex + 1;
            if (nextIndex < traySlots.Length && traySlots[nextIndex] != null)
            {
                // The incoming visual starts while the queued stack moves forward.
                traySlots[nextIndex].InvokeTrayEntering();
            }

            yield return SlideRemainingTraysForward();
            activeIndex++;
            isTransitioning = false;
        }

        // Moves every tray behind the consumed one up by one slot in the stack.
        private IEnumerator SlideRemainingTraysForward()
        {
            int firstQueuedIndex = activeIndex + 1;
            int queuedCount = traySlots.Length - firstQueuedIndex;
            if (queuedCount <= 0) yield break;

            Vector3[] startPositions = new Vector3[queuedCount];
            for (int k = 0; k < queuedCount; k++)
            {
                startPositions[k] = traySlots[firstQueuedIndex + k].transform.position;
            }

            if (forwardSlideDuration <= 0f)
            {
                SetQueuedTrayPositions(startPositions, 1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < forwardSlideDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / forwardSlideDuration);
                SetQueuedTrayPositions(startPositions, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            SetQueuedTrayPositions(startPositions, 1f);
        }

        private void SetQueuedTrayPositions(Vector3[] startPositions, float progress)
        {
            for (int k = 0; k < startPositions.Length; k++)
            {
                TraySlot queuedTray = traySlots[activeIndex + 1 + k];
                if (queuedTray == null) continue;

                queuedTray.transform.position = Vector3.Lerp(startPositions[k], slotPositions[k], progress);
            }
        }

        private void CacheLayoutPositions()
        {
            slotPositions = new Vector3[traySlots?.Length ?? 0];
            for (int i = 0; i < slotPositions.Length; i++)
            {
                slotPositions[i] = traySlots[i].transform.position;
                traySlots[i].gameObject.SetActive(true);
            }
        }
    }
}
