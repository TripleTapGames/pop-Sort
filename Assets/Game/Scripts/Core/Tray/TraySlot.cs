using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PopSort
{
    public class TraySlot : MonoBehaviour
    {
        [SerializeField] private int colorId;
        [SerializeField] private Sprite traySprite;
        [SerializeField] private int capacity = 3;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private Transform[] slotPositions; // manually placed in the editor, one per capacity slot
        [SerializeField] private float intakeMoveSpeed = 8f;

        [Header("Tray Animation Events")]
        [SerializeField] private UnityEvent onTrayFilled = new UnityEvent();
        [SerializeField] private UnityEvent onTrayEntering = new UnityEvent();

        public int ColorId => colorId;
        public bool HasSpace => placedBalls.Count < capacity;
        public bool IsComplete => hasReachedCapacity;
        public float LastIntakeMoveTime { get; private set; }

        private readonly List<Ball> placedBalls = new List<Ball>();
        private bool hasReachedCapacity;

        private void OnValidate()
        {
            SpriteRenderer trayVisual = GetComponent<SpriteRenderer>();
            if (trayVisual != null && traySprite != null) trayVisual.sprite = traySprite;
        }

        private void Awake()
        {
            ApplyTrayColor();
        }

        public void Configure(int colorId, int capacity, Sprite traySprite, BallPool ballPool)
        {
            this.colorId = colorId;
            this.capacity = Mathf.Max(capacity, 1);
            this.traySprite = traySprite;
            this.ballPool = ballPool;
            placedBalls.Clear();
            hasReachedCapacity = false;
            ApplyTrayColor();
        }

        // Returns true once this tray has reached capacity.
        public bool Fill(Ball ball)
        {
            ball.SetInTray();
            Vector3 targetPosition = slotPositions[placedBalls.Count].position;
            LastIntakeMoveTime = CalculateMoveTime(ball.transform.position, targetPosition);
            placedBalls.Add(ball);
            StartCoroutine(MoveBallToSlot(ball, targetPosition));

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
        }

        // Called by TrayColumn after the final ball has reached this tray.
        public void InvokeTrayFilled()
        {
            onTrayFilled?.Invoke();
        }

        // Called by TrayColumn when this tray begins sliding into the active position.
        public void InvokeTrayEntering()
        {
            onTrayEntering?.Invoke();
        }

        private float CalculateMoveTime(Vector3 startPosition, Vector3 targetPosition)
        {
            float speed = Mathf.Max(intakeMoveSpeed, 0.01f);
            return Vector3.Distance(startPosition, targetPosition) / speed;
        }

        private IEnumerator MoveBallToSlot(Ball ball, Vector3 targetPosition)
        {
            while (Vector3.Distance(ball.transform.position, targetPosition) > 0.001f)
            {
                ball.transform.position = Vector3.MoveTowards(
                    ball.transform.position,
                    targetPosition,
                    intakeMoveSpeed * Time.deltaTime);
                yield return null;
            }

            ball.transform.position = targetPosition;
        }

        private void ApplyTrayColor()
        {
            SpriteRenderer trayVisual = GetComponent<SpriteRenderer>();
            if (trayVisual != null && traySprite != null) trayVisual.sprite = traySprite;
        }
    }
}
