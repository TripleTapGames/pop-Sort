using UnityEngine;
using System;

namespace PopSort
{
    public class TapInputManager : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridManager gridManager;

        public event Action OnPopTapped;
        public Camera MainCamera => mainCamera;

        private void Update()
        {
            Vector2? tapWorldPos = GetTapWorldPosition();
            if (tapWorldPos == null) return;

            if (gridManager != null && gridManager.TryPopHolderAtWorldPosition(tapWorldPos.Value))
            {
                OnPopTapped?.Invoke();
            }
        }

        private Vector2? GetTapWorldPosition()
        {
            if (Input.touchCount > 0)
            {
                if (Input.GetTouch(0).phase != TouchPhase.Began) return null;
                return mainCamera.ScreenToWorldPoint(Input.GetTouch(0).position);
            }

            if (Input.GetMouseButtonDown(0))
            {
                return mainCamera.ScreenToWorldPoint(Input.mousePosition);
            }

            return null;
        }
    }
}
