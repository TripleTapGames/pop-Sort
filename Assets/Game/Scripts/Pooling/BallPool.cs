using UnityEngine;
using UnityEngine.Pool;

namespace PopSort
{
    public class BallPool : MonoBehaviour
    {
        [SerializeField] private Ball ballPrefab;

        private ObjectPool<Ball> pool;

        private void Awake()
        {
            pool = new ObjectPool<Ball>(CreateBall, OnGetBall, OnReleaseBall, OnDestroyBall);
        }

        public Ball Get() => pool.Get();

        public void Release(Ball ball) => pool.Release(ball);

        private Ball CreateBall() => Instantiate(ballPrefab, transform);

        private void OnGetBall(Ball ball) => ball.gameObject.SetActive(true);

        private void OnReleaseBall(Ball ball) => ball.gameObject.SetActive(false);

        private void OnDestroyBall(Ball ball) => Destroy(ball.gameObject);
    }
}
