using TMPro;
using UnityEngine;

namespace PopSort
{
    public class BallHolder : MonoBehaviour
    {
        [SerializeField] private GameObject popHolderSprite;
        [SerializeField] private GameObject blocks;
        [SerializeField] private GameObject pressedEffect;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private bool showCountForSingleBall;

        private SpriteRenderer popHolderRenderer;
        private SpriteRenderer blocksRenderer;
        private SpriteRenderer pressedEffectRenderer;
        private bool isPressed;

        private void Awake()
        {
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);
            CacheStateRenderers();
        }

        public void Configure(Sprite popHolderAsset, Sprite blockAsset, Sprite pressedAsset, int ballCount)
        {
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);

            isPressed = false;

            CacheStateRenderers();
            if (popHolderRenderer != null) popHolderRenderer.sprite = popHolderAsset;
            if (blocksRenderer != null) blocksRenderer.sprite = blockAsset;
            if (pressedEffectRenderer != null) pressedEffectRenderer.sprite = pressedAsset;

            SetStateObjects(true, false, false);
            SetCount(ballCount);
        }

        public void SetTappable(bool tappable)
        {
            if (isPressed) return;

            SetStateObjects(tappable, !tappable, false);
        }

        public void SetCount(int count)
        {
            if (countLabel == null) return;

            countLabel.text = Mathf.Max(count, 0).ToString();
            countLabel.gameObject.SetActive(showCountForSingleBall || count > 1);
        }

        public void SetPressed()
        {
            isPressed = true;

            SetStateObjects(false, false, true);
        }

        private void CacheStateRenderers()
        {
            if (popHolderRenderer == null && popHolderSprite != null)
            {
                popHolderRenderer = popHolderSprite.GetComponent<SpriteRenderer>();
            }

            if (blocksRenderer == null && blocks != null)
            {
                blocksRenderer = blocks.GetComponent<SpriteRenderer>();
            }

            if (pressedEffectRenderer == null && pressedEffect != null)
            {
                pressedEffectRenderer = pressedEffect.GetComponent<SpriteRenderer>();
            }
        }

        private void SetStateObjects(bool showPopHolder, bool showBlocks, bool showPressedEffect)
        {
            if (popHolderSprite != null) popHolderSprite.SetActive(showPopHolder);
            if (blocks != null) blocks.SetActive(showBlocks);
            if (pressedEffect != null) pressedEffect.SetActive(showPressedEffect);
        }
    }
}
