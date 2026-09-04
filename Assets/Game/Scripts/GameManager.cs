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
        [SerializeField] private TapInputManager tapInputManager;
        [SerializeField] private LevelData[] levelSequence;
        [SerializeField] private int levelNumber = 1;

        public GameState State { get; private set; } = GameState.Playing;
        public LevelData CurrentLevel { get; private set; }

        private bool gridCleared;

        private void Start()
        {
            LoadLevel(levelNumber - 1);
        }

        private void OnEnable()
        {
            if (gridManager != null) gridManager.OnGridCleared += HandleGridCleared;
            if (beltQueueManager != null) beltQueueManager.OnOverflow += HandleOverflow;
        }

        private void OnDisable()
        {
            if (gridManager != null) gridManager.OnGridCleared -= HandleGridCleared;
            if (beltQueueManager != null) beltQueueManager.OnOverflow -= HandleOverflow;
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            // Win only once every popped ball has also been resolved into a tray.
            if (gridCleared && beltQueueManager != null && beltQueueManager.QueueCount == 0 &&
                trayManager != null && trayManager.AreAllTraysComplete())
            {
                State = GameState.Won;
                if (tapInputManager != null) tapInputManager.enabled = false;
                beltQueueManager.SetBeltMoving(false);
                Debug.Log("You Win!");
            }
        }

        private void HandleGridCleared()
        {
            gridCleared = true;
        }

        private void HandleOverflow()
        {
            if (State != GameState.Playing) return;

            State = GameState.Lost;
            if (tapInputManager != null) tapInputManager.enabled = false;
            beltQueueManager?.SetBeltMoving(false);
            Debug.Log("Game Over");
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(20f, 20f, 240f, 40f), $"Level {levelNumber}", GUI.skin.GetStyle("label"));
            if (State == GameState.Playing) return;

            float width = 300f;
            float height = 160f;
            Rect popup = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(popup, State == GameState.Won ? "Level Complete" : "Level Failed");

            Rect button = new Rect(popup.x + 70f, popup.y + 90f, 160f, 36f);
            if (State == GameState.Won)
            {
                if (GUI.Button(button, "Next")) LoadNextLevel();
            }
            else if (GUI.Button(button, "Retry"))
            {
                LoadLevel(levelNumber - 1);
            }
        }

        private void LoadNextLevel()
        {
            int nextIndex = levelNumber;
            if (levelSequence == null || nextIndex < 0 || nextIndex >= levelSequence.Length || levelSequence[nextIndex] == null)
            {
                Debug.Log("No next level is configured.", this);
                return;
            }

            LoadLevel(nextIndex);
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
            gridCleared = false;
            CurrentLevel = nextLevel;
            if (tapInputManager != null) tapInputManager.enabled = false;
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
