using UnityEngine;
using TMPro;

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
        [Tooltip("Resume the last reached level when the game is launched.")]
        [SerializeField] private bool resumeSavedProgress = true;
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private GameWin gameWinPanel;
        [SerializeField] private GameLoose gameLoosePanel;

        [SerializeField] private TextMeshProUGUI txt_levelNo;


        public GameState State { get; private set; } = GameState.Playing;
        public LevelData CurrentLevel { get; private set; }

        private const string SavedLevelIndexKey = "PopSort.SavedLevelIndex";
        private int configuredStartingLevelIndex;

        private void Awake()
        {
            configuredStartingLevelIndex = levelNumber - 1;
        }

        private void Start()
        {
            LoadLevel(GetStartingLevelIndex());
        }

        private void OnEnable()
        {
            if (beltQueueManager != null) beltQueueManager.OnOverflow += HandleOverflow;
        }

        private void OnDisable()
        {
            if (beltQueueManager != null) beltQueueManager.OnOverflow -= HandleOverflow;
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            // Win as soon as every tray is filled.
            if (trayManager != null && trayManager.AreAllTraysComplete())
            {
                State = GameState.Won;
                SaveNextLevelProgress();
                if (tapInputManager != null) tapInputManager.enabled = false;
                beltQueueManager?.SetBeltMoving(false);
                gameWinPanel?.Show(LoadNextLevel);
            }
        }

        private void HandleOverflow()
        {
            if (State != GameState.Playing) return;

            State = GameState.Lost;
            if (tapInputManager != null) tapInputManager.enabled = false;
            beltQueueManager?.SetBeltMoving(false);
            gameLoosePanel?.Show(() => LoadLevel(levelNumber - 1));
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(20f, 20f, 240f, 40f), $"Level {levelNumber}", GUI.skin.GetStyle("label"));
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
            beltQueueManager?.SetLevelData(nextLevel);
            gridManager?.SpawnGridFromLevelData(nextLevel);
            levelNumber = sequenceIndex + 1;
            SaveProgress(sequenceIndex);
            if (tapInputManager != null) tapInputManager.enabled = true;
        }

        /// <summary>
        /// Clears the saved level and returns to the level configured in the inspector.
        /// This can be connected to a reset-progress button.
        /// </summary>
        public void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(SavedLevelIndexKey);
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
