using TMPro;
using UnityEngine;

namespace PopSort
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BallHolder : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer holderRenderer;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private bool showCountForSingleBall;

        private Sprite popHolderSprite;
        private Sprite blockSprite;
        private Sprite pressedSprite;

        private void Awake()
        {
            if (holderRenderer == null) holderRenderer = GetComponent<SpriteRenderer>();
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);
        }

        public void Configure(Sprite popHolder, Sprite blockAsset, Sprite pressedAsset, int ballCount)
        {
            if (holderRenderer == null) holderRenderer = GetComponent<SpriteRenderer>();
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);

            popHolderSprite = popHolder;
            blockSprite = blockAsset;
            pressedSprite = pressedAsset;
            SetCount(ballCount);
        }

        public void SetTappable(bool tappable)
        {
            if (holderRenderer != null)
            {
                holderRenderer.sprite = tappable ? popHolderSprite : blockSprite;
            }
        }

        public void SetCount(int count)
        {
            if (countLabel == null) return;

            countLabel.text = Mathf.Max(count, 0).ToString();
            countLabel.gameObject.SetActive(showCountForSingleBall || count > 1);
        }

        public void SetPressed()
        {
            if (holderRenderer != null && pressedSprite != null)
            {
                holderRenderer.sprite = pressedSprite;
            }
        }
    }
}
