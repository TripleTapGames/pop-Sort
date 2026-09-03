using System;
using System.Collections.Generic;
using PaperSort.Game;
using UnityEngine;

namespace PopSort
{
    public class BeltQueueManager : MonoBehaviour
    {
        [SerializeField] private LevelData levelData;
        [SerializeField] private TrayManager trayManager;
        [SerializeField] private SplineConveyorBelt2D splineConveyorBelt;
        [SerializeField] private Transform trayPickupPoint;
        [SerializeField] private float acceptDistanceThreshold = 0.05f;

        public event Action OnOverflow;

        public int QueueCount => queue.Count;

        private readonly List<QueuedBall> queue = new List<QueuedBall>();
        private readonly HashSet<int> occupiedSplineSlots = new HashSet<int>();

        private void Start()
        {
            if (levelData != null && splineConveyorBelt != null)
            {
                splineConveyorBelt.SetSpeed(levelData.beltSpeed);
            }
        }

        public void HandleBallLanded(Ball ball)
        {
            if (splineConveyorBelt == null || trayPickupPoint == null)
            {
                Debug.LogError("BeltQueueManager requires Spline Conveyor Belt and Tray Pickup Point references.", this);
                OnOverflow?.Invoke();
                return;
            }

            AddBallToSplineBelt(ball);
        }

        private void Update()
        {
            TryCollectBallsAtPickup();
        }

        private void AddBallToSplineBelt(Ball ball)
        {
            if (queue.Count >= GetBeltCapacity())
            {
                OnOverflow?.Invoke();
                return;
            }

            int slotIndex = FindNearestFreeSplineSlot(ball.transform.position);
            if (slotIndex < 0)
            {
                OnOverflow?.Invoke();
                return;
            }

            ball.SetQueued();
            splineConveyorBelt.AttachObjectToSlot(ball.transform, slotIndex);
            occupiedSplineSlots.Add(slotIndex);
            queue.Add(new QueuedBall(ball, slotIndex));
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

        private void TryCollectBallsAtPickup()
        {
            if (queue.Count == 0) return;

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                QueuedBall queuedBall = queue[i];
                float distance = Vector3.Distance(queuedBall.Ball.transform.position, trayPickupPoint.position);
                if (distance > acceptDistanceThreshold) continue;

                if (!trayManager.TryAcceptBall(queuedBall.Ball)) continue;

                splineConveyorBelt.DetachObject(queuedBall.Ball.transform);
                occupiedSplineSlots.Remove(queuedBall.SplineSlotIndex);
                queue.RemoveAt(i);
            }
        }

        private struct QueuedBall
        {
            public readonly Ball Ball;
            public readonly int SplineSlotIndex;

            public QueuedBall(Ball ball, int splineSlotIndex)
            {
                Ball = ball;
                SplineSlotIndex = splineSlotIndex;
            }
        }
    }
}
