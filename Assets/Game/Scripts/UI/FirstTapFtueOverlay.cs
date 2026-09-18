using UnityEngine;

namespace PopSort
{
    /// <summary>
    /// Drives the authored FtuePanel in the scene. The panel owns its art and text;
    /// this component only follows the suggested holder and animates TapHand.
    /// </summary>
    public class FirstTapFtueOverlay : MonoBehaviour
    {
        private RectTransform canvasRect;
        private RectTransform handRect;
        private Transform target;
        private Camera targetCamera;
        private float animationTime;

        public void Show(Camera camera, Transform target)
        {
            if (camera == null || target == null)
            {
                Hide();
                return;
            }

            this.target = target;
            targetCamera = camera;
            animationTime = 0f;
            CacheAuthoredPanel();
            if (canvasRect == null || handRect == null)
            {
                Debug.LogError("FtuePanel requires a Panel/TapHand RectTransform.", this);
                Hide();
                return;
            }

            gameObject.SetActive(true);
            UpdatePositions();
        }

        public void Hide()
        {
            target = null;
            if (gameObject != null) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (target == null)
            {
                Hide();
                return;
            }

            animationTime += Time.unscaledDeltaTime;
            UpdatePositions();
        }

        private void CacheAuthoredPanel()
        {
            canvasRect = gameObject.GetComponent<RectTransform>();
            if (handRect == null) handRect = transform.Find("Panel/TapHand") as RectTransform;
        }

        private void UpdatePositions()
        {
            if (canvasRect == null || handRect == null || targetCamera == null) return;

            Vector2 screenPosition = targetCamera.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 targetPosition);

            float tapProgress = Mathf.Sin(animationTime * Mathf.PI * 1.5f) * 0.5f + 0.5f;
            handRect.anchoredPosition = targetPosition + Vector2.down * (tapProgress * 26f);
            handRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, tapProgress);
        }
    }
}
