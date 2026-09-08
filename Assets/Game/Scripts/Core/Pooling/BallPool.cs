using UnityEngine;
using UnityEngine.Pool;

namespace PopSort
{
    public class BallPool : MonoBehaviour
    {
        [SerializeField] private Ball ballPrefab;

        private ObjectPool<Ball> pool;
        private readonly System.Collections.Generic.HashSet<Ball> activeBalls = new System.Collections.Generic.HashSet<Ball>();

        private void Awake()
        {
            pool = new ObjectPool<Ball>(CreateBall, OnGetBall, OnReleaseBall, OnDestroyBall);
        }

        public Ball Get()
        {
            Ball ball = pool.Get();
            activeBalls.Add(ball);
            return ball;
        }

        public void Release(Ball ball)
        {
            if (ball == null || !activeBalls.Remove(ball)) return;
            pool.Release(ball);
        }

        // Catches balls in transient states no single subsystem tracks (e.g. airborne between a grid pop and belt landing).
        public void ReleaseAll()
        {
            foreach (Ball ball in new System.Collections.Generic.List<Ball>(activeBalls))
            {
                Release(ball);
            }
        }

        private Ball CreateBall() => Instantiate(ballPrefab, transform);

        private void OnGetBall(Ball ball) => ball.gameObject.SetActive(true);

        private void OnReleaseBall(Ball ball) => ball.gameObject.SetActive(false);

        private void OnDestroyBall(Ball ball) => Destroy(ball.gameObject);
    }
}
