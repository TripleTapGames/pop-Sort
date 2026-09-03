using System;
using UnityEngine;

namespace PopSort
{
    public enum BallState
    {
        InGrid,
        Falling,
        Queued,
        InTray
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public class Ball : MonoBehaviour
    {
        public int ColorId { get; private set; }
        public BallState State { get; private set; }

        private Rigidbody2D rb;
        private CircleCollider2D col;
        private SpriteRenderer sr;
        private Action<Ball> onPopped;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            sr = GetComponent<SpriteRenderer>();
        }

        // Called each time this instance is (re)used from the pool for a fresh grid spawn.
        public void Initialize(int colorId, Color color, Action<Ball> poppedCallback)
        {
            ColorId = colorId;
            sr.color = color;
            onPopped = poppedCallback;
            State = BallState.InGrid;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true; // kinematic + simulated keeps the collider visible to Physics2D queries (tap detection)
            col.isTrigger = true;
        }

        public void Pop()
        {
            if (State != BallState.InGrid) return;

            State = BallState.Falling;
            col.isTrigger = false;
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
            onPopped?.Invoke(this);
        }

        // Stops physics once queue movement takes over so belt positioning is exact.
        public void SetQueued()
        {
            State = BallState.Queued;
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            col.isTrigger = true;
        }

        public void SetInTray()
        {
            State = BallState.InTray;
        }
    }
}
