using System;
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

        public event Action OnGridCleared;

        private readonly List<Ball> aliveBalls = new List<Ball>();

        private void Start()
        {
            // GameManager supplies the selected level.
        }

        public void SetLevelData(LevelData newLevelData)
        {
            levelData = newLevelData;
        }

        public void ClearGrid()
        {
            foreach (Ball ball in aliveBalls)
            {
                if (ball != null) ballPool.Release(ball);
            }

            aliveBalls.Clear();
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

                    Ball ball = ballPool.Get();
                    ball.transform.position = CellToWorldPosition(x, y, width, height);
                    ball.Initialize(cell.colorId, levelData.GetColor(cell.colorId), HandleBallPopped);
                    aliveBalls.Add(ball);
                }
            }
        }

        private Vector3 CellToWorldPosition(int x, int y, int width, int height)
        {
            float centeredX = (x - (width - 1) / 2f) * cellSize;
            float centeredY = ((height - 1) / 2f - y) * cellSize;
            return gridOrigin.position + new Vector3(centeredX, centeredY, 0f);
        }

        private void HandleBallPopped(Ball ball)
        {
            aliveBalls.Remove(ball);
            if (aliveBalls.Count == 0)
            {
                OnGridCleared?.Invoke();
            }
        }
    }
}
