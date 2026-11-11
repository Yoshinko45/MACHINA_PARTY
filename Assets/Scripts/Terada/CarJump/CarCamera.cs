using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [SerializeField]
    private Vector3 offset =
        new Vector3(0f, 3.5f, -8f);

    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float rotateSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition =
            target.position +
            target.rotation * offset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        Vector3 lookDirection =
            target.position + Vector3.up * 1.5f
            - transform.position;

        Quaternion targetRotation =
            Quaternion.LookRotation(lookDirection);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}