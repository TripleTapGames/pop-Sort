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
        [SerializeField] private float acceptDistanceThreshold = 0.05f;
        [SerializeField] private float noMatchFailDelay = 0.75f;
        [SerializeField] private float funnelMoveSpeed = 6f;
        [SerializeField] private float slotCatchDistance = 0.35f;
        [SerializeField] private float funnelQueueSpacing = 0.5f;
        [SerializeField] private float seatToSlotSpeed = 100f;
        [SerializeField] private float funnelExitTolerance = 0.2f;

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
        private float noMatchElapsedTime;
        private bool isProcessingQueue;

        private void Start()
        {
            // GameManager supplies the selected level.
        }

        public void HandleBallLanded(Ball ball)
        {
            if (!isProcessingQueue) return;

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

            TryCollectBallsAtPickup();
            TryAddPendingBalls();
            UpdateFunnelWaitingBalls();
            CheckForFullBeltDeadlock();
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
            queue.Add(new QueuedBall(ball, slotIndex, isSeated: false));
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
            for (int i = 0; i < funnelWaitingBalls.Count; i++)
            {
                Ball waitingBall = funnelWaitingBalls[i];
                if (i == 0)
                {
                    waitingBall.SetFunnelKinematic();
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

            if (funnelWaitingBalls.Count == 0 || queue.Count >= GetBeltCapacity()) return;

            Ball frontBall = funnelWaitingBalls[0];
            bool frontAtExit = Vector3.Distance(frontBall.transform.position, funnelExitPoint.position) <= funnelExitTolerance;
            if (!frontAtExit) return;

            int slotIndex = FindNearestFreeSplineSlot(funnelExitPoint.position);
            bool slotNearExit = slotIndex >= 0 &&
                Vector3.Distance(splineConveyorBelt.GetSlotWorldPosition(slotIndex), funnelExitPoint.position) <= slotCatchDistance;
            if (!slotNearExit) return;

            funnelWaitingBalls.RemoveAt(0);
            AttachBallToSlot(frontBall, slotIndex);
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
                    queuedBall.Ball.transform.localPosition = Vector3.MoveTowards(
                        queuedBall.Ball.transform.localPosition,
                        Vector3.zero,
                        seatToSlotSpeed * Time.deltaTime);

                    if (queuedBall.Ball.transform.localPosition.sqrMagnitude > 0.0001f)
                    {
                        continue;
                    }

                    queue[i] = queuedBall.Seat();
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
                Transform pickupPoint = trayManager.GetColumnPickupPoint(columnIndex);
                if (pickupPoint == null) continue;

                float distance = Vector3.Distance(queuedBall.Ball.transform.position, pickupPoint.position);
                if (distance > acceptDistanceThreshold) continue;

                return trayManager.TryAcceptBallAtColumn(columnIndex, queuedBall.Ball);
            }

            return false;
        }

        private struct QueuedBall
        {
            public readonly Ball Ball;
            public readonly int SplineSlotIndex;
            public readonly bool IsSeated;

            public QueuedBall(Ball ball, int splineSlotIndex, bool isSeated)
            {
                Ball = ball;
                SplineSlotIndex = splineSlotIndex;
                IsSeated = isSeated;
            }

            public QueuedBall Seat() => new QueuedBall(Ball, SplineSlotIndex, true);
        }
    }
}
