using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    public class TrayManager : MonoBehaviour
    {
        [SerializeField] private LevelData levelData;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private TraySlot traySlotPrefab;
        [SerializeField] private Transform[] columnAnchors;
        [SerializeField] private float generatedRowSpacing = 1.1f;
        [SerializeField] private bool generateFromLevelOnStart = true;
        [SerializeField] private TrayColumn[] trayColumns;

        private readonly List<GameObject> generatedObjects = new List<GameObject>();

        private void Start()
        {
            if (generateFromLevelOnStart)
            {
                GenerateFromLevel();
            }
        }

        [ContextMenu("Generate From Level")]
        public void GenerateFromLevel()
        {
            if (!CanGenerateFromLevel()) return;

            if (!HasAnyColumnAnchors()) return;

            TrayColumnData[] columnData = levelData.GenerateTrayColumnsFromGrid();
            if (!HasColumnAnchorsFor(columnData.Length)) return;

            ClearGeneratedObjects();

            trayColumns = new TrayColumn[columnData.Length];
            int spawnedTrayCount = 0;

            for (int columnIndex = 0; columnIndex < columnData.Length; columnIndex++)
            {
                GameObject columnObject = new GameObject($"TrayColumn_{columnIndex}");
                columnObject.transform.SetParent(transform);
                columnObject.transform.position = GetColumnPosition(columnIndex);
                generatedObjects.Add(columnObject);

                TrayColumn column = columnObject.AddComponent<TrayColumn>();
                TrayData[] trays = columnData[columnIndex].trays;
                TraySlot[] spawnedSlots = new TraySlot[trays.Length];

                for (int trayIndex = 0; trayIndex < trays.Length; trayIndex++)
                {
                    TraySlot traySlot = Instantiate(traySlotPrefab, columnObject.transform);
                    traySlot.name = $"Tray_{columnIndex}_{trayIndex}_Color_{trays[trayIndex].colorId}";
                    traySlot.gameObject.SetActive(true);
                    traySlot.transform.position = columnObject.transform.position + Vector3.down * (trayIndex * generatedRowSpacing);
                    traySlot.Configure(
                        trays[trayIndex].colorId,
                        trays[trayIndex].capacity,
                        levelData.GetColor(trays[trayIndex].colorId),
                        ballPool);
                    spawnedSlots[trayIndex] = traySlot;
                    spawnedTrayCount++;
                }

                column.Configure(spawnedSlots);
                trayColumns[columnIndex] = column;
            }

            Debug.Log($"TrayManager generated {spawnedTrayCount} trays across {trayColumns.Length} columns.", this);
        }

        public bool TryAcceptBall(Ball ball)
        {
            foreach (TrayColumn column in trayColumns)
            {
                if (column == null) continue;
                if (column.TryAcceptBall(ball)) return true;
            }

            return false;
        }

        private Vector3 GetColumnPosition(int columnIndex)
        {
            return columnAnchors[columnIndex].position;
        }

        private bool HasAnyColumnAnchors()
        {
            if (columnAnchors != null && columnAnchors.Length > 0) return true;

            Debug.LogError("TrayManager cannot generate trays: assign Column Anchors for the tray columns.", this);
            return false;
        }

        private bool HasColumnAnchorsFor(int columnCount)
        {
            if (columnAnchors == null || columnAnchors.Length < columnCount)
            {
                Debug.LogError($"TrayManager cannot generate trays: assign {columnCount} Column Anchors.", this);
                return false;
            }

            for (int i = 0; i < columnCount; i++)
            {
                if (columnAnchors[i] != null) continue;

                Debug.LogError($"TrayManager cannot generate trays: Column Anchors element {i} is not assigned.", this);
                return false;
            }

            return true;
        }

        private void ClearGeneratedObjects()
        {
            foreach (GameObject generatedObject in generatedObjects)
            {
                if (generatedObject != null) Destroy(generatedObject);
            }

            generatedObjects.Clear();
        }

        private bool CanGenerateFromLevel()
        {
            if (levelData == null)
            {
                Debug.LogError("TrayManager cannot generate trays: Level Data is not assigned.", this);
                return false;
            }

            if (traySlotPrefab == null)
            {
                Debug.LogError("TrayManager cannot generate trays: Tray Slot Prefab is not assigned.", this);
                return false;
            }

            if (ballPool == null)
            {
                Debug.LogError("TrayManager cannot generate trays: Ball Pool is not assigned.", this);
                return false;
            }

            int totalBalls = 0;
            int[] ballCounts = levelData.CountBallsByColor();
            foreach (int count in ballCounts)
            {
                totalBalls += count;
            }

            if (totalBalls == 0)
            {
                Debug.LogWarning("TrayManager generated no trays because the Level Data grid has no enabled balls.", this);
            }

            return true;
        }
    }
}
