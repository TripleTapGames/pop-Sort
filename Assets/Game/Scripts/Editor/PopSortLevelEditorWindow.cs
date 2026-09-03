using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PopSort.EditorTools
{
    public class PopSortLevelEditorWindow : EditorWindow
    {
        private const int CellSize = 24;
        private const string DefaultLevelFolder = "Assets/Game/Data/Levels";

        private LevelData levelData;
        private int gridWidth = 5;
        private int gridHeight = 5;
        private int activeColorId;
        private Vector2 scrollPosition;

        [MenuItem("Tools/PopSort/Level Editor")]
        public static void Open()
        {
            GetWindow<PopSortLevelEditorWindow>("PopSort Level Editor");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawAssetSection();
            if (levelData == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(8f);
            DrawDifficultySection();
            EditorGUILayout.Space(8f);
            DrawPaletteSection();
            EditorGUILayout.Space(8f);
            DrawGridSection();
            EditorGUILayout.Space(8f);
            DrawGeneratedTraySection();
            EditorGUILayout.Space(8f);
            DrawValidationSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetSection()
        {
            EditorGUILayout.LabelField("Level Asset", EditorStyles.boldLabel);
            levelData = (LevelData)EditorGUILayout.ObjectField("Level Data", levelData, typeof(LevelData), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Level"))
                {
                    CreateNewLevelAsset();
                }

                using (new EditorGUI.DisabledScope(levelData == null))
                {
                    if (GUILayout.Button("Save"))
                    {
                        SaveLevel();
                    }
                }
            }
        }

        private void DrawDifficultySection()
        {
            EditorGUILayout.LabelField("Difficulty", EditorStyles.boldLabel);
            Undo.RecordObject(levelData, "Edit Level Difficulty");

            EditorGUI.BeginChangeCheck();
            LevelDifficulty newDifficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", levelData.difficulty);
            bool difficultyChanged = EditorGUI.EndChangeCheck();
            levelData.difficulty = newDifficulty;

            levelData.beltSpeed = EditorGUILayout.FloatField("Belt Speed", levelData.beltSpeed);
            levelData.colorCount = Mathf.Max(1, EditorGUILayout.IntField("Color Count", levelData.colorCount));
            levelData.beltSlotCount = Mathf.Max(1, EditorGUILayout.IntField("Belt Slot Count", levelData.beltSlotCount));
            levelData.slotsPerTray = Mathf.Max(1, EditorGUILayout.IntField("Slots Per Tray", levelData.slotsPerTray));

            if (difficultyChanged)
            {
                ApplyDifficultyPreset();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Difficulty Preset"))
                {
                    ApplyDifficultyPreset();
                }

                if (GUILayout.Button("Randomize Level"))
                {
                    GenerateRandomLevel();
                }
            }

            EnsurePaletteSize();
            EditorUtility.SetDirty(levelData);
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            EnsurePaletteSize();

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < levelData.colorCount; i++)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(70f)))
                    {
                        levelData.colorPalette[i] = EditorGUILayout.ColorField(levelData.colorPalette[i], GUILayout.Width(60f));
                        if (GUILayout.Toggle(activeColorId == i, $"Color {i}", "Button", GUILayout.Width(65f)))
                        {
                            activeColorId = i;
                        }
                    }
                }
            }
        }

        private void DrawGridSection()
        {
            EditorGUILayout.LabelField("Grid Painter", EditorStyles.boldLabel);

            gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", gridWidth));
            gridHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", gridHeight));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Resize Grid"))
                {
                    ResizeGrid(gridWidth, gridHeight);
                }

                if (GUILayout.Button("Fill Grid"))
                {
                    FillGrid();
                }

                if (GUILayout.Button("Clear Grid"))
                {
                    ClearGrid();
                }

                if (GUILayout.Button("Mirror Horizontal"))
                {
                    MirrorGridHorizontal();
                }
            }

            if (levelData.rows == null || levelData.rows.Length == 0)
            {
                ResizeGrid(gridWidth, gridHeight);
            }

            for (int y = 0; y < levelData.Height; y++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int x = 0; x < levelData.Width; x++)
                    {
                        DrawGridCell(x, y);
                    }
                }
            }
        }

        private void DrawGridCell(int x, int y)
        {
            GridCell cell = levelData.rows[y].cells[x];
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = cell.enabled ? levelData.GetColor(Mathf.Clamp(cell.colorId, 0, levelData.colorPalette.Length - 1)) : Color.gray;

            Rect rect = GUILayoutUtility.GetRect(CellSize, CellSize, GUILayout.Width(CellSize), GUILayout.Height(CellSize));
            if (GUI.Button(rect, cell.enabled ? cell.colorId.ToString() : ""))
            {
                Undo.RecordObject(levelData, "Paint Grid Cell");
                cell.enabled = true;
                cell.colorId = activeColorId;
                levelData.rows[y].cells[x] = cell;
                EditorUtility.SetDirty(levelData);
            }

            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition))
            {
                Undo.RecordObject(levelData, "Disable Grid Cell");
                cell.enabled = false;
                levelData.rows[y].cells[x] = cell;
                EditorUtility.SetDirty(levelData);
                current.Use();
            }

            GUI.backgroundColor = oldColor;
        }

        private void DrawGeneratedTraySection()
        {
            EditorGUILayout.LabelField("Generated Trays", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Trays From Grid Counts"))
            {
                Undo.RecordObject(levelData, "Generate Trays From Grid");
                levelData.GenerateTrayColumnsFromGrid();
                EditorUtility.SetDirty(levelData);
            }

            int[] counts = levelData.CountBallsByColor();
            int trayCapacity = Mathf.Max(levelData.slotsPerTray, 1);
            for (int colorId = 0; colorId < counts.Length; colorId++)
            {
                if (counts[colorId] == 0) continue;

                int trayCount = Mathf.CeilToInt(counts[colorId] / (float)trayCapacity);
                EditorGUILayout.LabelField($"Color {colorId}", $"{counts[colorId]} balls -> {trayCount} trays");
            }

            if (levelData.trayColumns != null)
            {
                EditorGUILayout.LabelField("Generated Columns", levelData.trayColumns.Length.ToString());
                for (int columnIndex = 0; columnIndex < levelData.trayColumns.Length; columnIndex++)
                {
                    TrayData[] trays = levelData.trayColumns[columnIndex].trays;
                    string label = trays == null || trays.Length == 0 ? "empty" : string.Join(", ", GetTrayLabels(trays));
                    EditorGUILayout.LabelField($"Column {columnIndex}", label);
                }
            }
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            List<LevelValidationMessage> messages = LevelDataValidator.Validate(levelData);
            foreach (LevelValidationMessage message in messages)
            {
                MessageType messageType = message.Severity switch
                {
                    LevelValidationSeverity.Error => MessageType.Error,
                    LevelValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(message.Message, messageType);
            }
        }

        private void CreateNewLevelAsset()
        {
            EnsureFolder("Assets/Game/Data");
            EnsureFolder(DefaultLevelFolder);

            LevelData newLevel = CreateInstance<LevelData>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultLevelFolder}/Level_New.asset");
            AssetDatabase.CreateAsset(newLevel, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            levelData = newLevel;
            ApplyDifficultyPreset();
            SaveLevel();
        }

        private void ApplyDifficultyPreset()
        {
            Undo.RecordObject(levelData, "Apply Difficulty Preset");

            switch (levelData.difficulty)
            {
                case LevelDifficulty.Easy:
                    gridWidth = 4;
                    gridHeight = 4;
                    levelData.colorCount = 2;
                    levelData.beltSpeed = 0.12f;
                    levelData.beltSlotCount = 10;
                    levelData.slotsPerTray = 3;
                    break;
                case LevelDifficulty.Medium:
                    gridWidth = 5;
                    gridHeight = 5;
                    levelData.colorCount = 3;
                    levelData.beltSpeed = 0.18f;
                    levelData.beltSlotCount = 8;
                    levelData.slotsPerTray = 3;
                    break;
                case LevelDifficulty.Hard:
                    gridWidth = 6;
                    gridHeight = 6;
                    levelData.colorCount = 4;
                    levelData.beltSpeed = 0.26f;
                    levelData.beltSlotCount = 7;
                    levelData.slotsPerTray = 3;
                    break;
                case LevelDifficulty.Expert:
                    gridWidth = 7;
                    gridHeight = 7;
                    levelData.colorCount = 5;
                    levelData.beltSpeed = 0.34f;
                    levelData.beltSlotCount = 6;
                    levelData.slotsPerTray = 3;
                    break;
            }

            EnsurePaletteSize();
            GenerateRandomLevel();
            EditorUtility.SetDirty(levelData);
        }

        private void GenerateRandomLevel()
        {
            Undo.RecordObject(levelData, "Generate Random Level");
            ResizeGrid(gridWidth, gridHeight);

            int trayCapacity = Mathf.Max(levelData.slotsPerTray, 1);
            int totalCells = gridWidth * gridHeight;
            int totalGroups = totalCells / trayCapacity;
            int colorCount = Mathf.Max(levelData.colorCount, 1);

            List<int> ballColors = new List<int>();
            for (int groupIndex = 0; groupIndex < totalGroups; groupIndex++)
            {
                int colorId = groupIndex % colorCount;
                for (int i = 0; i < trayCapacity; i++)
                {
                    ballColors.Add(colorId);
                }
            }

            for (int i = ballColors.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ballColors[i], ballColors[j]) = (ballColors[j], ballColors[i]);
            }

            int ballIndex = 0;
            for (int y = 0; y < levelData.Height; y++)
            {
                for (int x = 0; x < levelData.Width; x++)
                {
                    levelData.rows[y].cells[x] = ballIndex < ballColors.Count
                        ? new GridCell { enabled = true, colorId = ballColors[ballIndex++] }
                        : new GridCell();
                }
            }

            levelData.GenerateTrayColumnsFromGrid();
            EditorUtility.SetDirty(levelData);
        }

        private void ResizeGrid(int width, int height)
        {
            Undo.RecordObject(levelData, "Resize Level Grid");

            GridRow[] oldRows = levelData.rows;
            levelData.rows = new GridRow[height];
            for (int y = 0; y < height; y++)
            {
                levelData.rows[y] = new GridRow { cells = new GridCell[width] };
                for (int x = 0; x < width; x++)
                {
                    if (oldRows != null && y < oldRows.Length && oldRows[y]?.cells != null && x < oldRows[y].cells.Length)
                    {
                        levelData.rows[y].cells[x] = oldRows[y].cells[x];
                    }
                }
            }

            EditorUtility.SetDirty(levelData);
        }

        private void FillGrid()
        {
            Undo.RecordObject(levelData, "Fill Level Grid");
            for (int y = 0; y < levelData.Height; y++)
            {
                for (int x = 0; x < levelData.Width; x++)
                {
                    levelData.rows[y].cells[x] = new GridCell { enabled = true, colorId = activeColorId };
                }
            }

            EditorUtility.SetDirty(levelData);
        }

        private void ClearGrid()
        {
            Undo.RecordObject(levelData, "Clear Level Grid");
            for (int y = 0; y < levelData.Height; y++)
            {
                for (int x = 0; x < levelData.Width; x++)
                {
                    levelData.rows[y].cells[x] = new GridCell();
                }
            }

            EditorUtility.SetDirty(levelData);
        }

        private void MirrorGridHorizontal()
        {
            Undo.RecordObject(levelData, "Mirror Level Grid Horizontally");
            for (int y = 0; y < levelData.Height; y++)
            {
                for (int x = 0; x < levelData.Width / 2; x++)
                {
                    int oppositeX = levelData.Width - 1 - x;
                    levelData.rows[y].cells[oppositeX] = levelData.rows[y].cells[x];
                }
            }

            EditorUtility.SetDirty(levelData);
        }

        private void EnsurePaletteSize()
        {
            int size = Mathf.Max(levelData.colorCount, 1);
            Color[] oldPalette = levelData.colorPalette;
            if (oldPalette != null && oldPalette.Length == size) return;

            levelData.colorPalette = new Color[size];
            for (int i = 0; i < size; i++)
            {
                if (oldPalette != null && i < oldPalette.Length)
                {
                    levelData.colorPalette[i] = oldPalette[i];
                }
                else
                {
                    levelData.colorPalette[i] = Color.HSVToRGB(i / (float)size, 0.85f, 1f);
                }
            }

            activeColorId = Mathf.Clamp(activeColorId, 0, size - 1);
        }

        private void SaveLevel()
        {
            EditorUtility.SetDirty(levelData);
            AssetDatabase.SaveAssets();
        }

        private static IEnumerable<string> GetTrayLabels(TrayData[] trays)
        {
            foreach (TrayData tray in trays)
            {
                yield return $"C{tray.colorId}";
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
