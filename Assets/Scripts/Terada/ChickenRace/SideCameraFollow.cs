using UnityEngine;

public class SideCameraFollow : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform target;

    [Header("カメラ位置")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -10f);

    [Header("追従設定")]
    [SerializeField] private float followSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        // 車の位置を基準にカメラの目標位置を決める
        Vector3 targetPosition = target.position + offset;

        // 滑らかに追従
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        // 車の方向を見る
        Vector3 lookPosition = target.position;
        lookPosition.y += 1f;

        transform.LookAt(lookPosition);
    }
}