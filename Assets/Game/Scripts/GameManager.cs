using UnityEngine;
using TMPro;
using System.Collections.Generic;
using TripleTapSDK;

namespace PopSort
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TrayManager trayManager;
        [SerializeField] private BeltQueueManager beltQueueManager;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private TapInputManager tapInputManager;
        [SerializeField] private LevelData[] levelSequence;
        [Header("Performance")]
        [SerializeField, Range(15, 120)] private int targetFrameRate = 60;
        [Tooltip("Resume the last reached level when the game is launched.")]
        [SerializeField] private bool resumeSavedProgress = true;
        [SerializeField] private int levelNumber = 1;
        [Header("Fast Finish")]
        [SerializeField, Range(1f, 4f)] private float fastFinishTimeScale = 2f;
        [Header("FTUE Panels")]
        [SerializeField] private GameObject ftueLevel1Panel;
        [SerializeField] private GameObject ftueLevel2Panel;
        [SerializeField] private GameWin gameWinPanel;
        [SerializeField] private GameLoose gameLoosePanel;

        [SerializeField] private TextMeshProUGUI txt_levelNo;

        public GameState State { get; private set; } = GameState.Playing;
        public LevelData CurrentLevel { get; private set; }

        private const string SavedLevelIndexKey = "PopSort.SavedLevelIndex";
        private const string FirstPopFtueSeenKey = "PopSort.FirstPopFtueSeen";
        private const string LegacyLevel2FtueSeenKey = "PopSort.Level2FtueSeen";
        private const string Level2ConveyorFtueSeenKey = "PopSort.Level2ConveyorFtueSeen";
        private const int Level2GreenColorId = 2;
        private const int Level2YellowColorId = 3;
        private enum FtueStep { None, Level1Tap, Level2Green, Level2Yellow, Level2WaitingForConveyor, Level2Warning }

        private int configuredStartingLevelIndex;
        private bool isFastFinishActive;
        private FirstTapFtueOverlay level1FtueOverlay;
        private FirstTapFtueOverlay level2FtueOverlay;
        private FtueStep activeFtueStep;
        private int tutorialBallsExpected;
        private int tutorialBallsSeated;
        private bool dismissWarningAtEndOfFrame;

        private void Awake()
        {
            // VSync takes priority over Application.targetFrameRate on desktop platforms.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            RestoreNormalTimeScale();
            configuredStartingLevelIndex = levelNumber - 1;
        }

        private void Start()
        {
            LoadLevel(GetStartingLevelIndex());
        }

        private void OnEnable()
        {
            if (beltQueueManager != null) beltQueueManager.OnOverflow += HandleOverflow;
            if (beltQueueManager != null) beltQueueManager.OnBallSeated += HandleTutorialBallSeated;
            if (gridManager != null) gridManager.OnGridCleared += HandleGridCleared;
            if (tapInputManager != null) tapInputManager.OnPopTapped += HandleFtueTap;
            CreateFtueOverlays();
        }

        private void OnDisable()
        {
            if (beltQueueManager != null) beltQueueManager.OnOverflow -= HandleOverflow;
            if (beltQueueManager != null) beltQueueManager.OnBallSeated -= HandleTutorialBallSeated;
            if (gridManager != null) gridManager.OnGridCleared -= HandleGridCleared;
            if (tapInputManager != null) tapInputManager.OnPopTapped -= HandleFtueTap;
            EndFtue();
            RestoreNormalTimeScale();
        }

        private void OnDestroy()
        {
            RestoreNormalTimeScale();
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            if (activeFtueStep == FtueStep.Level2Warning)
            {
                if (WasTapStartedThisFrame()) DismissConveyorWarning();
                return;
            }

            if (activeFtueStep == FtueStep.Level2WaitingForConveyor)
            {
                if (tutorialBallsSeated >= tutorialBallsExpected) ShowConveyorWarning();
                return;
            }

            TryStartFastFinish();

            // Win as soon as every tray is filled.
            if (trayManager != null && trayManager.AreAllTraysComplete())
            {
                State = GameState.Won;
                EndFtue();
                RestoreNormalTimeScale();
                SfxManager.PlayLevelWon();
                SaveNextLevelProgress();
                TTManager.Instance?.GAService?.LogProgressionComplete($"level_{levelNumber}");
                TTManager.Instance?.TTAnalyticsService?.LogMilestoneLevelCompleted(levelNumber);
                if (tapInputManager != null) tapInputManager.enabled = false;
                beltQueueManager?.SetBeltMoving(false);
                gameWinPanel?.Show(LoadNextLevel);
            }
        }

        private void LateUpdate()
        {
            if (!dismissWarningAtEndOfFrame) return;

            dismissWarningAtEndOfFrame = false;
            PlayerPrefs.SetInt(Level2ConveyorFtueSeenKey, 1);
            PlayerPrefs.Save();
            if (level2FtueOverlay != null) level2FtueOverlay.Hide();
            activeFtueStep = FtueStep.None;
            tutorialBallsExpected = 0;
            tutorialBallsSeated = 0;
            RestoreNormalTimeScale();
            beltQueueManager?.SetBeltMoving(true);
            gridManager?.SetHolderTapsBlocked(false);
        }

        private void HandleOverflow()
        {
            if (State != GameState.Playing) return;

            State = GameState.Lost;
            EndFtue();
            RestoreNormalTimeScale();
            SfxManager.PlayLevelFailed();
            TTManager.Instance?.GAService?.LogProgressionFail($"level_{levelNumber}");
            if (tapInputManager != null) tapInputManager.enabled = false;
            beltQueueManager?.SetBeltMoving(false);
            gameLoosePanel?.Show(() => LoadLevel(levelNumber - 1));
        }

        private void LoadNextLevel()
        {
            if (levelSequence == null || levelSequence.Length == 0)
            {
                Debug.Log("No levels are configured.", this);
                return;
            }

            // Wrap back to the first level once the sequence ends, skipping any empty slots.
            for (int offset = 0; offset < levelSequence.Length; offset++)
            {
                int candidateIndex = (levelNumber + offset) % levelSequence.Length;
                if (levelSequence[candidateIndex] != null)
                {
                    LoadLevel(candidateIndex);
                    return;
                }
            }

            Debug.LogError("Level sequence has no valid levels.", this);
        }

        private void LoadLevel(int sequenceIndex)
        {
            RestoreNormalTimeScale();
            if (levelSequence == null || sequenceIndex < 0 || sequenceIndex >= levelSequence.Length)
            {
                Debug.LogError($"Cannot load level sequence index {sequenceIndex}.", this);
                return;
            }

            LevelData nextLevel = levelSequence[sequenceIndex];
            if (nextLevel == null)
            {
                Debug.LogError($"Level sequence index {sequenceIndex} is empty.", this);
                return;
            }

            EndFtue();
            State = GameState.Playing;
            CurrentLevel = nextLevel;
            gameWinPanel?.Hide();
            gameLoosePanel?.Hide();
            if (tapInputManager != null) tapInputManager.enabled = false;
            beltQueueManager?.ClearQueue();
            gridManager?.ClearGrid();
            trayManager?.LoadLevelData(nextLevel);
            // Releases balls in transient states (e.g. still falling) that no subsystem list tracks.
            ballPool?.ReleaseAll();
            ballPool?.Prewarm(nextLevel.TotalBallCount());
            beltQueueManager?.SetLevelData(nextLevel);
            gridManager?.SpawnGridFromLevelData(nextLevel);
            levelNumber = sequenceIndex + 1;
            UpdateLevelNumberLabel();
            SaveProgress(sequenceIndex);
            TTManager.Instance?.GAService?.LogProgressionStart($"level_{levelNumber}");
            if (tapInputManager != null) tapInputManager.enabled = true;
            ShowFtueIfNeeded();
        }

        private void HandleFtueTap()
        {
            switch (activeFtueStep)
            {
                case FtueStep.Level1Tap:
                    PlayerPrefs.SetInt(FirstPopFtueSeenKey, 1);
                    PlayerPrefs.Save();
                    EndFtue();
                    break;
                case FtueStep.Level2Green:
                    BeginFtueStep(FtueStep.Level2Yellow);
                    break;
                case FtueStep.Level2Yellow:
                    WaitForConveyorWarning();
                    break;
            }
        }

        private void HandleTutorialBallSeated(Ball ball)
        {
            if (ball == null || (ball.ColorId != Level2GreenColorId && ball.ColorId != Level2YellowColorId)) return;

            if (activeFtueStep == FtueStep.Level2Green || activeFtueStep == FtueStep.Level2Yellow ||
                activeFtueStep == FtueStep.Level2WaitingForConveyor)
            {
                tutorialBallsSeated++;
            }
        }

        private void WaitForConveyorWarning()
        {
            gridManager?.HideFirstTapFtue();
            gridManager?.SetHolderTapsBlocked(true);
            if (level2FtueOverlay != null) level2FtueOverlay.Hide();
            activeFtueStep = FtueStep.Level2WaitingForConveyor;
        }

        private void ShowConveyorWarning()
        {
            Camera camera = tapInputManager != null ? tapInputManager.MainCamera : null;
            Transform funnelExitPoint = beltQueueManager != null ? beltQueueManager.FunnelExitPoint : null;
            if (level2FtueOverlay == null || !level2FtueOverlay.ShowConveyorWarning(camera, funnelExitPoint))
            {
                Debug.LogError("Cannot show the level 2 conveyor FTUE warning.", this);
                EndFtue();
                return;
            }

            activeFtueStep = FtueStep.Level2Warning;
            beltQueueManager?.SetBeltMoving(false);
            Time.timeScale = 0f;
        }

        private void DismissConveyorWarning()
        {
            dismissWarningAtEndOfFrame = true;
        }

        private static bool WasTapStartedThisFrame()
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            return Input.GetMouseButtonDown(0);
        }

        private void CreateFtueOverlays()
        {
            level1FtueOverlay = GetFtueOverlay(ftueLevel1Panel, "FtueLevel1Panel");
            level2FtueOverlay = GetFtueOverlay(ftueLevel2Panel, "FtueLevel2Panel");
        }

        private FirstTapFtueOverlay GetFtueOverlay(GameObject panel, string panelName)
        {
            if (panel == null)
            {
                Debug.LogError($"GameManager requires the {panelName} scene object.", this);
                return null;
            }

            FirstTapFtueOverlay overlay = panel.GetComponent<FirstTapFtueOverlay>();
            if (overlay == null) overlay = panel.AddComponent<FirstTapFtueOverlay>();
            overlay.Hide();
            return overlay;
        }

        private void ShowFtueIfNeeded()
        {
            if (levelNumber == 1 && !PlayerPrefs.HasKey(FirstPopFtueSeenKey))
            {
                BeginFtueStep(FtueStep.Level1Tap);
            }
            else if (levelNumber == 2 && !PlayerPrefs.HasKey(Level2ConveyorFtueSeenKey))
            {
                if (CurrentLevel == null || CurrentLevel.Width < 2 || CurrentLevel.Height < 1)
                {
                    Debug.LogError("Level 2 FTUE requires a grid with a bottom-left and bottom-right holder.", this);
                    EndFtue();
                    return;
                }

                int bottomRow = CurrentLevel.Height - 1;
                tutorialBallsExpected = Mathf.Max(1, CurrentLevel.GetCell(0, bottomRow).ballCount) +
                    Mathf.Max(1, CurrentLevel.GetCell(CurrentLevel.Width - 1, bottomRow).ballCount);
                tutorialBallsSeated = 0;
                BeginFtueStep(FtueStep.Level2Green);
            }
        }

        private void BeginFtueStep(FtueStep step)
        {
            Transform target = null;
            string instruction = null;
            Camera camera = tapInputManager != null ? tapInputManager.MainCamera : null;
            FirstTapFtueOverlay overlay = step == FtueStep.Level1Tap ? level1FtueOverlay : level2FtueOverlay;

            if (gridManager != null && CurrentLevel != null)
            {
                switch (step)
                {
                    case FtueStep.Level1Tap:
                        target = gridManager.GetFirstTappablePopHolderTransform();
                        instruction = "Tap a ball to pop it!";
                        break;
                    case FtueStep.Level2Green:
                        target = gridManager.GetTappablePopHolderTransformAt(0, CurrentLevel.Height - 1, Level2GreenColorId);
                        instruction = "Tap the green ball!";
                        break;
                    case FtueStep.Level2Yellow:
                        target = gridManager.GetTappablePopHolderTransformAt(
                            CurrentLevel.Width - 1, CurrentLevel.Height - 1, Level2YellowColorId);
                        instruction = "Tap the yellow ball!";
                        break;
                }
            }

            if (target == null || camera == null || overlay == null ||
                !gridManager.SetFtueTarget(target) || !overlay.Show(camera, target, instruction))
            {
                Debug.LogError($"Cannot start FTUE step {step}: its target, camera, or panel is unavailable.", this);
                EndFtue();
                return;
            }

            activeFtueStep = step;
        }

        private void EndFtue()
        {
            if (activeFtueStep == FtueStep.Level2Warning)
            {
                RestoreNormalTimeScale();
                if (State == GameState.Playing && beltQueueManager != null) beltQueueManager.SetBeltMoving(true);
            }

            activeFtueStep = FtueStep.None;
            tutorialBallsExpected = 0;
            tutorialBallsSeated = 0;
            dismissWarningAtEndOfFrame = false;
            if (gridManager != null)
            {
                gridManager.HideFirstTapFtue();
                gridManager.SetHolderTapsBlocked(false);
            }
            if (level1FtueOverlay != null) level1FtueOverlay.Hide();
            if (level2FtueOverlay != null) level2FtueOverlay.Hide();
        }

        private void HandleGridCleared()
        {
            TryStartFastFinish();
        }

        private void TryStartFastFinish()
        {
            if (isFastFinishActive || State != GameState.Playing || gridManager == null ||
                !gridManager.AreAllHoldersPopped || gridManager.HasUnspawnedBalls ||
                trayManager == null || ballPool == null || beltQueueManager == null ||
                !beltQueueManager.AreAllQueuedBallsSeated || !beltQueueManager.HasCollectableQueuedBall)
            {
                return;
            }

            Dictionary<int, int> inTransitBallCounts = new Dictionary<int, int>();
            foreach (Ball ball in ballPool.ActiveBalls)
            {
                if (ball == null) continue;
                if (ball.State == BallState.InTray || ball.State == BallState.TrayLanding) continue;
                if (ball.State != BallState.Queued) return;

                inTransitBallCounts.TryGetValue(ball.ColorId, out int currentCount);
                inTransitBallCounts[ball.ColorId] = currentCount + 1;
            }

            if (!trayManager.CanResolveRemainingBalls(inTransitBallCounts)) return;

            isFastFinishActive = true;
            Time.timeScale = fastFinishTimeScale;
        }

        private void RestoreNormalTimeScale()
        {
            Time.timeScale = 1f;
            isFastFinishActive = false;
        }

        private void UpdateLevelNumberLabel()
        {
            if (txt_levelNo != null) txt_levelNo.text = $"Level {levelNumber}";
        }

        /// <summary>
        /// Clears the saved level and returns to the level configured in the inspector.
        /// This can be connected to a reset-progress button.
        /// </summary>
        public void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(SavedLevelIndexKey);
            PlayerPrefs.DeleteKey(FirstPopFtueSeenKey);
            PlayerPrefs.DeleteKey(LegacyLevel2FtueSeenKey);
            PlayerPrefs.DeleteKey(Level2ConveyorFtueSeenKey);
            PlayerPrefs.Save();
            LoadLevel(GetConfiguredStartingLevelIndex());
        }

        private int GetStartingLevelIndex()
        {
            int configuredIndex = GetConfiguredStartingLevelIndex();
            if (!resumeSavedProgress || !PlayerPrefs.HasKey(SavedLevelIndexKey)) return configuredIndex;

            int savedIndex = PlayerPrefs.GetInt(SavedLevelIndexKey);
            return IsValidLevelIndex(savedIndex) ? savedIndex : configuredIndex;
        }

        private int GetConfiguredStartingLevelIndex()
        {
            if (levelSequence == null || levelSequence.Length == 0) return 0;

            int requestedIndex = Mathf.Clamp(configuredStartingLevelIndex, 0, levelSequence.Length - 1);
            if (IsValidLevelIndex(requestedIndex)) return requestedIndex;

            for (int index = 0; index < levelSequence.Length; index++)
            {
                if (IsValidLevelIndex(index)) return index;
            }

            return requestedIndex;
        }

        private bool IsValidLevelIndex(int index)
        {
            return levelSequence != null && index >= 0 && index < levelSequence.Length && levelSequence[index] != null;
        }

        private void SaveNextLevelProgress()
        {
            if (levelSequence == null || levelSequence.Length == 0) return;

            for (int offset = 1; offset <= levelSequence.Length; offset++)
            {
                int candidateIndex = ((levelNumber - 1) + offset) % levelSequence.Length;
                if (IsValidLevelIndex(candidateIndex))
                {
                    SaveProgress(candidateIndex);
                    return;
                }
            }
        }

        private void SaveProgress(int sequenceIndex)
        {
            if (!resumeSavedProgress || !IsValidLevelIndex(sequenceIndex)) return;

            PlayerPrefs.SetInt(SavedLevelIndexKey, sequenceIndex);
            PlayerPrefs.Save();
        }
    }
}
