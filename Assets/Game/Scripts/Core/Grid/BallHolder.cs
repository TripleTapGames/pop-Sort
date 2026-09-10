using System.Collections;
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

        [Header("Release Feedback")]
        [SerializeField, Min(0f)] private float releasePunchDuration = 0.1f;
        [SerializeField, Range(0f, 0.5f)] private float releasePunchStrength = 0.08f;

        private SpriteRenderer popHolderRenderer;
        private SpriteRenderer blocksRenderer;
        private SpriteRenderer pressedEffectRenderer;
        private bool isPressed;
        private Vector3 baseLocalScale;
        private Coroutine releaseFeedbackRoutine;

        public float ReleaseFeedbackDuration => releasePunchDuration > 0f && releasePunchStrength > 0f
            ? releasePunchDuration
            : 0f;

        private void Awake()
        {
            baseLocalScale = transform.localScale;
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);
            CacheStateRenderers();
        }

        public void Configure(Sprite popHolderAsset, Sprite blockAsset, Sprite pressedAsset, int ballCount)
        {
            if (countLabel == null) countLabel = GetComponentInChildren<TMP_Text>(true);

            if (releaseFeedbackRoutine != null) StopCoroutine(releaseFeedbackRoutine);
            releaseFeedbackRoutine = null;
            transform.localScale = baseLocalScale;
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

        // A quick squash-and-stretch makes each released marble feel like it pushes
        // through the holder, without needing an Animator on every holder prefab.
        public void PlayReleaseFeedback()
        {
            if (releaseFeedbackRoutine != null) StopCoroutine(releaseFeedbackRoutine);
            releaseFeedbackRoutine = StartCoroutine(ReleaseFeedbackRoutine());
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

        private IEnumerator ReleaseFeedbackRoutine()
        {
            if (releasePunchDuration <= 0f || releasePunchStrength <= 0f)
            {
                releaseFeedbackRoutine = null;
                yield break;
            }

            Vector3 squashedScale = Vector3.Scale(baseLocalScale,
                new Vector3(1f + releasePunchStrength, 1f - releasePunchStrength, 1f));
            Vector3 stretchedScale = Vector3.Scale(baseLocalScale,
                new Vector3(1f - releasePunchStrength * 0.5f, 1f + releasePunchStrength * 0.5f, 1f));

            yield return ScaleOverTime(baseLocalScale, squashedScale, releasePunchDuration * 0.25f);
            yield return ScaleOverTime(squashedScale, stretchedScale, releasePunchDuration * 0.35f);
            yield return ScaleOverTime(stretchedScale, baseLocalScale, releasePunchDuration * 0.4f);

            transform.localScale = baseLocalScale;
            releaseFeedbackRoutine = null;
        }

        private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                transform.localScale = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }
    }
}
