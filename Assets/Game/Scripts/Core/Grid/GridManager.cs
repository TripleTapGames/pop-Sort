using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    public class GridManager : MonoBehaviour
    {
        private LevelData levelData;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private BallHolder ballHolderPrefab;
        [SerializeField] private Transform gridOrigin;
        [SerializeField] private float cellSize = 1f;

        [Header("Marble Pop Motion")]
        [SerializeField] private float burstVelocity = 1.5f;
        [SerializeField] private Vector2 burstDirection = Vector2.up;
        [SerializeField, Range(0f, 180f)] private float burstSpreadAngle = 55f;
        [SerializeField, Min(0f)] private float minimumBurstSpeedMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float maximumBurstSpeedMultiplier = 1.2f;
        [SerializeField] private float burstAngularVelocity = 90f;
        [SerializeField, Min(0f)] private float popReleaseInterval = 0.12f;

        [Header("Marble Pop Polish")]
        [SerializeField, Min(0f)] private float launchPunchDuration = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float launchPunchStrength = 0.12f;

        public event Action OnGridCleared;

        private readonly List<Ball> aliveBalls = new List<Ball>();
        private readonly List<BallGroup> groups = new List<BallGroup>();
        private readonly List<Transform> holders = new List<Transform>();
        private bool hasLoggedMissingBallHolderPrefab;

        private void Start()
        {
            // GameManager supplies the selected level.
        }

        public void SetLevelData(LevelData newLevelData)
        {
            levelData = newLevelData;
        }

        // Distance-based lookup; avoids Physics2D.OverlapPoint, which isn't reliable
        // against just-reactivated pooled colliders in the same frame a level (re)spawns.
        public Ball FindBallAtWorldPosition(Vector2 worldPosition)
        {
            Ball closest = null;
            float closestSqrDistance = float.PositiveInfinity;

            foreach (Ball ball in aliveBalls)
            {
                if (ball == null || ball.State != BallState.InGrid) continue;

                float sqrDistance = ((Vector2)ball.transform.position - worldPosition).sqrMagnitude;
                float radius = ball.Radius;
                if (sqrDistance > radius * radius) continue;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = ball;
                }
            }

            return closest;
        }

        public bool TryPopHolderAtWorldPosition(Vector2 worldPosition)
        {
            BallGroup closestGroup = null;
            float closestSqrDistance = float.PositiveInfinity;
            float tapRadius = cellSize * 0.5f;

            foreach (BallGroup group in groups)
            {
                if (group == null || group.IsPopping || HasBallBelowLogical(group)) continue;

                float sqrDistance = ((Vector2)group.Position - worldPosition).sqrMagnitude;
                if (sqrDistance > tapRadius * tapRadius || sqrDistance >= closestSqrDistance) continue;

                closestGroup = group;
                closestSqrDistance = sqrDistance;
            }

            if (closestGroup == null) return false;

            closestGroup.TryPopFromHolder();
            return true;
        }

        public void ClearGrid()
        {
            StopAllCoroutines();

            foreach (Ball ball in aliveBalls)
            {
                if (ball != null) ballPool.Release(ball);
            }

            aliveBalls.Clear();
            groups.Clear();

            foreach (Transform holder in holders)
            {
                if (holder != null) Destroy(holder.gameObject);
            }

            holders.Clear();
        }

        public void SpawnGridFromLevelData(LevelData newLevelData)
        {
            ClearGrid();
            SetLevelData(newLevelData);
            SpawnGrid();
        }

        private void SpawnGrid()
        {
            int width = levelData.Width;
            int height = levelData.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCell cell = levelData.GetCell(x, y);
                    if (!cell.enabled) continue;

                    int ballCount = Mathf.Max(1, cell.ballCount);
                    BallGroup group = BallGroup.Create(this, cell.colorId, x, y);
                    Vector3 cellPosition = CellToWorldPosition(x, y, width, height);
                    BallHolder holder = CreateHolder(x, y, cellPosition, ballCount, cell.colorId);
                    group.SetHolder(holder, ballCount);
                    groups.Add(group);
                }
            }

            // Force the physics engine to see the new positions now, since autoSyncTransforms
            // is off by default and a tap could raycast against stale colliders otherwise.
            Physics2D.SyncTransforms();
            RefreshGridBallSprites();
        }

        private BallHolder CreateHolder(int x, int y, Vector3 position, int ballCount, int colorId)
        {
            BallHolder holder;
            if (ballHolderPrefab != null)
            {
                holder = Instantiate(ballHolderPrefab, transform);
            }
            else
            {
                if (!hasLoggedMissingBallHolderPrefab)
                {
                    Debug.LogError("GridManager requires a Ball Holder Prefab. Using fallback holders without count labels.", this);
                    hasLoggedMissingBallHolderPrefab = true;
                }
                GameObject holderObject = new GameObject();
                holderObject.transform.SetParent(transform, false);
                holder = holderObject.AddComponent<BallHolder>();
            }

            holder.name = $"BallHolder_{x}_{y}";
            holder.transform.position = position;
            holder.Configure(
                levelData.GetHolderAsset(colorId),
                levelData.GetBlockAsset(colorId),
                levelData.GetPressedAsset(colorId),
                ballCount);
            holders.Add(holder.transform);
            return holder;
        }

        private void SpawnExtraBall(int colorId, Vector3 originPosition, Transform holder, Vector2 launchVelocity)
        {
            Ball extraBall = ballPool.Get();
            if (holder != null) extraBall.transform.SetParent(holder, false);
            extraBall.transform.position = originPosition;
            extraBall.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));
            extraBall.Initialize(colorId, levelData.GetPopAsset(colorId), HandleBallPopped);
            extraBall.PopBurstFromState(launchVelocity, RandomBurstAngularVelocity());
            extraBall.PlayLaunchPunch(launchPunchDuration, launchPunchStrength);
            aliveBalls.Add(extraBall);
        }

        private Vector2 RandomBurstVelocity()
        {
            Vector2 direction = burstDirection.sqrMagnitude > 0.0001f ? burstDirection.normalized : Vector2.up;
            float angle = UnityEngine.Random.Range(-burstSpreadAngle, burstSpreadAngle);
            direction = (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction);

            float minSpeed = Mathf.Min(minimumBurstSpeedMultiplier, maximumBurstSpeedMultiplier);
            float maxSpeed = Mathf.Max(minimumBurstSpeedMultiplier, maximumBurstSpeedMultiplier);
            return direction * burstVelocity * UnityEngine.Random.Range(minSpeed, maxSpeed);
        }

        private float RandomBurstAngularVelocity()
        {
            return UnityEngine.Random.Range(-burstAngularVelocity, burstAngularVelocity);
        }

        private IEnumerator ReleaseRemainingBalls(
            int colorId,
            Vector3 popPosition,
            Transform holder,
            BallHolder holderDisplay,
            Vector2 launchVelocity,
            int remainingBallCount)
        {
            for (int ballIndex = remainingBallCount; ballIndex > 0; ballIndex--)
            {
                yield return new WaitForSeconds(popReleaseInterval);
                SpawnExtraBall(colorId, popPosition, holder, launchVelocity);
                holderDisplay?.SetCount(ballIndex - 1);
            }

            StartCoroutine(DestroyHolderAfterDelay(holder));
        }

        private Vector3 CellToWorldPosition(int x, int y, int width, int height)
        {
            float centeredX = (x - (width - 1) / 2f) * cellSize;
            float centeredY = ((height - 1) / 2f - y) * cellSize;
            return gridOrigin.position + new Vector3(centeredX, centeredY, 0f);
        }

        private IEnumerator DestroyHolderAfterDelay(Transform holder)
        {
            yield return new WaitForSeconds(1f);
            holders.Remove(holder);
            if (holder != null) Destroy(holder.gameObject);
        }

        private void HandleBallPopped(Ball ball)
        {
            aliveBalls.Remove(ball);
            RefreshGridBallSprites();
        }

        private void NotifyGridClearedIfAllHoldersPopped()
        {
            foreach (BallGroup group in groups)
            {
                if (group != null && !group.IsPopping) return;
            }

            OnGridCleared?.Invoke();
        }

        private void RefreshGridBallSprites()
        {
            foreach (BallGroup group in groups)
            {
                bool tappable = !HasBallBelowLogical(group);
                group.SetTappable(tappable);
            }
        }

        // Grid-data based stacking check; avoids Physics2D raycasts, which aren't reliable
        // against just-reactivated pooled colliders in the same frame a level (re)spawns.
        private bool HasBallBelowLogical(BallGroup group)
        {
            foreach (BallGroup otherGroup in groups)
            {
                if (otherGroup == null || otherGroup == group || otherGroup.IsPopping) continue;
                if (otherGroup.X == group.X && otherGroup.Y > group.Y) return true;
            }

            return false;
        }

        private sealed class BallGroup
        {
            private BallGroup(GridManager owner, int colorId, int x, int y)
            {
                this.owner = owner;
                this.colorId = colorId;
                X = x;
                Y = y;
            }

            private readonly GridManager owner;
            private readonly int colorId;
            private BallHolder holderDisplay;
            private Transform holder;
            private int remainingBalls;
            private bool popping;

            public int X { get; }
            public int Y { get; }
            public bool IsPopping => popping;
            public Vector3 Position => holder != null ? holder.position : Vector3.zero;

            public static BallGroup Create(GridManager owner, int colorId, int x, int y)
            {
                return new BallGroup(owner, colorId, x, y);
            }

            public void SetHolder(BallHolder ballHolder, int totalCount)
            {
                remainingBalls = totalCount;
                holder = ballHolder != null ? ballHolder.transform : null;
                holderDisplay = ballHolder;
                holderDisplay?.SetCount(totalCount);
            }

            public void TryPopFromHolder()
            {
                if (popping || holder == null) return;
                if (owner.HasBallBelowLogical(this)) return;

                popping = true;
                Vector3 popPosition = holder.position;
                Vector2 launchVelocity = owner.RandomBurstVelocity();
                holderDisplay?.SetPressed();
                owner.SpawnExtraBall(colorId, popPosition, holder, launchVelocity);
                remainingBalls--;
                holderDisplay?.SetCount(remainingBalls);
                owner.StartCoroutine(owner.ReleaseRemainingBalls(
                    colorId,
                    popPosition,
                    holder,
                    holderDisplay,
                    launchVelocity,
                    remainingBalls));
                remainingBalls = 0;
                owner.RefreshGridBallSprites();
                owner.NotifyGridClearedIfAllHoldersPopped();
            }

            public void SetTappable(bool tappable)
            {
                holderDisplay?.SetTappable(tappable);
            }
        }
    }
}
