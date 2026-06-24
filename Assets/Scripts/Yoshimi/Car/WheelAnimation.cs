using UnityEngine;

public class WheelAnimation : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("車本体のRigidbody（未設定なら親から自動取得）")]
    public Rigidbody carRigidbody;

    [Header("ホイール設定")]
    [Tooltip("前輪ならON（ハンドル操作が有効になる）")]
    public bool isFrontWheel = false;

    [Tooltip("ホイールの回転速度（大きいほど速く回る）")]
    public float rotationSpeed = 300f;

    [Tooltip("ハンドルの最大切れ角（度）")]
    public float steerAngle = 30f;

    [Tooltip("ハンドルの補間速度（大きいほど素早く切れる）")]
    public float steerSmoothSpeed = 8f;

    // 内部状態
    private Quaternion initialRotation; // 初期回転（基準）
    private float wheelRotation;        // ホイールの累積回転角度
    private float currentSteer;         // 現在のハンドル角度（補間用）

    void Start()
    {
        // 初期回転を保持（回転計算のズレ防止）
        initialRotation = transform.localRotation;

        // Rigidbodyが未設定なら親オブジェクトから自動取得
        if (carRigidbody == null)
            carRigidbody = GetComponentInParent<Rigidbody>();

        if (carRigidbody == null)
            Debug.LogWarning($"[WheelAnimation] {gameObject.name}: Rigidbodyが見つかりません。");
    }

    void Update()
    {
        if (carRigidbody == null) return;

        // ── 前進速度を取得 ──────────────────────────
        // 車の前方向への速度成分のみ抽出
        float speed = Vector3.Dot(
            carRigidbody.linearVelocity,
            carRigidbody.transform.forward
        );

        // ── ホイール回転（Z軸）──────────────────────
        wheelRotation += speed * rotationSpeed * Time.deltaTime;

        // 角度を-360〜360に正規化してfloatの肥大化を防ぐ
        wheelRotation = Mathf.Repeat(wheelRotation, 360f);

        // ── ハンドル操舵（Y軸・前輪のみ）───────────
        float targetSteer = 0f;
        if (isFrontWheel)
        {
            targetSteer = Input.GetAxis("Horizontal") * steerAngle;
        }

        // 急激に切れないよう滑らかに補間
        currentSteer = Mathf.Lerp(currentSteer, targetSteer, steerSmoothSpeed * Time.deltaTime);

        // ── 回転を合成して適用 ──────────────────────
        Quaternion rotZ = Quaternion.AngleAxis(wheelRotation, Vector3.forward); // 転がり
        Quaternion rotY = Quaternion.AngleAxis(currentSteer, Vector3.up);       // ハンドル

        // 順番重要：初期回転 → ハンドル → 転がり
        transform.localRotation = initialRotation * rotY * rotZ;
    }
}