using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    public class TraySlot : MonoBehaviour
    {
        [SerializeField] private int colorId;
        [SerializeField] private Color trayColor = Color.white;
        [SerializeField] private int capacity = 3;
        [SerializeField] private BallPool ballPool;
        [SerializeField] private Transform[] slotPositions; // manually placed in the editor, one per capacity slot
        [SerializeField] private float intakeMoveSpeed = 8f;

        public int ColorId => colorId;
        public bool HasSpace => placedBalls.Count < capacity;
        public float LastIntakeMoveTime { get; private set; }

        private readonly List<Ball> placedBalls = new List<Ball>();

        private void OnValidate()
        {
            SpriteRenderer trayVisual = GetComponent<SpriteRenderer>();
            if (trayVisual != null) trayVisual.color = trayColor;
        }

        private void Awake()
        {
            ApplyTrayColor();
        }

        public void Configure(int colorId, int capacity, Color trayColor, BallPool ballPool)
        {
            this.colorId = colorId;
            this.capacity = Mathf.Max(capacity, 1);
            this.trayColor = trayColor;
            this.ballPool = ballPool;
            placedBalls.Clear();
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

            return placedBalls.Count >= capacity;
        }

        public void ClearBalls()
        {
            foreach (Ball placed in placedBalls)
            {
                ballPool.Release(placed);
            }

            placedBalls.Clear();
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
            if (trayVisual != null) trayVisual.color = trayColor;
        }
    }
}
