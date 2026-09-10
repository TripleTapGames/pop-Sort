using System.Collections.Generic;
using UnityEngine;

namespace PopSort.EditorTools
{
    public enum LevelValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct LevelValidationMessage
    {
        public readonly LevelValidationSeverity Severity;
        public readonly string Message;

        public LevelValidationMessage(LevelValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class LevelDataValidator
    {
        public static List<LevelValidationMessage> Validate(LevelData levelData)
        {
            List<LevelValidationMessage> messages = new List<LevelValidationMessage>();
            if (levelData == null)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "No level selected."));
                return messages;
            }

            if (levelData.rows == null || levelData.rows.Length == 0 || levelData.Width == 0)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "Grid is empty."));
                return messages;
            }

            if (levelData.colorPalette == null || levelData.colorPalette.Length == 0)
            {
                LevelValidationSeverity severity = levelData.colorCount == 0
                    ? LevelValidationSeverity.Warning
                    : LevelValidationSeverity.Error;
                messages.Add(new LevelValidationMessage(severity, "Color palette is empty."));
            }

            if (levelData.colorCount < 0)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "Color count cannot be negative."));
            }

            if (levelData.slotsPerTray <= 0)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "Slots per tray must be greater than 0."));
            }

            if (levelData.beltSlotCount <= 0)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "Belt slot count must be greater than 0."));
            }

            int enabledCells = 0;
            int[] ballCounts = levelData.CountBallsByColor();
            for (int y = 0; y < levelData.Height; y++)
            {
                if (levelData.rows[y]?.cells == null || levelData.rows[y].cells.Length != levelData.Width)
                {
                    messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Row {y} has an invalid cell count."));
                    continue;
                }

                for (int x = 0; x < levelData.Width; x++)
                {
                    GridCell cell = levelData.GetCell(x, y);
                    if (!cell.enabled) continue;

                    enabledCells++;
                    if (cell.ballCount < 1)
                    {
                        messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Cell ({x}, {y}) has ball count below 1."));
                    }

                    if (cell.colorId < 0 || cell.colorId >= levelData.colorCount)
                    {
                        messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Cell ({x}, {y}) has color id outside active color count."));
                    }

                    if (levelData.colorPalette != null && cell.colorId >= levelData.colorPalette.Length)
                    {
                        messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Cell ({x}, {y}) has no matching palette color."));
                    }

                    if (levelData.GetPopAsset(cell.colorId) == null || levelData.GetTrayAsset(cell.colorId) == null ||
                        levelData.GetHolderAsset(cell.colorId) == null || levelData.GetBlockAsset(cell.colorId) == null ||
                        levelData.GetPressedAsset(cell.colorId) == null)
                    {
                        messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Cell ({x}, {y}) color {cell.colorId} is missing pop, holder, block, pressed, or tray assets."));
                    }
                }
            }

            int maxTrayColumns = Mathf.Clamp(levelData.maxTrayColumnCount, 1, 3);
            if (levelData.trayColumns == null || levelData.trayColumns.Length < 1 || levelData.trayColumns.Length > maxTrayColumns)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Tray columns must be between 1 and {maxTrayColumns}."));
            }

            if (enabledCells == 0)
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, "Grid has no enabled cells."));
            }

            int trayCapacity = Mathf.Max(levelData.slotsPerTray, 1);
            int totalGeneratedTrays = 0;
            for (int colorId = 0; colorId < ballCounts.Length; colorId++)
            {
                if (ballCounts[colorId] == 0) continue;

                int trayCount = Mathf.CeilToInt(ballCounts[colorId] / (float)trayCapacity);
                totalGeneratedTrays += trayCount;
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Info, $"Color {colorId}: {ballCounts[colorId]} balls -> {trayCount} trays."));

                if (ballCounts[colorId] % trayCapacity != 0)
                {
                    messages.Add(new LevelValidationMessage(LevelValidationSeverity.Warning, $"Color {colorId} leaves a partially filled final tray."));
                }
            }

            if (levelData.trayColumns != null && !levelData.HasExactTrayCapacity())
            {
                messages.Add(new LevelValidationMessage(LevelValidationSeverity.Error, $"Tray capacity ({levelData.TotalTrayCapacity()}) must exactly equal total balls ({levelData.TotalBallCount()})."));
            }

            int previewColumns = levelData.trayColumns?.Length ?? 0;
            messages.Add(new LevelValidationMessage(LevelValidationSeverity.Info, $"Tray preview uses {previewColumns} columns, filled row-first left to right."));

            return messages;
        }
    }
}
