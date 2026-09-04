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
        private int selectedPoolColorIndex;
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
            levelData.colorCount = Mathf.Max(0, EditorGUILayout.IntField("Color Count", levelData.colorCount));
            levelData.beltSlotCount = Mathf.Max(1, EditorGUILayout.IntField("Belt Slot Count", levelData.beltSlotCount));
            levelData.slotsPerTray = Mathf.Max(1, EditorGUILayout.IntField("Slots Per Tray", levelData.slotsPerTray));

            if (difficultyChanged)
            {
                Undo.RecordObject(levelData, "Change Level Difficulty");
                levelData.difficultyParameters = GetDifficultyParameters(levelData.difficulty);
                EditorUtility.SetDirty(levelData);
            }

            if (GUILayout.Button("Generate Level"))
            {
                GenerateLevel();
            }

            EnsurePaletteSize();
            EditorUtility.SetDirty(levelData);
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            ColorConfigPool colorPool = levelData.colorConfigPool != null
                ? levelData.colorConfigPool
                : FindColorConfigPool();
            if (colorPool != null)
            {
                string[] colorNames = GetColorNames(colorPool);
                int newSelection = EditorGUILayout.Popup("Add Color", selectedPoolColorIndex, colorNames);
                if (newSelection != selectedPoolColorIndex)
                {
                    selectedPoolColorIndex = newSelection;
                    if (selectedPoolColorIndex > 0)
                    {
                        AddColorFromPool(colorPool, selectedPoolColorIndex - 1);
                    }
                    selectedPoolColorIndex = 0;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Create a Color Config Pool to add named colors.", MessageType.Info);
            }

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

            using (new EditorGUI.DisabledScope(levelData.colorCount == 0))
            {
                if (GUILayout.Button("Remove Last Color"))
                {
                    RemoveLastColor();
                }
            }
        }

        private void RemoveLastColor()
        {
            int colorId = levelData.colorCount - 1;
            if (IsColorUsed(colorId))
            {
                EditorUtility.DisplayDialog(
                    "Color In Use",
                    $"Color {colorId} is used by one or more grid cells. Clear those cells before removing it.",
                    "OK");
                return;
            }

            Undo.RecordObject(levelData, "Remove Color From Level Palette");
            levelData.colorCount = Mathf.Max(0, colorId);
            Color[] resizedPalette = new Color[levelData.colorCount];
            if (levelData.colorPalette != null)
            {
                for (int i = 0; i < resizedPalette.Length && i < levelData.colorPalette.Length; i++)
                {
                    resizedPalette[i] = levelData.colorPalette[i];
                }
            }

            levelData.colorPalette = resizedPalette;
            activeColorId = Mathf.Clamp(activeColorId, 0, levelData.colorCount - 1);
            EditorUtility.SetDirty(levelData);
        }

        private bool IsColorUsed(int colorId)
        {
            if (levelData.rows == null) return false;

            foreach (GridRow row in levelData.rows)
            {
                if (row?.cells == null) continue;
                foreach (GridCell cell in row.cells)
                {
                    if (cell.enabled && cell.colorId == colorId) return true;
                }
            }

            return false;
        }

        private void AddColorFromPool(ColorConfigPool colorPool, int colorIndex)
        {
            if (colorPool.colors == null || colorIndex < 0 || colorIndex >= colorPool.colors.Count) return;

            ColorConfig config = colorPool.colors[colorIndex];
            if (config == null || config.id < 0) return;

            Undo.RecordObject(levelData, "Add Color To Level Palette");
            levelData.colorConfigPool = colorPool;
            levelData.colorCount = Mathf.Max(levelData.colorCount, config.id + 1);
            EnsurePaletteSize();
            levelData.colorPalette[config.id] = config.color;
            activeColorId = config.id;
            EditorUtility.SetDirty(levelData);
        }

        private static string[] GetColorNames(ColorConfigPool colorPool)
        {
            if (colorPool.colors == null) return new string[0];

            string[] names = new string[colorPool.colors.Count + 1];
            names[0] = "Select a color...";
            for (int i = 0; i < colorPool.colors.Count; i++)
            {
                ColorConfig config = colorPool.colors[i];
                names[i + 1] = config == null
                    ? "Missing Color"
                    : $"{config.id}: {config.displayName}";
            }

            return names;
        }

        private static ColorConfigPool FindColorConfigPool()
        {
            string[] guids = AssetDatabase.FindAssets("t:ColorConfigPool");
            if (guids.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ColorConfigPool>(path);
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
            GUI.backgroundColor = cell.enabled && levelData.colorPalette != null && levelData.colorPalette.Length > 0
                ? levelData.GetColor(Mathf.Clamp(cell.colorId, 0, levelData.colorPalette.Length - 1))
                : Color.gray;

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
            levelData.difficultyParameters = GetDifficultyParameters(levelData.difficulty);
            GenerateLevel();
            SaveLevel();
        }

        private void GenerateLevel()
        {
            Undo.RecordObject(levelData, "Generate Level");
            levelData.difficultyParameters = GetDifficultyParameters(levelData.difficulty);
            GenerateRandomLevel();
        }

        private DifficultyParameters GetDifficultyParameters(LevelDifficulty difficulty)
        {
            DifficultyParameters parameters = new DifficultyParameters();
            switch (difficulty)
            {
                case LevelDifficulty.Easy:
                    parameters.maxCanonicalConveyorPressure = 0.25f;
                    parameters.forcedReliefThreshold = 0.15f;
                    parameters.usefulBallUnlockDepth = 1;
                    parameters.colourRepetition = 1f;
                    parameters.verticalColourClustering = 1f;
                    parameters.preferredTrayColumnCount = 4;
                    break;
                case LevelDifficulty.Medium:
                    parameters.maxCanonicalConveyorPressure = 0.4f;
                    parameters.forcedReliefThreshold = 0.3f;
                    parameters.usefulBallUnlockDepth = 2;
                    parameters.colourRepetition = 0.65f;
                    parameters.verticalColourClustering = 0.65f;
                    parameters.preferredTrayColumnCount = 3;
                    break;
                case LevelDifficulty.Hard:
                    parameters.maxCanonicalConveyorPressure = 0.6f;
                    parameters.forcedReliefThreshold = 0.5f;
                    parameters.usefulBallUnlockDepth = 3;
                    parameters.colourRepetition = 0.35f;
                    parameters.verticalColourClustering = 0.35f;
                    parameters.preferredTrayColumnCount = 2;
                    break;
                case LevelDifficulty.SuperHard:
                    parameters.maxCanonicalConveyorPressure = 0.75f;
                    parameters.forcedReliefThreshold = 0.65f;
                    parameters.usefulBallUnlockDepth = 4;
                    parameters.colourRepetition = 0.1f;
                    parameters.verticalColourClustering = 0.1f;
                    parameters.preferredTrayColumnCount = 2;
                    break;
            }

            return parameters;
        }

        private void GenerateRandomLevel()
        {
            Undo.RecordObject(levelData, "Generate Random Level");
            ResizeGrid(gridWidth, gridHeight);

            if (levelData.colorCount == 0)
            {
                ClearGrid();
                levelData.trayColumns = null;
                EditorUtility.SetDirty(levelData);
                return;
            }

            int trayCapacity = Mathf.Max(levelData.slotsPerTray, 1);
            int totalCells = gridWidth * gridHeight;
            int colorCount = Mathf.Max(levelData.colorCount, 1);

            List<int> ballColors = new List<int>();
            for (int colorBallIndex = 0; colorBallIndex < totalCells; colorBallIndex++)
            {
                ballColors.Add((colorBallIndex / trayCapacity) % colorCount);
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

            levelData.GenerateTrayColumnsFromGrid(levelData.difficultyParameters.preferredTrayColumnCount);
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
            int size = Mathf.Max(levelData.colorCount, 0);
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

            activeColorId = size == 0 ? 0 : Mathf.Clamp(activeColorId, 0, size - 1);
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
