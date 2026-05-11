using UnityEngine;

public class WheelAnimation : MonoBehaviour
{
    [Header("参照")]
    public Rigidbody carRigidbody;
    public bool isFrontWheel = false;

    [Header("設定")]
    public float rotationSpeed = 300f;
    public float steerAngle = 30f;

    private Quaternion initialRotation;
    private float wheelRotation;

    void Start()
    {
        // 初期回転を保持（これがズレ防止）
        initialRotation = transform.localRotation;

        if (carRigidbody == null)
            carRigidbody = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (carRigidbody == null) return;

        // ===== 前進速度取得 =====
        float speed = Vector3.Dot(
            carRigidbody.linearVelocity,
            carRigidbody.transform.forward
        );

        // ===== Z軸回転 =====
        wheelRotation += speed * rotationSpeed * Time.deltaTime;

        // ===== ハンドル（Y軸）=====
        float steer = 0f;
        if (isFrontWheel)
        {
            steer = Input.GetAxis("Horizontal") * steerAngle;
        }

        // ===== 回転合成 =====
        Quaternion rotZ = Quaternion.AngleAxis(wheelRotation, Vector3.forward); // ←Z軸
        Quaternion rotY = Quaternion.AngleAxis(steer, Vector3.up);              // ←ハンドル

        // 順番大事（ハンドル→回転）
        transform.localRotation = initialRotation * rotY * rotZ;
    }
}