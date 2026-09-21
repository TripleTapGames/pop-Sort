using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PopSort
{
    /// <summary>
    /// Positions the tap hand over the suggested holder and displays the conveyor warning
    /// using the authored FTUE panel art.
    /// </summary>
    public class FirstTapFtueOverlay : MonoBehaviour
    {
        [SerializeField] private Vector2 handOffset = new Vector2(0f, 60f);
        [SerializeField] private Vector2 conveyorWarningOffset = new Vector2(0f, 190f);

        private RectTransform canvasRect;
        private RectTransform handRect;
        private RectTransform firstBubbleRect;
        private RectTransform secondBubbleRect;
        private Transform conveyorMask;
        private RectTransform warningBubbleRect;
        private RectTransform warningInputBlockerRect;
        private TMP_Text instructionText;
        private Transform target;
        private Camera targetCamera;
        private float animationTime;
        private bool isConveyorWarningVisible;

        public bool Show(Camera camera, Transform target, string instruction)
        {
            if (camera == null || target == null)
            {
                Hide();
                return false;
            }

            this.target = target;
            targetCamera = camera;
            animationTime = 0f;
            isConveyorWarningVisible = false;
            CacheAuthoredPanel();
            if (canvasRect == null || handRect == null || instructionText == null)
            {
                Debug.LogError("FTUE panel requires Panel/TapHand and Panel/InstructionBubble1/Text.", this);
                Hide();
                return false;
            }

            instructionText.text = instruction;
            handRect.gameObject.SetActive(true);
            if (conveyorMask != null) conveyorMask.gameObject.SetActive(false);
            firstBubbleRect?.gameObject.SetActive(true);
            secondBubbleRect?.gameObject.SetActive(true);
            if (warningBubbleRect != null) warningBubbleRect.gameObject.SetActive(false);
            if (warningInputBlockerRect != null) warningInputBlockerRect.gameObject.SetActive(false);
            gameObject.SetActive(true);
            UpdatePositions();
            return true;
        }

        public bool ShowConveyorWarning(Camera camera, Transform funnelExitPoint)
        {
            if (camera == null || funnelExitPoint == null)
            {
                Hide();
                return false;
            }

            CacheAuthoredPanel();
            if (canvasRect == null || conveyorMask == null || !CreateWarningBubble())
            {
                Debug.LogError("Conveyor FTUE requires Panel/Mask and Panel/InstructionBubble2 with an Image and Text.", this);
                Hide();
                return false;
            }

            target = null;
            isConveyorWarningVisible = true;
            if (handRect != null) handRect.gameObject.SetActive(false);
            firstBubbleRect?.gameObject.SetActive(false);
            secondBubbleRect.gameObject.SetActive(false);
            conveyorMask.gameObject.SetActive(true);
            warningBubbleRect.gameObject.SetActive(true);
            warningInputBlockerRect.gameObject.SetActive(true);
            gameObject.SetActive(true);

            Vector2 screenPosition = camera.WorldToScreenPoint(funnelExitPoint.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPosition);
            warningBubbleRect.anchoredPosition = localPosition + conveyorWarningOffset;
            return true;
        }

        public void Hide()
        {
            if (this == null) return;

            target = null;
            isConveyorWarningVisible = false;
            if (conveyorMask != null) conveyorMask.gameObject.SetActive(false);
            if (warningBubbleRect != null) warningBubbleRect.gameObject.SetActive(false);
            if (warningInputBlockerRect != null) warningInputBlockerRect.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isConveyorWarningVisible) return;

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
            if (conveyorMask == null) conveyorMask = transform.Find("Panel/Mask");
            if (firstBubbleRect == null) firstBubbleRect = transform.Find("Panel/InstructionBubble1") as RectTransform;
            if (secondBubbleRect == null) secondBubbleRect = transform.Find("Panel/InstructionBubble2") as RectTransform;
            if (instructionText == null)
            {
                Transform textTransform = transform.Find("Panel/InstructionBubble1/Text");
                if (textTransform != null) instructionText = textTransform.GetComponent<TMP_Text>();
            }
        }

        private bool CreateWarningBubble()
        {
            if (warningBubbleRect != null) return true;
            if (secondBubbleRect == null) return false;

            Image templateImage = secondBubbleRect.GetComponent<Image>();
            TMP_Text templateText = secondBubbleRect.GetComponentInChildren<TMP_Text>(true);
            if (templateImage == null || templateText == null) return false;

            GameObject warningBubble = new GameObject(
                "ConveyorWarningBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            warningBubbleRect = warningBubble.GetComponent<RectTransform>();
            warningBubbleRect.SetParent(secondBubbleRect.parent, false);
            warningBubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            warningBubbleRect.anchorMax = warningBubbleRect.anchorMin;
            warningBubbleRect.pivot = secondBubbleRect.pivot;
            warningBubbleRect.sizeDelta = secondBubbleRect.sizeDelta;

            Image warningImage = warningBubble.GetComponent<Image>();
            warningImage.sprite = templateImage.sprite;
            warningImage.preserveAspect = templateImage.preserveAspect;
            warningImage.color = new Color(0.08f, 0.22f, 0.48f, 1f);
            warningImage.raycastTarget = false;

            Outline outline = warningBubble.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.77f, 1f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            TMP_Text warningText = Instantiate(templateText, warningBubbleRect);
            warningText.gameObject.name = "Text";
            warningText.text = "Be careful! If the conveyor fills up fully, you lose the level!";
            warningText.color = Color.white;
            warningText.richText = true;
            warningText.fontSizeMax = 45f;
            warningBubble.SetActive(false);

            GameObject inputBlocker = new GameObject(
                "ConveyorWarningInputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            warningInputBlockerRect = inputBlocker.GetComponent<RectTransform>();
            warningInputBlockerRect.SetParent(secondBubbleRect.parent, false);
            warningInputBlockerRect.anchorMin = Vector2.zero;
            warningInputBlockerRect.anchorMax = Vector2.one;
            warningInputBlockerRect.offsetMin = Vector2.zero;
            warningInputBlockerRect.offsetMax = Vector2.zero;
            Image blockerImage = inputBlocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.01f);
            blockerImage.raycastTarget = true;
            inputBlocker.SetActive(false);
            return true;
        }

        private void UpdatePositions()
        {
            if (canvasRect == null || handRect == null || targetCamera == null) return;

            Vector2 screenPosition = targetCamera.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 targetPosition);

            float tapProgress = Mathf.Sin(animationTime * Mathf.PI * 1.5f) * 0.5f + 0.5f;
            handRect.anchoredPosition = targetPosition + handOffset + Vector2.down * (tapProgress * 26f);
            handRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, tapProgress);
        }
    }
}
