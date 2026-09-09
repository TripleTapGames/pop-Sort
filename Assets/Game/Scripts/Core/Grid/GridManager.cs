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
        [SerializeField] private float memberOffsetRadius = 0.08f;

        [Header("Marble Pop Motion")]
        [SerializeField] private float burstVelocity = 0.35f;
        [SerializeField] private Vector2 burstDirection = new Vector2(0f, -1f);
        [SerializeField, Range(0f, 180f)] private float burstSpreadAngle = 55f;
        [SerializeField, Min(0f)] private float minimumBurstSpeedMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float maximumBurstSpeedMultiplier = 1.2f;
        [SerializeField] private float burstAngularVelocity = 90f;

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

                    Ball ball = ballPool.Get();
                    ball.transform.SetParent(holder.transform, false);
                    ball.transform.position = cellPosition;
                    ball.transform.rotation = Quaternion.identity;
                    ball.Initialize(cell.colorId, levelData.GetPopAsset(cell.colorId), HandleBallPopped);
                    ball.ConfigureGroup(group, group.TryPop);
                    group.SetVisibleBall(ball, holder, ballCount);
                    aliveBalls.Add(ball);
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
                SpriteRenderer holderRenderer = holderObject.AddComponent<SpriteRenderer>();
                holderRenderer.sortingLayerName = "Ball";
                holderRenderer.sortingOrder = 0;
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

        private void SpawnExtraBall(int colorId, Vector3 originPosition, Transform holder)
        {
            Ball extraBall = ballPool.Get();
            if (holder != null) extraBall.transform.SetParent(holder, false);
            extraBall.transform.position = originPosition + (Vector3)(UnityEngine.Random.insideUnitCircle * memberOffsetRadius);
            extraBall.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));
            extraBall.Initialize(colorId, levelData.GetPopAsset(colorId), HandleBallPopped);
            extraBall.PopBurstFromState(RandomBurstVelocity(), RandomBurstAngularVelocity());
            extraBall.PlayLaunchPunch(launchPunchDuration, launchPunchStrength);
            aliveBalls.Add(extraBall);
        }

        private Vector2 RandomBurstVelocity()
        {
            Vector2 direction = burstDirection.sqrMagnitude > 0.0001f ? burstDirection.normalized : Vector2.down;
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
            if (aliveBalls.Count == 0)
            {
                OnGridCleared?.Invoke();
            }
        }

        private void RefreshGridBallSprites()
        {
            foreach (Ball ball in aliveBalls)
            {
                if (ball == null || ball.State != BallState.InGrid) continue;
                if (!(ball.Group is BallGroup group)) continue;

                bool tappable = !HasBallBelowLogical(group);
                group.SetTappable(tappable);
                ball.SetSprite(levelData.GetPopAsset(ball.ColorId));
            }
        }

        // Grid-data based stacking check; avoids Physics2D raycasts, which aren't reliable
        // against just-reactivated pooled colliders in the same frame a level (re)spawns.
        private bool HasBallBelowLogical(BallGroup group)
        {
            foreach (Ball otherBall in aliveBalls)
            {
                if (otherBall == null || !(otherBall.Group is BallGroup otherGroup) || otherGroup == group) continue;
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
            private Ball visibleBall;
            private BallHolder holderDisplay;
            private Transform holder;
            private int remainingBalls;
            private bool popping;

            public int X { get; }
            public int Y { get; }

            public static BallGroup Create(GridManager owner, int colorId, int x, int y)
            {
                return new BallGroup(owner, colorId, x, y);
            }

            public void SetVisibleBall(Ball ball, BallHolder ballHolder, int totalCount)
            {
                visibleBall = ball;
                remainingBalls = totalCount - 1;
                holder = ball != null ? ball.transform.parent : null;
                holderDisplay = ballHolder;
                holderDisplay?.SetCount(totalCount);
            }

            public void TryPop(Ball source)
            {
                if (popping || source == null || visibleBall == null) return;
                if (owner.HasBallBelowLogical(this)) return;

                popping = true;
                Vector3 popPosition = holder != null ? holder.position : visibleBall.transform.position;
                Transform poppedHolder = holder;
                holderDisplay?.SetCount(0);
                holderDisplay?.SetPressed();
                visibleBall.PopBurst(owner.RandomBurstVelocity(), owner.RandomBurstAngularVelocity());
                visibleBall.PlayLaunchPunch(owner.launchPunchDuration, owner.launchPunchStrength);

                for (int i = 0; i < remainingBalls; i++)
                {
                    owner.SpawnExtraBall(colorId, popPosition, holder);
                }

                remainingBalls = 0;
                owner.StartCoroutine(owner.DestroyHolderAfterDelay(poppedHolder));
            }

            public void SetTappable(bool tappable)
            {
                holderDisplay?.SetTappable(tappable);
            }
        }
    }
}
