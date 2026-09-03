using UnityEngine;

namespace PopSort
{
    public class TapInputManager : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        private void Update()
        {
            Vector2? tapWorldPos = GetTapWorldPosition();
            if (tapWorldPos == null) return;

            Collider2D hit = Physics2D.OverlapPoint(tapWorldPos.Value);
            if (hit == null) return;

            Ball ball = hit.GetComponent<Ball>();
            if (ball != null && ball.State == BallState.InGrid)
            {
                ball.Pop();
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
