using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    Transform target;

    public Vector3 offset = new Vector3(0, 5, -7);
    public float smooth = 5f;

    public void SetTarget(Transform t)
    {
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + target.rotation * offset;

        transform.position =
            Vector3.Lerp(transform.position, desiredPos, smooth * Time.deltaTime);

        transform.LookAt(target);
    }
}