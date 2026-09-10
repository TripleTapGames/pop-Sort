using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    public enum LevelDifficulty
    {
        Easy,
        Medium,
        Hard,
        SuperHard
    }

    [Serializable]
    public class DifficultyParameters
    {
        [Range(0f, 1f)] public float maxCanonicalConveyorPressure = 0.25f;
        [Range(0f, 1f)] public float forcedReliefThreshold = 0.15f;
        public int usefulBallUnlockDepth = 1;
        [Range(0f, 1f)] public float colourRepetition = 1f;
        [Range(0f, 1f)] public float verticalColourClustering = 1f;
    }

    [Serializable]
    public struct GridCell
    {
        public bool enabled;
        public int colorId;
        public int ballCount;
    }

    [Serializable]
    public class GridRow
    {
        public GridCell[] cells;
    }

    [Serializable]
    public struct TrayData
    {
        public int colorId;
        public int capacity;

        public TrayData(int colorId, int capacity)
        {
            this.colorId = colorId;
            this.capacity = capacity;
        }
    }

    [Serializable]
    public class TrayColumnData
    {
        public TrayData[] trays;
    }

    [CreateAssetMenu(fileName = "LevelData", menuName = "PopSort/Level Data")]
    public class LevelData : ScriptableObject
    {
        public const int DefaultTrayColumnCount = 4;

        public LevelDifficulty difficulty;
        public GridRow[] rows;
        public Color[] colorPalette;
        public Sprite[] popAssets;
        public Sprite[] trayAssets;
        public Sprite[] holderAssets;
        public Sprite[] blockAssets;
        public Sprite[] pressedAssets;
        public int colorCount = 4;
        public float beltSpeed = 0.2f;
        public int slotsPerTray = 3;
        public int beltSlotCount = 7;
        [Range(1, 3)] public int maxTrayColumnCount = 3;
        public TrayColumnData[] trayColumns;
        public DifficultyParameters difficultyParameters = new DifficultyParameters();

        public int Height => rows?.Length ?? 0;
        public int Width => Height > 0 ? rows[0].cells.Length : 0;

        public GridCell GetCell(int x, int y) => rows[y].cells[x];

        public Color GetColor(int colorId) => colorPalette[colorId];

        public Sprite GetPopAsset(int colorId)
        {
            return popAssets != null && colorId >= 0 && colorId < popAssets.Length ? popAssets[colorId] : null;
        }

        public Sprite GetTrayAsset(int colorId)
        {
            return trayAssets != null && colorId >= 0 && colorId < trayAssets.Length ? trayAssets[colorId] : null;
        }

        public Sprite GetHolderAsset(int colorId)
        {
            return holderAssets != null && colorId >= 0 && colorId < holderAssets.Length ? holderAssets[colorId] : null;
        }

        public Sprite GetBlockAsset(int colorId)
        {
            return blockAssets != null && colorId >= 0 && colorId < blockAssets.Length ? blockAssets[colorId] : null;
        }

        public Sprite GetPressedAsset(int colorId)
        {
            return pressedAssets != null && colorId >= 0 && colorId < pressedAssets.Length ? pressedAssets[colorId] : null;
        }

        public int[] CountBallsByColor()
        {
            int countLength = Mathf.Max(colorCount, colorPalette?.Length ?? 0);
            int[] counts = new int[countLength];

            if (rows == null) return counts;

            foreach (GridRow row in rows)
            {
                if (row?.cells == null) continue;

                foreach (GridCell cell in row.cells)
                {
                    if (!cell.enabled || cell.colorId < 0 || cell.colorId >= counts.Length) continue;
                    counts[cell.colorId] += Mathf.Max(1, cell.ballCount);
                }
            }

            return counts;
        }

        public int TotalBallCount()
        {
            int total = 0;
            foreach (int count in CountBallsByColor()) total += count;
            return total;
        }

        public int TotalTrayCapacity()
        {
            if (trayColumns == null) return 0;

            int total = 0;
            foreach (TrayColumnData column in trayColumns)
            {
                if (column?.trays == null) continue;
                foreach (TrayData tray in column.trays) total += Mathf.Max(tray.capacity, 0);
            }

            return total;
        }

        public bool HasExactTrayCapacity() => TotalTrayCapacity() == TotalBallCount();

        public TrayColumnData[] GenerateTrayColumnsFromGrid()
        {
            return GenerateTrayColumnsFromGrid(maxTrayColumnCount);
        }

        public TrayColumnData[] GenerateTrayColumnsFromGrid(int columnCount)
        {
            int[] ballCounts = CountBallsByColor();
            int trayCapacity = Mathf.Max(slotsPerTray, 1);
            int clampedColumnCount = Mathf.Clamp(columnCount, 1, Mathf.Clamp(maxTrayColumnCount, 1, 3));
            List<TrayData>[] traysByColor = new List<TrayData>[ballCounts.Length];

            for (int colorId = 0; colorId < ballCounts.Length; colorId++)
            {
                traysByColor[colorId] = new List<TrayData>();
                int trayCount = Mathf.CeilToInt(ballCounts[colorId] / (float)trayCapacity);
                for (int trayIndex = 0; trayIndex < trayCount; trayIndex++)
                {
                    int remaining = ballCounts[colorId] - trayIndex * trayCapacity;
                    traysByColor[colorId].Add(new TrayData(colorId, Mathf.Min(trayCapacity, remaining)));
                }
            }

            List<TrayData> generatedTrays = BuildTrayOrder(traysByColor, BuildGridColourOrder());
            int usedColumnCount = clampedColumnCount;
            List<TrayData>[] columnTrays = new List<TrayData>[usedColumnCount];

            for (int columnIndex = 0; columnIndex < columnTrays.Length; columnIndex++)
            {
                columnTrays[columnIndex] = new List<TrayData>();
            }

            for (int trayIndex = 0; trayIndex < generatedTrays.Count; trayIndex++)
            {
                int columnIndex = trayIndex % usedColumnCount;
                columnTrays[columnIndex].Add(generatedTrays[trayIndex]);
            }

            TrayColumnData[] generatedColumns = new TrayColumnData[usedColumnCount];
            for (int columnIndex = 0; columnIndex < generatedColumns.Length; columnIndex++)
            {
                generatedColumns[columnIndex] = new TrayColumnData { trays = columnTrays[columnIndex].ToArray() };
            }

            trayColumns = generatedColumns;
            return trayColumns;
        }

        private List<TrayData> BuildTrayOrder(List<TrayData>[] traysByColor, List<int> gridColourOrder)
        {
            List<TrayData> orderedTrays = new List<TrayData>();
            int[] nextTrayByColor = new int[traysByColor.Length];
            int previousColorId = -1;
            int totalTrays = CountTrays(traysByColor);

            while (orderedTrays.Count < totalTrays)
            {
                int selectedColorId = SelectNextTrayColor(traysByColor, nextTrayByColor, gridColourOrder, previousColorId);
                if (selectedColorId < 0) break;

                orderedTrays.Add(traysByColor[selectedColorId][nextTrayByColor[selectedColorId]++]);
                previousColorId = selectedColorId;
            }

            return orderedTrays;
        }

        private int SelectNextTrayColor(
            List<TrayData>[] traysByColor,
            int[] nextTrayByColor,
            List<int> gridColourOrder,
            int previousColorId)
        {
            int selectedColorId = -1;
            float selectedScore = float.MaxValue;
            float interleaveAmount = GetTrayInterleaveAmount();
            int targetOffset = Mathf.RoundToInt(Mathf.Lerp(0f, GetUsefulBallUnlockDepth() - 1, interleaveAmount));

            for (int colorId = 0; colorId < traysByColor.Length; colorId++)
            {
                if (nextTrayByColor[colorId] >= traysByColor[colorId].Count) continue;

                int alignmentIndex = FindColourIndex(gridColourOrder, colorId);
                float alignmentDistance = alignmentIndex < 0 ? gridColourOrder.Count : alignmentIndex;
                float score = Mathf.Abs(alignmentDistance - targetOffset);
                if (colorId == previousColorId) score += (1f - GetVerticalColourClustering()) * 2f;

                if (score < selectedScore)
                {
                    selectedScore = score;
                    selectedColorId = colorId;
                }
            }

            return selectedColorId;
        }

        private List<int> BuildGridColourOrder()
        {
            List<int> colourOrder = new List<int>();
            if (rows == null || rows.Length == 0) return colourOrder;

            for (int x = 0; x < Width; x++)
            {
                for (int y = rows.Length - 1; y >= 0; y--)
                {
                    if (rows[y]?.cells == null || x >= rows[y].cells.Length) continue;
                    int colorId = rows[y].cells[x].colorId;
                    if (rows[y].cells[x].enabled && !colourOrder.Contains(colorId)) colourOrder.Add(colorId);
                }
            }

            return colourOrder;
        }

        private static int FindColourIndex(List<int> colourOrder, int colorId)
        {
            for (int i = 0; i < colourOrder.Count; i++)
            {
                if (colourOrder[i] == colorId) return i;
            }

            return -1;
        }

        private static int CountTrays(List<TrayData>[] traysByColor)
        {
            int count = 0;
            foreach (List<TrayData> colorTrays in traysByColor) count += colorTrays.Count;
            return count;
        }

        private float GetTrayInterleaveAmount()
        {
            if (difficultyParameters == null)
            {
                return 1f - GetColourRepetition();
            }

            float pressure = Mathf.Clamp01(difficultyParameters.maxCanonicalConveyorPressure);
            float reliefDelay = Mathf.Clamp01(difficultyParameters.forcedReliefThreshold);
            float unlockDepth = Mathf.InverseLerp(1f, 4f, Mathf.Max(1, difficultyParameters.usefulBallUnlockDepth));
            float lowVerticalClustering = 1f - Mathf.Clamp01(difficultyParameters.verticalColourClustering);
            float lowColourRepetition = 1f - GetColourRepetition();

            return Mathf.Clamp01(Mathf.Max(
                pressure,
                reliefDelay,
                unlockDepth,
                lowVerticalClustering,
                lowColourRepetition));
        }

        private int GetUsefulBallUnlockDepth()
        {
            return difficultyParameters == null
                ? 1
                : Mathf.Max(1, difficultyParameters.usefulBallUnlockDepth);
        }

        private float GetVerticalColourClustering()
        {
            return difficultyParameters == null
                ? 1f
                : Mathf.Clamp01(difficultyParameters.verticalColourClustering);
        }

        private float GetColourRepetition()
        {
            if (difficultyParameters != null) return Mathf.Clamp01(difficultyParameters.colourRepetition);
            return difficulty == LevelDifficulty.Easy ? 1f : difficulty == LevelDifficulty.Medium ? 0.65f : 0.1f;
        }

    }
}
