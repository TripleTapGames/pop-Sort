using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PopSort
{
    public class TrayManager : MonoBehaviour
    {
        private LevelData levelData;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private TraySlot traySlotPrefab;
        [SerializeField] private Transform[] columnAnchors;
        [SerializeField] private Transform column1PickupPoint;
        [SerializeField] private Transform column2PickupPoint;
        [SerializeField] private Transform column3PickupPoint;

        [Header("Column Pickup Ranges")]
        [Tooltip("Half the horizontal collection width for Column 1. A value of 0.5 accepts balls from 0.5 units left to right of the pickup point.")]
        [FormerlySerializedAs("column1PickupRange")]
        [SerializeField, Min(0f)] private float column1PickupHalfWidth = 0.5f;
        [Tooltip("Vertical offset of Column 1's horizontal pickup band from its pickup point.")]
        [SerializeField] private float column1PickupVerticalOffset;
        [Tooltip("Half the vertical collection height for Column 1's pickup band.")]
        [SerializeField, Min(0f)] private float column1PickupHalfHeight = 0.25f;
        [Tooltip("Half the horizontal collection width for Column 2.")]
        [FormerlySerializedAs("column2PickupRange")]
        [SerializeField, Min(0f)] private float column2PickupHalfWidth = 0.5f;
        [Tooltip("Vertical offset of Column 2's horizontal pickup band from its pickup point.")]
        [SerializeField] private float column2PickupVerticalOffset;
        [Tooltip("Half the vertical collection height for Column 2's pickup band.")]
        [SerializeField, Min(0f)] private float column2PickupHalfHeight = 0.25f;
        [Tooltip("Half the horizontal collection width for Column 3.")]
        [FormerlySerializedAs("column3PickupRange")]
        [SerializeField, Min(0f)] private float column3PickupHalfWidth = 0.5f;
        [Tooltip("Vertical offset of Column 3's horizontal pickup band from its pickup point.")]
        [SerializeField] private float column3PickupVerticalOffset;
        [Tooltip("Half the vertical collection height for Column 3's pickup band.")]
        [SerializeField, Min(0f)] private float column3PickupHalfHeight = 0.25f;
        [SerializeField] private float generatedRowSpacing = 1.1f;

        [Header("Ball Landing Motion")]
        [SerializeField] private TrayLandingSettings trayLandingSettings = TrayLandingSettings.CreateDefault();

        [Header("Completion VFX")]
        [SerializeField] private TrayCompletionVfx completionVfxPrefab;
        [SerializeField, Min(0.01f)] private float completionVfxLifetime = 0.8f;
        [SerializeField, Min(0.01f)] private float completionVfxScale = 1f;
        private TrayColumn[] trayColumns;

        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private readonly List<TrayCompletionVfx> completionVfxInstances = new List<TrayCompletionVfx>();

        private void Start()
        {
            // GameManager supplies the selected level.
        }

        [ContextMenu("Generate From Level")]
        public void GenerateFromLevel()
        {
            if (!CanGenerateFromLevel()) return;

            if (!HasAnyColumnAnchors()) return;

            TrayColumnData[] columnData = levelData.GenerateTrayColumnsFromGrid();
            if (!HasColumnAnchorsFor(columnData.Length)) return;
            if (!HasPickupPointsFor(columnData.Length)) return;

            ClearGeneratedObjects();

            trayColumns = new TrayColumn[columnData.Length];
            int spawnedTrayCount = 0;

            for (int columnIndex = 0; columnIndex < columnData.Length; columnIndex++)
            {
                GameObject columnObject = new GameObject($"TrayColumn_{columnIndex}");
                columnObject.transform.SetParent(transform);
                columnObject.transform.position = GetColumnPosition(columnIndex, columnData.Length);
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
                        levelData.GetTrayAsset(trays[trayIndex].colorId),
                        levelData.GetTrayCoverAsset(trays[trayIndex].colorId),
                        ballPool,
                        trayLandingSettings,
                        PlayCompletionVfx);
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

        public Transform GetColumnPickupPoint(int columnIndex)
        {
            return GetColumnPickupPoint(columnIndex, ColumnCount);
        }

        private Transform GetColumnPickupPoint(int columnIndex, int columnCount)
        {
            return GetPickupPoint(GetPickupPointIndex(columnIndex, columnCount));
        }

        private Transform GetPickupPoint(int pickupPointIndex)
        {
            return pickupPointIndex switch
            {
                0 => column1PickupPoint,
                1 => column2PickupPoint,
                2 => column3PickupPoint,
                _ => null
            };
        }

        public bool IsWithinColumnPickupRange(int columnIndex, Vector3 worldPosition)
        {
            int columnCount = ColumnCount;
            Transform pickupPoint = GetColumnPickupPoint(columnIndex, columnCount);
            if (pickupPoint == null) return false;

            Vector2 rangeCenter = pickupPoint.position + Vector3.up * GetColumnPickupVerticalOffset(columnIndex);
            Vector2 offset = (Vector2)worldPosition - rangeCenter;
            return Mathf.Abs(offset.x) <= GetColumnPickupHalfWidth(columnIndex) &&
                Mathf.Abs(offset.y) <= GetColumnPickupHalfHeight(columnIndex);
        }

        public bool TryAcceptBallAtColumn(int columnIndex, Ball ball)
        {
            if (trayColumns == null || columnIndex < 0 || columnIndex >= trayColumns.Length) return false;

            TrayColumn column = trayColumns[columnIndex];
            return column != null && column.TryAcceptBall(ball);
        }

        public int ColumnCount => trayColumns?.Length ?? 0;

        public void SetLevelData(LevelData newLevelData)
        {
            levelData = newLevelData;
        }

        public bool AreAllTraysComplete()
        {
            if (trayColumns == null || trayColumns.Length == 0) return false;

            foreach (TrayColumn column in trayColumns)
            {
                if (column == null || !column.IsComplete) return false;
            }

            return true;
        }

        public void ClearGeneratedTrays()
        {
            StopCompletionVfx();

            if (trayColumns != null)
            {
                foreach (TrayColumn column in trayColumns)
                {
                    if (column == null) continue;
                    foreach (TraySlot slot in column.GetComponentsInChildren<TraySlot>(true))
                    {
                        slot.ClearBalls();
                    }
                }
            }

            ClearGeneratedObjects();
            trayColumns = null;
        }

        public void LoadLevelData(LevelData newLevelData)
        {
            ClearGeneratedTrays();
            SetLevelData(newLevelData);
            GenerateFromLevel();
        }

        private void PlayCompletionVfx(Vector3 position)
        {
            if (completionVfxPrefab == null) return;

            TrayCompletionVfx vfx = GetAvailableCompletionVfx();
            vfx.Play(position, completionVfxScale, completionVfxLifetime, null);
        }

        private TrayCompletionVfx GetAvailableCompletionVfx()
        {
            foreach (TrayCompletionVfx pooledInstance in completionVfxInstances)
            {
                if (pooledInstance != null && !pooledInstance.gameObject.activeSelf) return pooledInstance;
            }

            TrayCompletionVfx newInstance = Instantiate(completionVfxPrefab, transform);
            newInstance.gameObject.SetActive(false);
            completionVfxInstances.Add(newInstance);
            return newInstance;
        }

        private void StopCompletionVfx()
        {
            foreach (TrayCompletionVfx instance in completionVfxInstances)
            {
                if (instance != null) instance.StopAndHide();
            }
        }

        private float GetColumnPickupHalfWidth(int columnIndex)
        {
            return GetPickupHalfWidth(GetPickupPointIndex(columnIndex, ColumnCount));
        }

        private float GetPickupHalfWidth(int pickupPointIndex)
        {
            return pickupPointIndex switch
            {
                0 => column1PickupHalfWidth,
                1 => column2PickupHalfWidth,
                2 => column3PickupHalfWidth,
                _ => 0f
            };
        }

        private float GetColumnPickupVerticalOffset(int columnIndex)
        {
            return GetPickupVerticalOffset(GetPickupPointIndex(columnIndex, ColumnCount));
        }

        private float GetPickupVerticalOffset(int pickupPointIndex)
        {
            return pickupPointIndex switch
            {
                0 => column1PickupVerticalOffset,
                1 => column2PickupVerticalOffset,
                2 => column3PickupVerticalOffset,
                _ => 0f
            };
        }

        private float GetColumnPickupHalfHeight(int columnIndex)
        {
            return GetPickupHalfHeight(GetPickupPointIndex(columnIndex, ColumnCount));
        }

        private float GetPickupHalfHeight(int pickupPointIndex)
        {
            return pickupPointIndex switch
            {
                0 => column1PickupHalfHeight,
                1 => column2PickupHalfHeight,
                2 => column3PickupHalfHeight,
                _ => 0f
            };
        }

        private void OnDrawGizmosSelected()
        {
            DrawPickupRange(column1PickupPoint, column1PickupHalfWidth, column1PickupVerticalOffset, column1PickupHalfHeight);
            DrawPickupRange(column2PickupPoint, column2PickupHalfWidth, column2PickupVerticalOffset, column2PickupHalfHeight);
            DrawPickupRange(column3PickupPoint, column3PickupHalfWidth, column3PickupVerticalOffset, column3PickupHalfHeight);
        }

        private static void DrawPickupRange(Transform pickupPoint, float halfWidth, float verticalOffset, float halfHeight)
        {
            if (pickupPoint == null || halfWidth <= 0f || halfHeight <= 0f) return;

            Gizmos.color = Color.cyan;
            Vector3 center = pickupPoint.position + Vector3.up * verticalOffset;
            Gizmos.DrawWireCube(center, new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));
        }

        private Vector3 GetColumnPosition(int columnIndex, int columnCount)
        {
            return columnAnchors[GetColumnAnchorIndex(columnIndex, columnCount)].position;
        }

        private int GetColumnAnchorIndex(int columnIndex, int columnCount)
        {
            if (columnCount <= 1) return columnAnchors.Length / 2;

            float normalizedColumnIndex = columnIndex / (float)(columnCount - 1);
            return Mathf.RoundToInt(normalizedColumnIndex * (columnAnchors.Length - 1));
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
                int anchorIndex = GetColumnAnchorIndex(i, columnCount);
                if (columnAnchors[anchorIndex] != null) continue;

                Debug.LogError($"TrayManager cannot generate trays: Column Anchors element {anchorIndex} is not assigned.", this);
                return false;
            }

            return true;
        }

        // Catches a silent soft-lock: balls queued for a column with no pickup point can never be collected.
        private bool HasPickupPointsFor(int columnCount)
        {
            for (int i = 0; i < columnCount; i++)
            {
                if (GetColumnPickupPoint(i, columnCount) != null) continue;

                Debug.LogError($"TrayManager cannot generate trays: no pickup point assigned for tray column {i}.", this);
                return false;
            }

            return true;
        }

        // Keep pickup lanes aligned with the anchor selection: one column uses the
        // middle lane, while two columns use the outer lanes.
        private static int GetPickupPointIndex(int columnIndex, int columnCount)
        {
            if (columnCount <= 1) return 1;

            float normalizedIndex = Mathf.Clamp01(columnIndex / (float)(columnCount - 1));
            return Mathf.RoundToInt(normalizedIndex * 2f);
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
        public bool CanAcceptColor(int colorId)
        {
            foreach (TrayColumn column in trayColumns)
            {
                if (column != null && column.CanAccept(colorId)) return true;
            }

            return false;
        }
    }
}
