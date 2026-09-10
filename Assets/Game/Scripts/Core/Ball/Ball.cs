using System;
using UnityEngine;
using TMPro;

namespace PopSort
{
    public enum BallState
    {
        InGrid,
        Falling,
        FunnelWaiting,
        Queued,
        TrayLanding,
        InTray
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public class Ball : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private SpriteRenderer highlight;

        [Header("Marble Flight")]
        [SerializeField] private float gravityScale = 1.5f;
        [SerializeField, Range(0f, 1f)] private float bounceRetention = 0.22f;
        [SerializeField, Min(0f)] private float minimumBounceSpeed = 0.35f;

        [Header("Belt Visual Polish")]
        [SerializeField, Min(0f)] private float queuedWobbleAngle = 1.5f;
        [SerializeField, Min(0f)] private float queuedWobbleSpeed = 7f;

        [Header("Spawn Bounce")]
        [SerializeField, Min(0f)] private float spawnBounceDuration = 0.18f;
        [SerializeField, Range(0f, 0.5f)] private float spawnBounceStrength = 0.08f;

        [Header("Pop Rendering")]
        [SerializeField] private int poppedSortingOrder = 3;

        public int ColorId { get; private set; }
        public BallState State { get; private set; }
        public object Group { get; private set; }

        // Reused for logical tap-hit and stacking checks that avoid Physics2D queries.
        public float Radius => col != null ? col.radius * transform.lossyScale.y : 0.1f;

        private Rigidbody2D rb;
        private CircleCollider2D col;
        private SpriteRenderer sr;
        private Canvas worldCanvas;
        private Action<Ball> onPopped;
        private Action<Ball> onGroupPopRequested;

        private Vector3 prefabLocalScale;
        private Vector3 visualBaseLocalScale;
        private int prefabSpriteSortingOrder;
        private int prefabCanvasSortingOrder;
        private int prefabHighlightSortingOrder;
        private float queueWobblePhase;
        private Coroutine scaleFeedbackRoutine;

        public void PopBurstFromState(Vector2 velocity, float angularVelocity)
        {
            transform.SetParent(null);
            PlayPopSfx();
            BringToFront();
            State = BallState.Falling;
            col.isTrigger = false;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            rb.velocity = velocity;
            rb.angularVelocity = angularVelocity;
            onPopped?.Invoke(this);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            sr = GetComponent<SpriteRenderer>();
            worldCanvas = GetComponentInChildren<Canvas>();
            prefabLocalScale = transform.localScale;
            visualBaseLocalScale = prefabLocalScale;
            prefabSpriteSortingOrder = sr.sortingOrder;
            prefabCanvasSortingOrder = worldCanvas != null ? worldCanvas.sortingOrder : 0;
            prefabHighlightSortingOrder = highlight != null ? highlight.sortingOrder : 0;
        }

        private void Update()
        {
            if (State != BallState.Queued) return;

            float angle = Mathf.Sin(Time.time * queuedWobbleSpeed + queueWobblePhase) * queuedWobbleAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Called each time this instance is (re)used from the pool for a fresh grid spawn.
        public void Initialize(int colorId, Sprite sprite, Action<Ball> poppedCallback)
        {
            RestorePrefabScale();
            RestoreSortingOrders();
            ColorId = colorId;
            if (sprite != null)
            {
                sr.sprite = sprite;
                SetHighlightSprite(sprite);
            }
            sr.color = Color.white;
            onPopped = poppedCallback;
            Group = null;
            onGroupPopRequested = null;
            SetCount(1);
            State = BallState.InGrid;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero; // clear leftover motion from a previous pooled life (e.g. reused mid-flight on retry)
            rb.angularVelocity = 0f;
            rb.simulated = true; // kinematic + simulated keeps the collider visible to Physics2D queries (tap detection)
            col.enabled = true;
            col.isTrigger = true;
            PlayScalePunch(spawnBounceDuration, spawnBounceStrength);
        }

        public void ConfigureGroup(object group, Action<Ball> groupPopRequested)
        {
            Group = group;
            onGroupPopRequested = groupPopRequested;
        }

        public void SetCount(int count)
        {
            if (countLabel == null) return;

            countLabel.text = count.ToString();
            countLabel.gameObject.SetActive(count > 1);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;

            sr.sprite = sprite;
            SetHighlightSprite(sprite);
        }

        public void Pop()
        {
            if (State != BallState.InGrid) return;

            if (Group != null)
            {
                onGroupPopRequested?.Invoke(this);
                return;
            }

            if (HasBallBelow(false)) return;

            PlayPopSfx();
            BringToFront();
            State = BallState.Falling;
            col.isTrigger = false;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            onPopped?.Invoke(this);
        }

        public void PopBurst(Vector2 velocity, float angularVelocity)
        {
            if (State != BallState.InGrid) return;

            transform.SetParent(null);
            PlayPopSfx();
            BringToFront();
            State = BallState.Falling;
            col.isTrigger = false;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            rb.velocity = velocity;
            rb.angularVelocity = angularVelocity;
            onPopped?.Invoke(this);
        }

        public bool HasBallBelow(bool ignoreGroupMembers)
        {
            float radius = col != null ? col.radius * transform.lossyScale.y : 0.1f;
            Vector2 origin = (Vector2)transform.position + Vector2.down * (radius + 0.01f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, Mathf.Infinity);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider == col) continue;

                Ball ballBelow = hit.collider.GetComponent<Ball>();
                if (ballBelow == null || ballBelow.State != BallState.InGrid) continue;
                if (ignoreGroupMembers && Group != null && ReferenceEquals(ballBelow.Group, Group)) continue;
                return true;
            }

            return false;
        }

        private void PlayPopSfx()
        {
            SfxManager.PlayBallPop();
        }

        // Stops physics once queue movement takes over so belt positioning is exact.
        public void SetQueued()
        {
            State = BallState.Queued;
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            col.isTrigger = true;
            queueWobblePhase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        // Call after changing parent when visual feedback must preserve world scale.
        public void CaptureVisualBaseScale()
        {
            visualBaseLocalScale = transform.localScale;
        }

        public void PlayLaunchPunch(float duration, float strength)
        {
            PlayScalePunch(duration, strength);
        }

        public void PlayBeltSettle(float duration, float strength)
        {
            PlayScalePunch(duration, strength);
        }

        public void SetFunnelKinematic()
        {
            State = BallState.FunnelWaiting;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            col.isTrigger = false;
        }

        // The ball currently leaving the physical funnel pile.  It must not collide
        // with the remaining dynamic balls while being guided to the belt exit.
        public void SetFunnelExtracting()
        {
            State = BallState.FunnelWaiting;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            col.isTrigger = true;
        }

        public void SetFunnelDynamic()
        {
            State = BallState.FunnelWaiting;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            col.isTrigger = false;
        }

        public void SetInTray()
        {
            FinishTrayLanding();
        }

        // Removes the ball from belt/physics control while TraySlot guides it into place.
        public void BeginTrayLanding()
        {
            transform.SetParent(null, true);
            CaptureVisualBaseScale();
            State = BallState.TrayLanding;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            col.enabled = false;
        }

        public void FinishTrayLanding()
        {
            State = BallState.InTray;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            col.enabled = false;
            RestoreVisualScale();
        }

        public void SetVisualScaleMultiplier(Vector2 multiplier)
        {
            transform.localScale = new Vector3(
                visualBaseLocalScale.x * multiplier.x,
                visualBaseLocalScale.y * multiplier.y,
                visualBaseLocalScale.z);
        }

        public void PrepareForPool()
        {
            StopAllCoroutines();
            scaleFeedbackRoutine = null;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            col.enabled = false;
            RestorePrefabScale();
            RestoreSortingOrders();
        }

        private void BringToFront()
        {
            if (sr != null) sr.sortingOrder = Mathf.Max(sr.sortingOrder, poppedSortingOrder);
            if (worldCanvas != null) worldCanvas.sortingOrder = Mathf.Max(worldCanvas.sortingOrder, poppedSortingOrder);
            if (highlight != null)
            {
                highlight.sortingLayerID = sr.sortingLayerID;
                highlight.sortingOrder = sr.sortingOrder + 1;
            }
        }

        private void RestoreSortingOrders()
        {
            if (sr != null) sr.sortingOrder = prefabSpriteSortingOrder;
            if (worldCanvas != null) worldCanvas.sortingOrder = prefabCanvasSortingOrder;
            if (highlight != null) highlight.sortingOrder = prefabHighlightSortingOrder;
        }

        private void SetHighlightSprite(Sprite sprite)
        {
            if (highlight == null) return;

            highlight.sprite = sprite;
            highlight.sortingLayerID = sr.sortingLayerID;
            highlight.sortingOrder = sr.sortingOrder + 1;
        }

        private void RestorePrefabScale()
        {
            transform.localScale = prefabLocalScale;
            visualBaseLocalScale = prefabLocalScale;
        }

        private void RestoreVisualScale()
        {
            transform.localScale = visualBaseLocalScale;
        }

        private void PlayScalePunch(float duration, float strength)
        {
            if (scaleFeedbackRoutine != null) StopCoroutine(scaleFeedbackRoutine);
            scaleFeedbackRoutine = StartCoroutine(ScalePunchRoutine(duration, strength));
        }

        private System.Collections.IEnumerator ScalePunchRoutine(float duration, float strength)
        {
            if (duration <= 0f || strength <= 0f)
            {
                RestoreVisualScale();
                scaleFeedbackRoutine = null;
                yield break;
            }

            Vector2 squash = new Vector2(1f + strength, 1f - strength);
            Vector2 stretch = new Vector2(1f - strength * 0.5f, 1f + strength * 0.5f);
            float elapsed = 0f;
            float squashDuration = duration * 0.25f;
            while (elapsed < squashDuration)
            {
                elapsed += Time.deltaTime;
                SetVisualScaleMultiplier(Vector2.Lerp(Vector2.one, squash, elapsed / squashDuration));
                yield return null;
            }

            elapsed = 0f;
            float stretchDuration = duration * 0.35f;
            while (elapsed < stretchDuration)
            {
                elapsed += Time.deltaTime;
                SetVisualScaleMultiplier(Vector2.Lerp(squash, stretch, elapsed / stretchDuration));
                yield return null;
            }

            elapsed = 0f;
            float restoreDuration = duration * 0.4f;
            while (elapsed < restoreDuration)
            {
                elapsed += Time.deltaTime;
                SetVisualScaleMultiplier(Vector2.Lerp(stretch, Vector2.one, elapsed / restoreDuration));
                yield return null;
            }

            RestoreVisualScale();
            scaleFeedbackRoutine = null;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (State != BallState.Falling || collision.contactCount == 0) return;

            ContactPoint2D contact = collision.GetContact(0);
            float impactSpeed = -Vector2.Dot(collision.relativeVelocity, contact.normal);
            if (impactSpeed < minimumBounceSpeed) return;

            rb.velocity = Vector2.Reflect(collision.relativeVelocity, contact.normal) * bounceRetention;
        }
    }
}
