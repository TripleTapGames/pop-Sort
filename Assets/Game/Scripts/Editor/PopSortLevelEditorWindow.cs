using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PopSort.EditorTools
{
    public class PopSortLevelEditorWindow : EditorWindow
    {
        private const int CellSize = 24;
        private const string DefaultLevelFolder = "Assets/Game/Data/PopsortLevels";
        private const string DescendingLevelFolder = "Assets/Game/Data/BubbleBreakerLevels";

        private LevelData levelData;
        private int gridWidth = 3;
        private int gridHeight = 3;
        private int activeColorId;
        private int selectedPoolColorIndex;
        private int selectedCellX = -1;
        private int selectedCellY = -1;
        private Vector2 scrollPosition;
        [SerializeField] private bool descendingGridWorkflow;

        [MenuItem("Tools/PopSort/Level Editor")]
        public static void Open()
        {
            PopSortLevelEditorWindow window = GetWindow<PopSortLevelEditorWindow>();
            window.SetWorkflow(false);
        }

        [MenuItem("Tools/PopSort/Descending Grid Level Editor")]
        public static void OpenDescendingGridEditor()
        {
            PopSortLevelEditorWindow window = GetWindow<PopSortLevelEditorWindow>();
            window.SetWorkflow(true);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(descendingGridWorkflow
                ? "Descending Grid Editor"
                : "PopSort Level Editor");
        }

        private void SetWorkflow(bool useDescendingGridWorkflow)
        {
            descendingGridWorkflow = useDescendingGridWorkflow;
            titleContent = new GUIContent(useDescendingGridWorkflow
                ? "Descending Grid Editor"
                : "PopSort Level Editor");
            Show();
            Repaint();
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

            if (descendingGridWorkflow && levelData.gameMode != LevelGameMode.DescendingGrid)
            {
                EditorGUILayout.HelpBox(
                    "This asset is a Classic level. Create a new Descending Grid level or deliberately change its Game Mode in the standard Level Editor.",
                    MessageType.Warning);
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
            EditorGUI.BeginChangeCheck();
            LevelData selectedLevel = (LevelData)EditorGUILayout.ObjectField("Level Data", levelData, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck())
            {
                levelData = selectedLevel;
                if (levelData != null)
                {
                    gridWidth = Mathf.Max(1, levelData.Width);
                    gridHeight = Mathf.Max(1, levelData.Height);
                    selectedCellX = -1;
                    selectedCellY = -1;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(descendingGridWorkflow ? "Create New Descending Level" : "Create New Level"))
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

            if (descendingGridWorkflow)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.EnumPopup("Game Mode", levelData.gameMode);
                }

                EditorGUILayout.HelpBox(
                    "Each successful holder tap moves every remaining holder down by one grid row. Reaching the scene's Danger Line ends the level after the configured delay.",
                    MessageType.Info);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                LevelGameMode newGameMode = (LevelGameMode)EditorGUILayout.EnumPopup("Game Mode", levelData.gameMode);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(levelData, "Change Level Game Mode");
                    levelData.gameMode = newGameMode;
                    EditorUtility.SetDirty(levelData);
                }
            }

            EditorGUI.BeginChangeCheck();
            LevelDifficulty newDifficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", levelData.difficulty);
            bool difficultyChanged = EditorGUI.EndChangeCheck();
            levelData.difficulty = newDifficulty;

            levelData.beltSpeed = EditorGUILayout.FloatField("Belt Speed", levelData.beltSpeed);
            levelData.colorCount = Mathf.Max(0, EditorGUILayout.IntField("Color Count", levelData.colorCount));
            levelData.beltSlotCount = Mathf.Max(1, EditorGUILayout.IntField("Belt Slot Count", levelData.beltSlotCount));
            levelData.slotsPerTray = Mathf.Max(1, EditorGUILayout.IntField("Slots Per Tray", levelData.slotsPerTray));
            levelData.maxTrayColumnCount = Mathf.Clamp(
                EditorGUILayout.IntField("Max Tray Columns", levelData.maxTrayColumnCount), 1, 3);

            DrawAllowedStackSizesSection();

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

        private void DrawAllowedStackSizesSection()
        {
            EditorGUILayout.LabelField("Allowed Stack Sizes", EditorStyles.boldLabel);

            if (levelData.allowedStackSizes == null)
            {
                EditorGUILayout.LabelField(
                    $"Using difficulty default: 1-{GetMaxBallCount(levelData.difficulty)}. Change a toggle to customize.",
                    EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int size = LevelData.MinStackSize; size <= LevelData.MaxStackSize; size++)
                {
                    bool isAllowed = IsStackSizeAllowedForEditor(size);
                    bool newIsAllowed = GUILayout.Toggle(isAllowed, size.ToString(), GUILayout.Width(34f));
                    if (newIsAllowed == isAllowed) continue;

                    Undo.RecordObject(levelData, "Edit Allowed Stack Sizes");
                    EnsureAllowedStackSizeSelection();
                    levelData.allowedStackSizes[size - LevelData.MinStackSize] = newIsAllowed;
                    EditorUtility.SetDirty(levelData);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All"))
                {
                    SetAllAllowedStackSizes(true);
                }

                if (GUILayout.Button("Clear"))
                {
                    SetAllAllowedStackSizes(false);
                }

                if (GUILayout.Button("Use Difficulty Default"))
                {
                    Undo.RecordObject(levelData, "Use Difficulty Stack Size Default");
                    levelData.allowedStackSizes = null;
                    EditorUtility.SetDirty(levelData);
                }
            }

            if (levelData.allowedStackSizes != null && GetConfiguredAllowedStackSizes().Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Select at least one Allowed Stack Size before generating a level.",
                    MessageType.Warning);
            }
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            ColorConfigPool colorPool = FindColorConfigPool();
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

            using (new EditorGUI.DisabledScope(colorPool == null || levelData.colorCount == 0))
            {
                if (GUILayout.Button("Update Art Assets From Pool"))
                {
                    UpdateArtAssetsFromPool(colorPool);
                }
            }

            EnsurePaletteSize();
            EditorGUILayout.LabelField("Asset order: ball, tray, tray cover, tappable holder, blocked holder, pressed holder", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < levelData.colorCount; i++)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(84f)))
                    {
                        levelData.popAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.popAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        levelData.trayAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.trayAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        levelData.trayCoverAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.trayCoverAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        levelData.holderAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.holderAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        levelData.blockAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.blockAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        levelData.pressedAssets[i] = (Sprite)EditorGUILayout.ObjectField(
                            levelData.pressedAssets[i], typeof(Sprite), false, GUILayout.Width(60f));
                        if (GUILayout.Toggle(activeColorId == i, $"Color {i}", "Button", GUILayout.Width(80f)))
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
            EnsurePaletteSize();
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
            if (config == null) return;

            Undo.RecordObject(levelData, "Add Color To Level Palette");
            // Append at the next free local slot; the pool entry's id is unrelated to this level's slot index.
            int newColorId = levelData.colorCount;
            levelData.colorCount = newColorId + 1;
            EnsurePaletteSize();
            levelData.popAssets[newColorId] = config.popBalls;
            levelData.trayAssets[newColorId] = config.trayAsset;
            levelData.trayCoverAssets[newColorId] = config.trayCoverAsset;
            levelData.holderAssets[newColorId] = config.popHolder;
            levelData.blockAssets[newColorId] = config.blockAsset;
            levelData.pressedAssets[newColorId] = config.pressedAsset;
            levelData.colorConfigIds[newColorId] = config.id;
            activeColorId = newColorId;
            EditorUtility.SetDirty(levelData);
        }

        private void UpdateArtAssetsFromPool(ColorConfigPool colorPool)
        {
            if (colorPool == null) return;

            Undo.RecordObject(levelData, "Update Level Art Assets From Pool");
            EnsurePaletteSize();
            int updatedCount = 0;
            int missingCount = 0;

            for (int colorId = 0; colorId < levelData.colorCount; colorId++)
            {
                int configId = levelData.colorConfigIds[colorId];
                if (!colorPool.TryGet(configId, out ColorConfig config))
                {
                    missingCount++;
                    continue;
                }

                levelData.popAssets[colorId] = config.popBalls;
                levelData.trayAssets[colorId] = config.trayAsset;
                levelData.trayCoverAssets[colorId] = config.trayCoverAsset;
                levelData.holderAssets[colorId] = config.popHolder;
                levelData.blockAssets[colorId] = config.blockAsset;
                levelData.pressedAssets[colorId] = config.pressedAsset;
                updatedCount++;
            }

            EditorUtility.SetDirty(levelData);
            AssetDatabase.SaveAssets();
            string message = $"Updated {updatedCount} color slot(s) from {colorPool.name}.";
            if (missingCount > 0) message += $" No matching pool entry was found for {missingCount} slot(s).";
            EditorUtility.DisplayDialog("Art Assets Updated", message, "OK");
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

            gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Columns", gridWidth));
            gridHeight = Mathf.Max(1, EditorGUILayout.IntField("Rows", gridHeight));
            EditorGUILayout.LabelField(
                "Choose any positive row and column count. Cells may be painted or cleared in any pattern.",
                EditorStyles.miniLabel);

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

                if (descendingGridWorkflow && GUILayout.Button("Pack To Top"))
                {
                    PackGridToTop();
                }
            }

            if (levelData.rows == null || levelData.rows.Length == 0)
            {
                ResizeGrid(gridWidth, gridHeight);
            }

            DrawBallCountSection();

            if (descendingGridWorkflow)
            {
                EditorGUILayout.LabelField(
                    "Row 0 spawns at Grid Origin. Each following row spawns above it. Left-click paints; right-click clears.",
                    EditorStyles.miniLabel);
            }

            for (int displayRow = 0; displayRow < levelData.Height; displayRow++)
            {
                // Descending-grid assets are drawn like their world layout: the
                // highest indexed row at the top and origin row 0 at the bottom.
                bool descendingLayout = levelData.gameMode == LevelGameMode.DescendingGrid;
                int y = descendingLayout ? levelData.Height - 1 - displayRow : displayRow;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (descendingGridWorkflow)
                    {
                        GUILayout.Label($"R{y}", EditorStyles.miniLabel, GUILayout.Width(24f));
                    }

                    if (descendingLayout && (y & 1) == 1)
                    {
                        GUILayout.Space((CellSize + 4f) * 0.5f);
                    }

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
            Sprite cellSprite = cell.enabled ? levelData.GetPopAsset(cell.colorId) : null;

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = cellSprite != null
                ? Color.white
                : cell.enabled && levelData.colorPalette != null && levelData.colorPalette.Length > 0
                    ? levelData.GetColor(Mathf.Clamp(cell.colorId, 0, levelData.colorPalette.Length - 1))
                    : Color.gray;

            Rect rect = GUILayoutUtility.GetRect(CellSize, CellSize, GUILayout.Width(CellSize), GUILayout.Height(CellSize));
            string label = cell.enabled
                ? Mathf.Max(1, cell.ballCount) > 1 ? $"{cell.colorId} x{Mathf.Max(1, cell.ballCount)}" : cell.colorId.ToString()
                : "";
            if (GUI.Button(rect, cellSprite == null ? new GUIContent(label) : GUIContent.none))
            {
                Undo.RecordObject(levelData, "Paint Grid Cell");
                cell.enabled = true;
                cell.colorId = activeColorId;
                cell.ballCount = Mathf.Max(1, cell.ballCount);
                levelData.rows[y].cells[x] = cell;
                selectedCellX = x;
                selectedCellY = y;
                EditorUtility.SetDirty(levelData);
            }

            if (cellSprite != null)
            {
                DrawSpritePreview(rect, cellSprite);
                GUI.Label(rect, label, EditorStyles.centeredGreyMiniLabel);
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

        // Draws the sprite's actual source rect so packed/atlased sprites render correctly.
        private static void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Rect texCoords = new Rect(
                sprite.rect.x / sprite.texture.width,
                sprite.rect.y / sprite.texture.height,
                sprite.rect.width / sprite.texture.width,
                sprite.rect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords);
        }

        private void DrawBallCountSection()
        {
            if (selectedCellX < 0 || selectedCellY < 0 || levelData.rows == null ||
                selectedCellY >= levelData.Height || selectedCellX >= levelData.Width)
            {
                return;
            }

            GridCell cell = levelData.GetCell(selectedCellX, selectedCellY);
            if (!cell.enabled) return;

            EditorGUI.BeginChangeCheck();
            int ballCount = Mathf.Max(1, EditorGUILayout.IntField("Ball Count", Mathf.Max(1, cell.ballCount)));
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(levelData, "Edit Grid Cell Ball Count");
            cell.ballCount = ballCount;
            levelData.rows[selectedCellY].cells[selectedCellX] = cell;
            EditorUtility.SetDirty(levelData);
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
            string levelFolder = descendingGridWorkflow ? DescendingLevelFolder : DefaultLevelFolder;
            EnsureFolder(levelFolder);

            LevelData newLevel = CreateInstance<LevelData>();
            newLevel.gameMode = descendingGridWorkflow ? LevelGameMode.DescendingGrid : LevelGameMode.Classic;
            string fileName = descendingGridWorkflow ? "Descending_Level_New.asset" : "Level_New.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{levelFolder}/{fileName}");
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
            if (GetAllowedStackSizesForGeneration().Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Allowed Stack Sizes",
                    "Select at least one Allowed Stack Size before generating a level.",
                    "OK");
                return;
            }

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
                    break;
                case LevelDifficulty.Medium:
                    parameters.maxCanonicalConveyorPressure = 0.4f;
                    parameters.forcedReliefThreshold = 0.3f;
                    parameters.usefulBallUnlockDepth = 2;
                    parameters.colourRepetition = 0.65f;
                    parameters.verticalColourClustering = 0.65f;
                    break;
                case LevelDifficulty.Hard:
                    parameters.maxCanonicalConveyorPressure = 0.6f;
                    parameters.forcedReliefThreshold = 0.5f;
                    parameters.usefulBallUnlockDepth = 3;
                    parameters.colourRepetition = 0.35f;
                    parameters.verticalColourClustering = 0.35f;
                    break;
                case LevelDifficulty.SuperHard:
                    parameters.maxCanonicalConveyorPressure = 0.75f;
                    parameters.forcedReliefThreshold = 0.65f;
                    parameters.usefulBallUnlockDepth = 4;
                    parameters.colourRepetition = 0.1f;
                    parameters.verticalColourClustering = 0.1f;
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

            if (levelData.gameMode == LevelGameMode.DescendingGrid)
            {
                GenerateDescendingPattern();
                levelData.GenerateTrayColumnsFromGrid();
                EditorUtility.SetDirty(levelData);
                return;
            }

            int totalCells = gridWidth * gridHeight;
            int colorCount = Mathf.Max(levelData.colorCount, 1);
            int trayCapacity = Mathf.Max(levelData.slotsPerTray, 1);
            int usableCellCount = totalCells - (totalCells % trayCapacity);

            List<int> ballColors = new List<int>();
            List<int> ballCounts = new List<int>();
            int groupCount = usableCellCount / trayCapacity;
            List<int> allowedStackSizes = GetAllowedStackSizesForGeneration();
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                int colorId = Random.Range(0, colorCount);
                int groupBallCount = allowedStackSizes[Random.Range(0, allowedStackSizes.Count)];
                for (int slotIndex = 0; slotIndex < trayCapacity; slotIndex++)
                {
                    ballColors.Add(colorId);
                    ballCounts.Add(groupBallCount);
                }
            }

            for (int i = ballColors.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ballColors[i], ballColors[j]) = (ballColors[j], ballColors[i]);
                (ballCounts[i], ballCounts[j]) = (ballCounts[j], ballCounts[i]);
            }

            List<GridCell> generatedCells = new List<GridCell>(totalCells);
            for (int i = 0; i < ballColors.Count; i++)
            {
                generatedCells.Add(new GridCell { enabled = true, colorId = ballColors[i], ballCount = ballCounts[i] });
            }

            while (generatedCells.Count < totalCells)
            {
                generatedCells.Add(new GridCell());
            }

            for (int i = generatedCells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (generatedCells[i], generatedCells[j]) = (generatedCells[j], generatedCells[i]);
            }

            int ballIndex = 0;
            for (int y = 0; y < levelData.Height; y++)
            {
                for (int x = 0; x < levelData.Width; x++)
                {
                    levelData.rows[y].cells[x] = generatedCells[ballIndex++];
                }
            }

            levelData.GenerateTrayColumnsFromGrid();
            EditorUtility.SetDirty(levelData);
        }

        private void GenerateDescendingPattern()
        {
            // Grow a random connected shape from the support row. Empty cells
            // stay empty, while every generated branch has a path to the top.
            ClearGrid();
            int totalCells = gridWidth * gridHeight;
            int targetCount = totalCells == 1 ? 1 : Mathf.Clamp(
                Mathf.RoundToInt(totalCells * Random.Range(0.55f, 0.8f)), 1, totalCells - 1);
            List<int> stackSizes = GetAllowedStackSizesForGeneration();
            List<Vector2Int> frontier = new List<Vector2Int>();
            HashSet<Vector2Int> discovered = new HashSet<Vector2Int>();
            Vector2Int seed = new Vector2Int(Random.Range(0, gridWidth), gridHeight - 1);
            frontier.Add(seed);
            discovered.Add(seed);

            for (int count = 0; count < targetCount && frontier.Count > 0; count++)
            {
                int index = Random.Range(0, frontier.Count);
                Vector2Int cell = frontier[index];
                frontier.RemoveAt(index);
                levelData.rows[cell.y].cells[cell.x] = new GridCell
                {
                    enabled = true,
                    colorId = Random.Range(0, levelData.colorCount),
                    ballCount = stackSizes[Random.Range(0, stackSizes.Count)]
                };

                AddPatternFrontier(cell.x - 1, cell.y, frontier, discovered);
                AddPatternFrontier(cell.x + 1, cell.y, frontier, discovered);
                int diagonalOffset = (cell.y & 1) == 1 ? 1 : -1;
                AddPatternFrontier(cell.x, cell.y - 1, frontier, discovered);
                AddPatternFrontier(cell.x + diagonalOffset, cell.y - 1, frontier, discovered);
                AddPatternFrontier(cell.x, cell.y + 1, frontier, discovered);
                AddPatternFrontier(cell.x + diagonalOffset, cell.y + 1, frontier, discovered);
            }
        }

        private void AddPatternFrontier(int x, int y, List<Vector2Int> frontier, HashSet<Vector2Int> discovered)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return;
            Vector2Int cell = new Vector2Int(x, y);
            if (discovered.Add(cell)) frontier.Add(cell);
        }

        private static int GetMaxBallCount(LevelDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LevelDifficulty.Medium: return 4;
                case LevelDifficulty.Hard: return 5;
                case LevelDifficulty.SuperHard: return 6;
                default: return 3;
            }
        }

        private bool IsStackSizeAllowedForEditor(int size)
        {
            if (levelData.allowedStackSizes == null)
            {
                return size <= GetMaxBallCount(levelData.difficulty);
            }

            int index = size - LevelData.MinStackSize;
            return index >= 0 && index < levelData.allowedStackSizes.Length && levelData.allowedStackSizes[index];
        }

        private void EnsureAllowedStackSizeSelection()
        {
            if (levelData.allowedStackSizes != null && levelData.allowedStackSizes.Length == LevelData.MaxStackSize)
            {
                return;
            }

            bool[] selection = new bool[LevelData.MaxStackSize];
            if (levelData.allowedStackSizes == null)
            {
                int defaultMax = GetMaxBallCount(levelData.difficulty);
                for (int size = LevelData.MinStackSize; size <= defaultMax; size++)
                {
                    selection[size - LevelData.MinStackSize] = true;
                }
            }
            else
            {
                for (int i = 0; i < selection.Length && i < levelData.allowedStackSizes.Length; i++)
                {
                    selection[i] = levelData.allowedStackSizes[i];
                }
            }

            levelData.allowedStackSizes = selection;
        }

        private void SetAllAllowedStackSizes(bool isAllowed)
        {
            Undo.RecordObject(levelData, isAllowed ? "Select All Allowed Stack Sizes" : "Clear Allowed Stack Sizes");
            EnsureAllowedStackSizeSelection();
            for (int i = 0; i < levelData.allowedStackSizes.Length; i++)
            {
                levelData.allowedStackSizes[i] = isAllowed;
            }

            EditorUtility.SetDirty(levelData);
        }

        private List<int> GetAllowedStackSizesForGeneration()
        {
            if (levelData.allowedStackSizes == null)
            {
                List<int> difficultyDefaults = new List<int>();
                for (int size = LevelData.MinStackSize; size <= GetMaxBallCount(levelData.difficulty); size++)
                {
                    difficultyDefaults.Add(size);
                }

                return difficultyDefaults;
            }

            return GetConfiguredAllowedStackSizes();
        }

        private List<int> GetConfiguredAllowedStackSizes()
        {
            List<int> allowedSizes = new List<int>();
            for (int size = LevelData.MinStackSize; size <= LevelData.MaxStackSize; size++)
            {
                int index = size - LevelData.MinStackSize;
                if (index < levelData.allowedStackSizes.Length && levelData.allowedStackSizes[index])
                {
                    allowedSizes.Add(size);
                }
            }

            return allowedSizes;
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
                    levelData.rows[y].cells[x] = new GridCell { enabled = true, colorId = activeColorId, ballCount = 1 };
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

        private void PackGridToTop()
        {
            Undo.RecordObject(levelData, "Pack Descending Grid To Top");
            for (int x = 0; x < levelData.Width; x++)
            {
                List<GridCell> enabledCells = new List<GridCell>();
                for (int y = 0; y < levelData.Height; y++)
                {
                    GridCell cell = levelData.rows[y].cells[x];
                    if (cell.enabled) enabledCells.Add(cell);
                }

                for (int y = 0; y < levelData.Height; y++)
                {
                    levelData.rows[y].cells[x] = y < enabledCells.Count ? enabledCells[y] : new GridCell();
                }
            }

            EditorUtility.SetDirty(levelData);
        }

        private void EnsurePaletteSize()
        {
            int size = Mathf.Max(levelData.colorCount, 0);
            Color[] oldPalette = levelData.colorPalette;
            bool paletteNeedsResize = oldPalette == null || oldPalette.Length != size;
            if (paletteNeedsResize)
            {
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
            }

            if (levelData.popAssets == null || levelData.popAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.popAssets;
                levelData.popAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.popAssets[i] = oldAssets[i];
                }
            }

            if (levelData.trayAssets == null || levelData.trayAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.trayAssets;
                levelData.trayAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.trayAssets[i] = oldAssets[i];
                }
            }

            if (levelData.trayCoverAssets == null || levelData.trayCoverAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.trayCoverAssets;
                levelData.trayCoverAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.trayCoverAssets[i] = oldAssets[i];
                }
            }

            if (levelData.holderAssets == null || levelData.holderAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.holderAssets;
                levelData.holderAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.holderAssets[i] = oldAssets[i];
                }
            }

            if (levelData.blockAssets == null || levelData.blockAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.blockAssets;
                levelData.blockAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.blockAssets[i] = oldAssets[i];
                }
            }

            if (levelData.pressedAssets == null || levelData.pressedAssets.Length != size)
            {
                Sprite[] oldAssets = levelData.pressedAssets;
                levelData.pressedAssets = new Sprite[size];
                for (int i = 0; i < size && oldAssets != null && i < oldAssets.Length; i++)
                {
                    levelData.pressedAssets[i] = oldAssets[i];
                }
            }

            if (levelData.colorConfigIds == null || levelData.colorConfigIds.Length != size)
            {
                int[] oldConfigIds = levelData.colorConfigIds;
                levelData.colorConfigIds = new int[size];
                for (int i = 0; i < size; i++)
                {
                    levelData.colorConfigIds[i] = oldConfigIds != null && i < oldConfigIds.Length
                        ? oldConfigIds[i]
                        : i;
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
