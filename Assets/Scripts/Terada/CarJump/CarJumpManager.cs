using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class CarJumpManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform car;
        [SerializeField] private Transform jumpStartPoint;
        [SerializeField] private Text distanceText;

        [Header("Speed UI")]
        [SerializeField] private CarMove carMove;
        [SerializeField] private Text speedText;

        [Header("Wheel Colliders")]
        [SerializeField] private WheelCollider frontLeft;
        [SerializeField] private WheelCollider frontRight;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;

        private float distance;
        private bool isJumping = false;
        private bool isLanded = false;

        private void Start()
        {
            distanceText.text = "Distance : 0.0 m";
        }

        private void Update()
        {
            // 速度表示は常に更新
            speedText.text = $"{carMove.SpeedKmh:F0} km/h";

            if (isLanded)
            {
                return;
            }

            // 4輪すべてが地面から離れているか
            bool isGrounded =
                frontLeft.isGrounded ||
                frontRight.isGrounded ||
                rearLeft.isGrounded ||
                rearRight.isGrounded;

            // ジャンプ開始
            if (!isJumping && !isGrounded)
            {
                isJumping = true;
            }

            if (!isJumping)
            {
                return;
            }

            // 坂の端から車までの水平距離
            Vector3 start = jumpStartPoint.position;
            Vector3 end = car.position;

            start.y = 0f;
            end.y = 0f;

            distance = Vector3.Distance(start, end);

            distanceText.text =
                $"Distance : {distance:F1} m";

            // 着地したら計測終了
            if (isGrounded)
            {
                isLanded = true;

                distanceText.text =
                    $"Result : {distance:F1} m";
            }
        }
    }
}