using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace PopSort
{
    [System.Serializable]
    public struct TrayLandingSettings
    {
        [Min(0f)] public float duration;
        public AnimationCurve dropProgressCurve;
        [Range(0f, 1f)] public float horizontalDrift;
        [Min(0f)] public float impactDuration;
        [Range(0f, 0.5f)] public float impactSquash;

        public float TotalDuration => Mathf.Max(duration, 0f) + Mathf.Max(impactDuration, 0f);

        public static TrayLandingSettings CreateDefault()
        {
            return new TrayLandingSettings
            {
                duration = 0.28f,
                dropProgressCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 2f, 2f)),
                horizontalDrift = 0.12f,
                impactDuration = 0.12f,
                impactSquash = 0.12f
            };
        }

        public float EvaluateDropProgress(float time)
        {
            return dropProgressCurve != null ? Mathf.Clamp01(dropProgressCurve.Evaluate(time)) : time * time;
        }
    }

    public class TraySlot : MonoBehaviour
    {
        [SerializeField] private int colorId;
        [SerializeField] private Sprite traySprite;
        [SerializeField] private int capacity = 3;
        [SerializeField] private BallPool ballPool;

        [SerializeField] private SpriteRenderer trayVisual;

        [SerializeField] private Transform trayRoot;


        [SerializeField] private Transform[] slotPositions; // manually placed in the editor, one per capacity slot

        [Header("Generated Slot Fallback")]
        [Tooltip("Vertical spacing for extra rows when tray capacity exceeds the authored Slot transforms.")]
        [SerializeField, Min(0f)] private float generatedSlotRowSpacing = 0.48f;

        [Header("Tray Animation Events")]
        [SerializeField] private UnityEvent onTrayFilled = new UnityEvent();
        [SerializeField] private UnityEvent onTrayEntering = new UnityEvent();

        public int ColorId => colorId;
        public bool HasSpace => placedBalls.Count < capacity;
        public bool IsComplete => hasReachedCapacity;
        public float LastIntakeMoveTime { get; private set; }

        private readonly List<Ball> placedBalls = new List<Ball>();
        private bool hasReachedCapacity;
        private bool lastLandingComplete = true;
        private int activeLandingCount;
        private bool hasLoggedMissingSlotPosition;
        private TrayLandingSettings landingSettings;
        private Action<Vector3> onCompletionVfx;

        private void OnValidate()
        {
            // SpriteRenderer trayVisual = GetComponent<SpriteRenderer>();
            if (trayVisual != null && traySprite != null) trayVisual.sprite = traySprite;
        }

        private void Awake()
        {
            ApplyTrayColor();
        }

        public void Configure(
            int colorId,
            int capacity,
            Sprite traySprite,
            BallPool ballPool,
            TrayLandingSettings landingSettings,
            Action<Vector3> completionVfxCallback)
        {
            this.colorId = colorId;
            this.capacity = Mathf.Max(capacity, 1);
            this.traySprite = traySprite;
            this.ballPool = ballPool;
            this.landingSettings = landingSettings;
            onCompletionVfx = completionVfxCallback;
            placedBalls.Clear();
            hasReachedCapacity = false;
            lastLandingComplete = true;
            activeLandingCount = 0;
            ApplyTrayColor();
        }

        // Returns true once this tray has reached capacity.
        public bool Fill(Ball ball)
        {
            ball.BeginTrayLanding();
            Vector3 targetPosition = GetSlotTargetPosition(placedBalls.Count);
            LastIntakeMoveTime = landingSettings.TotalDuration;
            placedBalls.Add(ball);
            activeLandingCount++;
            lastLandingComplete = false;
            StartCoroutine(LandBallInSlot(ball, targetPosition));

            hasReachedCapacity = placedBalls.Count >= capacity;
            return hasReachedCapacity;
        }

        public void ClearBalls()
        {
            StopAllCoroutines();

            foreach (Ball placed in placedBalls)
            {
                ballPool.Release(placed);
            }

            placedBalls.Clear();
            activeLandingCount = 0;
            lastLandingComplete = true;
        }

        public IEnumerator WaitForLastLanding()
        {
            while (!lastLandingComplete)
            {
                yield return null;
            }
        }

        // Called by TrayColumn after the final ball has reached this tray.
        public void InvokeTrayFilled()
        {
            onCompletionVfx?.Invoke(trayRoot != null ? trayRoot.position : transform.position);
            SfxManager.PlayTrayFilled();
            onTrayFilled?.Invoke();
        }

        // Called by TrayColumn when this tray begins sliding into the active position.
        public void InvokeTrayEntering()
        {
            onTrayEntering?.Invoke();
        }

        private IEnumerator LandBallInSlot(Ball ball, Vector3 targetPosition)
        {
            Vector3 startPosition = ball.transform.position;
            float duration = Mathf.Max(landingSettings.duration, 0f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float dropProgress = landingSettings.EvaluateDropProgress(normalizedTime);
                float horizontalProgress = Mathf.SmoothStep(0f, 1f, normalizedTime);
                float drift = (targetPosition.x - startPosition.x) * landingSettings.horizontalDrift *
                    Mathf.Sin(normalizedTime * Mathf.PI);

                ball.transform.position = new Vector3(
                    Mathf.Lerp(startPosition.x, targetPosition.x, horizontalProgress) + drift,
                    Mathf.Lerp(startPosition.y, targetPosition.y, dropProgress),
                    Mathf.Lerp(startPosition.z, targetPosition.z, normalizedTime));
                yield return null;
            }

            ball.transform.position = targetPosition;
            // Keep landed balls visually attached to this tray through its slide,
            // filled, and disappearance animations.
            ball.transform.SetParent(trayRoot != null ? trayRoot : transform, true);
            ball.CaptureVisualBaseScale();
            ball.FinishTrayLanding();
            SfxManager.PlayBallLandedInTray();
            yield return PlayLandingPunch(ball);
            activeLandingCount = Mathf.Max(0, activeLandingCount - 1);
            lastLandingComplete = activeLandingCount == 0;
        }

        private IEnumerator PlayLandingPunch(Ball ball)
        {
            float duration = Mathf.Max(landingSettings.impactDuration, 0f);
            if (duration <= 0f)
            {
                ball.SetVisualScaleMultiplier(Vector2.one);
                yield break;
            }

            Vector2 squash = new Vector2(1f + landingSettings.impactSquash, 1f - landingSettings.impactSquash);
            yield return ScaleBall(ball, Vector2.one, squash, duration * 0.4f);
            yield return ScaleBall(ball, squash, Vector2.one, duration * 0.6f);
        }

        private IEnumerator ScaleBall(Ball ball, Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                ball.SetVisualScaleMultiplier(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ball.SetVisualScaleMultiplier(Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }

            ball.SetVisualScaleMultiplier(to);
        }

        private Vector3 GetSlotTargetPosition(int slotIndex)
        {
            if (slotPositions != null && slotIndex >= 0 && slotIndex < slotPositions.Length && slotPositions[slotIndex] != null)
            {
                return slotPositions[slotIndex].position;
            }

            if (slotPositions == null || slotPositions.Length == 0 || slotPositions[0] == null)
            {
                if (!hasLoggedMissingSlotPosition)
                {
                    Debug.LogError("TraySlot requires at least one assigned Slot transform.", this);
                    hasLoggedMissingSlotPosition = true;
                }

                return transform.position;
            }

            // The prefab currently authors one row of slot positions.  Higher-capacity
            // levels reuse that row as a grid, which keeps every ball visible and avoids
            // a hard dependency on a separate prefab per tray capacity.
            int column = slotIndex % slotPositions.Length;
            int row = slotIndex / slotPositions.Length;
            Transform sourceSlot = slotPositions[column] != null ? slotPositions[column] : slotPositions[0];
            if (row > 0 && !hasLoggedMissingSlotPosition)
            {
                Debug.LogWarning(
                    $"Tray capacity ({capacity}) exceeds authored slot transforms ({slotPositions.Length}). " +
                    "Using generated fallback rows; add more Slot transforms for custom placement.", this);
                hasLoggedMissingSlotPosition = true;
            }

            return sourceSlot.position + Vector3.down * (row * generatedSlotRowSpacing);
        }

        private void ApplyTrayColor()
        {
            // SpriteRenderer trayVisual = GetComponent<SpriteRenderer>();
            if (trayVisual != null && traySprite != null) trayVisual.sprite = traySprite;
        }

    }
}
