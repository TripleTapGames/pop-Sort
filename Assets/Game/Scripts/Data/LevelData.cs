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
        Expert
    }

    [Serializable]
    public struct GridCell
    {
        public bool enabled;
        public int colorId;
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
        public int colorCount = 4;
        public float beltSpeed = 0.2f;
        public int slotsPerTray = 3;
        public int beltSlotCount = 7;
        public TrayColumnData[] trayColumns;

        public int Height => rows?.Length ?? 0;
        public int Width => Height > 0 ? rows[0].cells.Length : 0;

        public GridCell GetCell(int x, int y) => rows[y].cells[x];

        public Color GetColor(int colorId) => colorPalette[colorId];

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
                    counts[cell.colorId]++;
                }
            }

            return counts;
        }

        public TrayColumnData[] GenerateTrayColumnsFromGrid()
        {
            return GenerateTrayColumnsFromGrid(DefaultTrayColumnCount);
        }

        public TrayColumnData[] GenerateTrayColumnsFromGrid(int columnCount)
        {
            int[] ballCounts = CountBallsByColor();
            int trayCapacity = Mathf.Max(slotsPerTray, 1);
            int clampedColumnCount = Mathf.Max(columnCount, 1);
            List<TrayData> generatedTrays = new List<TrayData>();

            for (int colorId = 0; colorId < ballCounts.Length; colorId++)
            {
                int trayCount = Mathf.CeilToInt(ballCounts[colorId] / (float)trayCapacity);
                for (int i = 0; i < trayCount; i++)
                {
                    generatedTrays.Add(new TrayData(colorId, trayCapacity));
                }
            }

            int usedColumnCount = Mathf.Clamp(generatedTrays.Count, 1, clampedColumnCount);
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
    }
}
