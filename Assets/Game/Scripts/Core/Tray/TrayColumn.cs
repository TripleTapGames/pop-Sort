using System.Collections;
using UnityEngine;

namespace PopSort
{
    public class TrayColumn : MonoBehaviour
    {
        [SerializeField] private TraySlot[] traySlots; // ordered front (active first) to back

        private int activeIndex;
        private Vector3[] slotPositions; // cached original front-to-back layout positions

        private void Awake()
        {
            CacheLayoutPositions();
        }

        public void Configure(TraySlot[] traySlots)
        {
            this.traySlots = traySlots;
            activeIndex = 0;
            CacheLayoutPositions();
        }

        public bool IsComplete
        {
            get
            {
                if (traySlots == null || traySlots.Length == 0) return true;

                foreach (TraySlot slot in traySlots)
                {
                    if (slot == null || !slot.IsComplete) return false;
                }

                return true;
            }
        }

        public bool CanAccept(int colorId) =>
            activeIndex < traySlots.Length &&
            traySlots[activeIndex].ColorId == colorId &&
            traySlots[activeIndex].HasSpace;

        public bool TryAcceptBall(Ball ball)
        {
            if (!CanAccept(ball.ColorId)) return false;

            TraySlot activeSlot = traySlots[activeIndex];
            bool becameFull = activeSlot.Fill(ball);
            if (becameFull)
            {
                StartCoroutine(ConsumeActiveTray(activeSlot));
            }

            return true;
        }

        private IEnumerator ConsumeActiveTray(TraySlot activeSlot)
        {
            yield return new WaitForSeconds(activeSlot.LastIntakeMoveTime);

            activeSlot.ClearBalls();
            activeSlot.gameObject.SetActive(false);
            ShiftRemainingTraysForward();
            activeIndex++;
        }

        // Moves every tray behind the one just consumed up by one slot in the stack.
        private void ShiftRemainingTraysForward()
        {
            for (int k = 1; activeIndex + k < traySlots.Length; k++)
            {
                traySlots[activeIndex + k].transform.position = slotPositions[k - 1];
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
