using System;
using System.Collections.Generic;
using PaperSort.Game;
using UnityEngine;

namespace PopSort
{
    public class BeltQueueManager : MonoBehaviour
    {
        private LevelData levelData;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private TrayManager trayManager;
        [SerializeField] private SplineConveyorBelt2D splineConveyorBelt;
        [SerializeField] private Transform funnelExitPoint;
        [SerializeField] private float noMatchFailDelay = 0.75f;
        [SerializeField] private float funnelMoveSpeed = 6f;
        [SerializeField] private float slotCatchDistance = 0.35f;
        [SerializeField] private float funnelQueueSpacing = 0.5f;
        [Header("Belt Entry Motion")]
        [SerializeField, Min(0f)] private float seatToSlotDuration = 0.12f;
        [SerializeField] private AnimationCurve seatToSlotCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float funnelExitTolerance = 0.2f;
        [Tooltip("Radius around the funnel outlet that hands falling balls to the funnel queue before they can jam on the physical lip.")]
        [SerializeField] private float funnelCaptureRadius = 1.2f;

        public event Action OnOverflow;

        public int QueueCount => queue.Count + pendingBalls.Count + funnelWaitingBalls.Count;

        public void SetLevelData(LevelData newLevelData)
        {
            levelData = newLevelData;
            isProcessingQueue = true;
            if (splineConveyorBelt != null)
            {
                splineConveyorBelt.SetSlotCount(levelData.beltSlotCount);
                splineConveyorBelt.SetSpeed(levelData.beltSpeed);
                splineConveyorBelt.SetMoving(true);
            }
        }

        public void SetBeltMoving(bool shouldMove)
        {
            isProcessingQueue = shouldMove;
            if (splineConveyorBelt != null) splineConveyorBelt.SetMoving(shouldMove);
        }

        public void ClearQueue()
        {
            StopAllCoroutines();

            foreach (QueuedBall queuedBall in queue)
            {
                if (queuedBall.Ball == null) continue;
                if (splineConveyorBelt != null)
                {
                    splineConveyorBelt.DetachObject(queuedBall.Ball.transform);
                }
                if (ballPool != null) ballPool.Release(queuedBall.Ball);
            }

            foreach (Ball pendingBall in pendingBalls)
            {
                if (pendingBall != null && ballPool != null) ballPool.Release(pendingBall);
            }

            foreach (Ball funnelBall in funnelWaitingBalls)
            {
                if (funnelBall != null && ballPool != null) ballPool.Release(funnelBall);
            }

            queue.Clear();
            pendingBalls.Clear();
            funnelWaitingBalls.Clear();
            occupiedSplineSlots.Clear();
            noMatchElapsedTime = 0f;
        }

        private readonly List<QueuedBall> queue = new List<QueuedBall>();
        private readonly List<Ball> pendingBalls = new List<Ball>();
        private readonly List<Ball> funnelWaitingBalls = new List<Ball>();
        private readonly HashSet<int> occupiedSplineSlots = new HashSet<int>();
        private readonly Collider2D[] funnelCaptureResults = new Collider2D[32];
        private float noMatchElapsedTime;
        private bool isProcessingQueue;

        private void Start()
        {
            // GameManager supplies the selected level.
        }

        public void HandleBallLanded(Ball ball)
        {
            if (!isProcessingQueue || ball == null || funnelWaitingBalls.Contains(ball)) return;

            if (splineConveyorBelt == null || trayManager == null || trayManager.ColumnCount == 0)
            {
                Debug.LogError("BeltQueueManager requires Spline Conveyor Belt and tray column pickup points.", this);
                OnOverflow?.Invoke();
                return;
            }

            if (funnelExitPoint == null)
            {
                Debug.LogError("BeltQueueManager requires a Funnel Exit Point.", this);
                OnOverflow?.Invoke();
                return;
            }

            ball.SetFunnelDynamic();
            funnelWaitingBalls.Add(ball);
        }

        private void Update()
        {
            if (!isProcessingQueue) return;

            CaptureFallingBallsNearFunnelExit();
            TryCollectBallsAtPickup();
            TryAddPendingBalls();
            UpdateFunnelWaitingBalls();
            CheckForFullBeltDeadlock();
        }

        // The visual funnel can hold balls above its physical outlet.  Waiting for
        // them to touch the belt means a stable pile can never enter the queue.
        // Capture only falling balls near the outlet; after capture they remain
        // dynamic until one is selected for extraction.
        private void CaptureFallingBallsNearFunnelExit()
        {
            if (funnelExitPoint == null) return;

            int resultCount = Physics2D.OverlapCircleNonAlloc(
                funnelExitPoint.position,
                funnelCaptureRadius,
                funnelCaptureResults);

            for (int i = 0; i < resultCount; i++)
            {
                Collider2D capturedCollider = funnelCaptureResults[i];
                funnelCaptureResults[i] = null;

                Ball ball = capturedCollider != null ? capturedCollider.GetComponent<Ball>() : null;
                if (ball == null || ball.State != BallState.Falling) continue;

                HandleBallLanded(ball);
            }
        }

        private void AddBallToSplineBelt(Ball ball)
        {
            if (queue.Count >= GetBeltCapacity())
            {
                pendingBalls.Add(ball);
                return;
            }

            int slotIndex = FindNearestFreeSplineSlot(ball.transform.position);
            if (slotIndex < 0)
            {
                pendingBalls.Add(ball);
                return;
            }

            AttachBallToSlot(ball, slotIndex);
        }

        private void AttachBallToSlot(Ball ball, int slotIndex)
        {
            ball.SetQueued();
            splineConveyorBelt.AttachObjectToSlot(ball.transform, slotIndex, resetLocalPosition: false);
            occupiedSplineSlots.Add(slotIndex);
            queue.Add(new QueuedBall(ball, slotIndex, false, ball.transform.localPosition, 0f));
        }

        private void TryAddPendingBalls()
        {
            while (pendingBalls.Count > 0 && queue.Count + funnelWaitingBalls.Count < GetBeltCapacity())
            {
                Ball pendingBall = pendingBalls[0];
                pendingBalls.RemoveAt(0);
                funnelWaitingBalls.Add(pendingBall);
            }
        }

        private void UpdateFunnelWaitingBalls()
        {
            funnelWaitingBalls.RemoveAll(ball => ball == null);
            if (funnelWaitingBalls.Count == 0) return;

            // Collision order does not reliably match the physical order inside the
            // funnel.  Extract the ball that has actually settled closest to the exit.
            Ball frontBall = GetBallClosestToFunnelExit();
            if (frontBall == null) return;

            foreach (Ball waitingBall in funnelWaitingBalls)
            {
                if (waitingBall == frontBall)
                {
                    // Keep the pile dynamic, but make the extracted ball non-blocking
                    // while it is guided through the narrow funnel outlet.
                    waitingBall.SetFunnelExtracting();
                    waitingBall.transform.position = Vector3.MoveTowards(
                        waitingBall.transform.position,
                        funnelExitPoint.position,
                        funnelMoveSpeed * Time.deltaTime);
                }
                else
                {
                    waitingBall.SetFunnelDynamic();
                }
            }

            if (queue.Count >= GetBeltCapacity()) return;

            bool frontAtExit = Vector3.Distance(frontBall.transform.position, funnelExitPoint.position) <= funnelExitTolerance;
            if (!frontAtExit) return;

            int slotIndex = FindNearestFreeSplineSlot(funnelExitPoint.position);
            bool slotNearExit = slotIndex >= 0 &&
                Vector3.Distance(splineConveyorBelt.GetSlotWorldPosition(slotIndex), funnelExitPoint.position) <= slotCatchDistance;
            if (!slotNearExit) return;

            funnelWaitingBalls.Remove(frontBall);
            AttachBallToSlot(frontBall, slotIndex);
        }

        private Ball GetBallClosestToFunnelExit()
        {
            Ball closestBall = null;
            float closestDistance = float.PositiveInfinity;

            foreach (Ball waitingBall in funnelWaitingBalls)
            {
                if (waitingBall == null) continue;

                float distance = Vector3.Distance(waitingBall.transform.position, funnelExitPoint.position);
                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closestBall = waitingBall;
            }

            return closestBall;
        }

        private int FindNearestFreeSplineSlot(Vector3 worldPosition)
        {
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < splineConveyorBelt.SlotCount; i++)
            {
                if (occupiedSplineSlots.Contains(i)) continue;

                float distance = Vector3.Distance(worldPosition, splineConveyorBelt.GetSlotWorldPosition(i));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private int GetBeltCapacity()
        {
            int splineSlotCount = splineConveyorBelt != null ? splineConveyorBelt.SlotCount : 0;
            if (levelData == null) return splineSlotCount;

            return Mathf.Min(Mathf.Max(levelData.beltSlotCount, 1), splineSlotCount);
        }

        private void OnDrawGizmosSelected()
        {
            if (funnelExitPoint == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(funnelExitPoint.position, funnelCaptureRadius);
        }

        private void CheckForFullBeltDeadlock()
        {
            if (queue.Count < GetBeltCapacity() || HasQueuedBallMatchingActiveTray())
            {
                noMatchElapsedTime = 0f;
                return;
            }

            noMatchElapsedTime += Time.deltaTime;
            if (noMatchElapsedTime < noMatchFailDelay) return;

            noMatchElapsedTime = 0f;
            OnOverflow?.Invoke();
        }

        private bool HasQueuedBallMatchingActiveTray()
        {
            if (trayManager == null) return false;

            foreach (QueuedBall queuedBall in queue)
            {
                if (queuedBall.Ball != null && trayManager.CanAcceptColor(queuedBall.Ball.ColorId)) return true;
            }

            return false;
        }

        private void TryCollectBallsAtPickup()
        {
            if (queue.Count == 0) return;

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                QueuedBall queuedBall = queue[i];
                if (!queuedBall.IsSeated)
                {
                    queuedBall = queuedBall.AdvanceSeat(Time.deltaTime, seatToSlotDuration, seatToSlotCurve);
                    queue[i] = queuedBall;
                    if (!queuedBall.IsSeated)
                    {
                        continue;
                    }
                }

                if (!TryCollectBallAtColumnPickup(queuedBall))
                {
                    continue;
                }

                splineConveyorBelt.DetachObject(queuedBall.Ball.transform);
                occupiedSplineSlots.Remove(queuedBall.SplineSlotIndex);
                queue.RemoveAt(i);
            }
        }

        private bool TryCollectBallAtColumnPickup(QueuedBall queuedBall)
        {
            for (int columnIndex = 0; columnIndex < trayManager.ColumnCount; columnIndex++)
            {
                if (!trayManager.IsWithinColumnPickupRange(columnIndex, queuedBall.Ball.transform.position)) continue;
                if (trayManager.TryAcceptBallAtColumn(columnIndex, queuedBall.Ball)) return true;
            }

            return false;
        }

        private struct QueuedBall
        {
            public readonly Ball Ball;
            public readonly int SplineSlotIndex;
            public readonly bool IsSeated;
            private readonly Vector3 seatStartLocalPosition;
            private readonly float seatElapsed;

            public QueuedBall(Ball ball, int splineSlotIndex, bool isSeated, Vector3 seatStartLocalPosition, float seatElapsed)
            {
                Ball = ball;
                SplineSlotIndex = splineSlotIndex;
                IsSeated = isSeated;
                this.seatStartLocalPosition = seatStartLocalPosition;
                this.seatElapsed = seatElapsed;
            }

            public QueuedBall AdvanceSeat(float deltaTime, float duration, AnimationCurve curve)
            {
                if (Ball == null) return new QueuedBall(null, SplineSlotIndex, true, Vector3.zero, 0f);

                if (duration <= 0f)
                {
                    Ball.transform.localPosition = Vector3.zero;
                    return new QueuedBall(Ball, SplineSlotIndex, true, Vector3.zero, 0f);
                }

                float elapsed = seatElapsed + deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float progress = curve != null ? Mathf.Clamp01(curve.Evaluate(normalizedTime)) : normalizedTime;
                Ball.transform.localPosition = Vector3.Lerp(seatStartLocalPosition, Vector3.zero, progress);
                return new QueuedBall(Ball, SplineSlotIndex, normalizedTime >= 1f, seatStartLocalPosition, elapsed);
            }
        }
    }
}
