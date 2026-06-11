using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    Transform target;

    // カメラのオフセット（y.5, z.-7）.
    public Vector3 offset = new Vector3(0, 5, -10);
    public float smooth = 5f;
    public float rotationSmooth = 8f;

    // 追従するターゲットを設定.
    public void SetTarget(Transform t)
    {
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 車の向きに合わせてカメラも回す.
        Vector3 desiredPos = target.position + target.rotation * offset;
        // 追従処理（Smoothで滑らかさを調整）.
        transform.position =
            Vector3.Lerp(transform.position, desiredPos, smooth * Time.deltaTime);

        Vector3 lookDir = target.position - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * Time.deltaTime);
        }

        transform.LookAt(target);
    }
}