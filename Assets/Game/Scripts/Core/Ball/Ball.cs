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
        [SerializeField] private AudioClip popSfx;

        [Header("Marble Flight")]
        [SerializeField] private float gravityScale = 1.5f;
        [SerializeField, Range(0f, 1f)] private float bounceRetention = 0.22f;
        [SerializeField, Min(0f)] private float minimumBounceSpeed = 0.35f;

        public int ColorId { get; private set; }
        public BallState State { get; private set; }
        public object Group { get; private set; }

        // Reused for logical tap-hit and stacking checks that avoid Physics2D queries.
        public float Radius => col != null ? col.radius * transform.lossyScale.y : 0.1f;

        private Rigidbody2D rb;
        private CircleCollider2D col;
        private SpriteRenderer sr;
        private Action<Ball> onPopped;
        private Action<Ball> onGroupPopRequested;

        private AudioSource audioSource;
        private Vector3 prefabLocalScale;

        public void PopBurstFromState(Vector2 velocity, float angularVelocity)
        {
            transform.SetParent(null);
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
            audioSource = GetComponent<AudioSource>();
            prefabLocalScale = transform.localScale;
        }

        // Called each time this instance is (re)used from the pool for a fresh grid spawn.
        public void Initialize(int colorId, Sprite sprite, Action<Ball> poppedCallback)
        {
            RestorePrefabScale();
            ColorId = colorId;
            if (sprite != null) sr.sprite = sprite;
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
            if (sprite != null) sr.sprite = sprite;
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
            if (audioSource != null && popSfx != null)
            {
                audioSource.PlayOneShot(popSfx);
            }
        }

        // Stops physics once queue movement takes over so belt positioning is exact.
        public void SetQueued()
        {
            State = BallState.Queued;
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            col.isTrigger = true;
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
            RestorePrefabScale();
        }

        public void SetVisualScaleMultiplier(Vector2 multiplier)
        {
            transform.localScale = new Vector3(
                prefabLocalScale.x * multiplier.x,
                prefabLocalScale.y * multiplier.y,
                prefabLocalScale.z);
        }

        public void PrepareForPool()
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            col.enabled = false;
            RestorePrefabScale();
        }

        private void RestorePrefabScale()
        {
            transform.localScale = prefabLocalScale;
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
