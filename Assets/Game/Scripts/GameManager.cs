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
        [SerializeField] private BeltQueueManager beltQueueManager;
        [SerializeField] private TapInputManager tapInputManager;

        public GameState State { get; private set; } = GameState.Playing;

        private bool gridCleared;

        private void OnEnable()
        {
            gridManager.OnGridCleared += HandleGridCleared;
            beltQueueManager.OnOverflow += HandleOverflow;
        }

        private void OnDisable()
        {
            gridManager.OnGridCleared -= HandleGridCleared;
            beltQueueManager.OnOverflow -= HandleOverflow;
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            // Win only once every popped ball has also been resolved into a tray.
            if (gridCleared && beltQueueManager.QueueCount == 0)
            {
                State = GameState.Won;
                tapInputManager.enabled = false;
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
            tapInputManager.enabled = false;
            Debug.Log("Game Over");
        }
    }
}
