using UnityEngine;

namespace PopSort
{
    [RequireComponent(typeof(Collider2D))]
    public class ConveyorBelt : MonoBehaviour
    {
        [SerializeField] private BeltQueueManager beltQueueManager;
        [SerializeField] private SpriteRenderer beltGlow;
        [SerializeField, Min(0f)] private float glowFadeSpeed = 5f;

        private Color beltGlowColor;
        private float currentGlowAlpha;

        private void Awake()
        {
            if (beltGlow == null) return;

            beltGlowColor = beltGlow.color;
            SetGlowAlpha(0f);
        }

        private void Update()
        {
            if (beltGlow == null || beltQueueManager == null) return;

            float targetGlowAlpha = GetTargetGlowAlpha(beltQueueManager.BeltItemCount);
            currentGlowAlpha = Mathf.MoveTowards(currentGlowAlpha, targetGlowAlpha, glowFadeSpeed * Time.deltaTime);
            SetGlowAlpha(currentGlowAlpha);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Ball ball = collision.collider.GetComponent<Ball>();
            if (ball != null && ball.State == BallState.Falling)
            {
                beltQueueManager.HandleBallLanded(ball);
            }
        }

        private static float GetTargetGlowAlpha(int beltItemCount)
        {
            if (beltItemCount >= 19) return 1f;
            if (beltItemCount >= 17) return 0.7f;
            return 0f;
        }

        private void SetGlowAlpha(float alpha)
        {
            beltGlow.color = new Color(
                beltGlowColor.r,
                beltGlowColor.g,
                beltGlowColor.b,
                alpha);
        }
    }
}
