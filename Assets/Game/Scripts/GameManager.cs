using UnityEngine;

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
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private GameWin gameWinPanel;
        [SerializeField] private GameLoose gameLoosePanel;

        public GameState State { get; private set; } = GameState.Playing;
        public LevelData CurrentLevel { get; private set; }

        private void Start()
        {
            LoadLevel(levelNumber - 1);
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
            // Releases balls in transient states (e.g. still falling) that no subsystem list tracks.
            ballPool?.ReleaseAll();
            beltQueueManager?.ClearQueue();
            gridManager?.ClearGrid();
            trayManager?.LoadLevelData(nextLevel);
            beltQueueManager?.SetLevelData(nextLevel);
            gridManager?.SpawnGridFromLevelData(nextLevel);
            levelNumber = sequenceIndex + 1;
            if (tapInputManager != null) tapInputManager.enabled = true;
        }
    }
}
