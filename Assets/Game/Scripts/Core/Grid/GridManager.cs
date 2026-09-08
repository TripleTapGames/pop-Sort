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
        [SerializeField] private Transform gridOrigin;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float memberOffsetRadius = 0.08f;
        [SerializeField] private float burstVelocity = 0.35f;
        [SerializeField] private float burstAngularVelocity = 90f;

        public event Action OnGridCleared;

        private readonly List<Ball> aliveBalls = new List<Ball>();
        private readonly List<BallGroup> groups = new List<BallGroup>();
        private readonly List<Transform> holders = new List<Transform>();

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
                    Transform holder = CreateHolder(x, y, cellPosition, levelData.GetHolderAsset(cell.colorId));

                    Ball ball = ballPool.Get();
                    ball.transform.SetParent(holder, false);
                    ball.transform.position = cellPosition;
                    ball.transform.rotation = Quaternion.identity;
                    ball.Initialize(cell.colorId, levelData.GetPopAsset(cell.colorId), HandleBallPopped);
                    ball.ConfigureGroup(group, group.TryPop);
                    group.SetVisibleBall(ball, ballCount);
                    aliveBalls.Add(ball);
                    groups.Add(group);
                }
            }

            // Force the physics engine to see the new positions now, since autoSyncTransforms
            // is off by default and a tap could raycast against stale colliders otherwise.
            Physics2D.SyncTransforms();
            RefreshGridBallSprites();
        }

        private Transform CreateHolder(int x, int y, Vector3 position, Sprite holderSprite)
        {
            GameObject holderObject = new GameObject($"BallHolder_{x}_{y}");
            holderObject.transform.SetParent(transform, false);
            holderObject.transform.position = position;
            if (holderSprite != null)
            {
                SpriteRenderer holderRenderer = holderObject.AddComponent<SpriteRenderer>();
                holderRenderer.sprite = holderSprite;
                holderRenderer.sortingLayerName = "Ball";
                holderRenderer.sortingOrder = 0;
            }
            holders.Add(holderObject.transform);
            return holderObject.transform;
        }

        private void SpawnExtraBall(int colorId, Vector3 originPosition, Transform holder)
        {
            Ball extraBall = ballPool.Get();
            if (holder != null) extraBall.transform.SetParent(holder, false);
            extraBall.transform.position = originPosition + (Vector3)(UnityEngine.Random.insideUnitCircle * memberOffsetRadius);
            extraBall.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));
            extraBall.Initialize(colorId, levelData.GetPopAsset(colorId), HandleBallPopped);
            extraBall.PopBurstFromState(RandomBurstVelocity(), RandomBurstAngularVelocity());
            aliveBalls.Add(extraBall);
        }

        private Vector2 RandomBurstVelocity()
        {
            return UnityEngine.Random.insideUnitCircle.normalized * burstVelocity;
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

                Sprite sprite = HasBallBelowLogical(group)
                    ? levelData.GetBlockAsset(ball.ColorId)
                    : levelData.GetPopAsset(ball.ColorId);
                ball.SetSprite(sprite);
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
            private Transform holder;
            private int remainingBalls;
            private bool popping;

            public int X { get; }
            public int Y { get; }

            public static BallGroup Create(GridManager owner, int colorId, int x, int y)
            {
                return new BallGroup(owner, colorId, x, y);
            }

            public void SetVisibleBall(Ball ball, int totalCount)
            {
                visibleBall = ball;
                remainingBalls = totalCount - 1;
                holder = ball != null ? ball.transform.parent : null;
                ball.SetCount(totalCount);
            }

            public void TryPop(Ball source)
            {
                if (popping || source == null || visibleBall == null) return;
                if (owner.HasBallBelowLogical(this)) return;

                popping = true;
                Vector3 popPosition = holder != null ? holder.position : visibleBall.transform.position;
                Transform poppedHolder = holder;
                visibleBall.SetCount(1);
                visibleBall.PopBurst(owner.RandomBurstVelocity(), owner.RandomBurstAngularVelocity());

                for (int i = 0; i < remainingBalls; i++)
                {
                    owner.SpawnExtraBall(colorId, popPosition, holder);
                }

                remainingBalls = 0;
                owner.StartCoroutine(owner.DestroyHolderAfterDelay(poppedHolder));
            }
        }
    }
}
