using UnityEngine;

namespace PopSort
{
    [RequireComponent(typeof(Collider2D))]
    public class ConveyorBelt : MonoBehaviour
    {
        [SerializeField] private BeltQueueManager beltQueueManager;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Ball ball = collision.collider.GetComponent<Ball>();
            if (ball != null && ball.State == BallState.Falling)
            {
                beltQueueManager.HandleBallLanded(ball);
            }
        }
    }
}
