using UnityEngine;
using Tanks.Complete;
public class WheelAnimation : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("車本体のRigidbody（未設定なら親から自動取得）")]
    public Rigidbody carRigidbody;

    [Header("ホイール設定")]
    [Tooltip("前輪ならON（ハンドル操作が有効になる）")]
    public bool isFrontWheel = false;

    [Tooltip("ホイールの半径（m）。実際のタイヤメッシュの半径に近づけるほど、転がる動きが車の実速度と正確に一致する")]
    public float wheelRadius = 0.3f;

    [Tooltip("回転速度の演出用倍率（1 = 物理的に正確な速度で回転。見た目を誇張したい時だけ1より大きく/小さくする）")]
    public float rotationMultiplier = 1f;

    [Tooltip("ハンドルの最大切れ角（度）")]
    public float steerAngle = 30f;

    [Tooltip("ハンドルの補間速度（大きいほど素早く切れる）")]
    public float steerSmoothSpeed = 8f;

    [Header("デバッグ用回転確認マーカー")]
    [Tooltip("ONにすると回転軸から外れた位置に目印キューブを自動生成する。ホイールのメッシュが回転対称で見た目では判別しにくい場合の確認用。確認できたらOFFにするか、このセクションごと削除してOK")]
    public bool showDebugMarker = true;

    [Tooltip("マーカーを中心からどれだけ離すか。ホイールの見た目の半径に合わせて調整")]
    public float markerOffset = 0.3f;

    [Tooltip("マーカーの大きさ（X,Y,Z）")]
    public Vector3 markerScale = new Vector3(0.05f, 0.05f, 0.2f);

    // 内部状態
    private Quaternion initialRotation; // 初期回転（基準）
    private float wheelRotation;        // ホイールの累積回転角度
    private float currentSteer;         // 現在のハンドル角度（補間用）
    private InputReader m_InputReader;  // この車についているInputReader（プレイヤーごとの入力を取るため）

    void Start()
    {
        // 初期回転を保持（回転計算のズレ防止）
        initialRotation = transform.localRotation;

        // Rigidbodyが未設定なら親オブジェクトから自動取得
        if (carRigidbody == null)
            carRigidbody = GetComponentInParent<Rigidbody>();

        if (carRigidbody == null)
            Debug.LogWarning($"[WheelAnimation] {gameObject.name}: Rigidbodyが見つかりません。");

        // 親（車本体）からInputReaderを取得
        // ※ Input.GetAxisだと全プレイヤー共通の値になってしまい、分割画面で2P以降のハンドルがズレるため、
        //   CarMovementと同じくInputReader経由でこの車自身の入力を取る
        m_InputReader = GetComponentInParent<InputReader>();

        if (isFrontWheel && m_InputReader == null)
            Debug.LogWarning($"[WheelAnimation] {gameObject.name}: InputReaderが見つかりません。ハンドルが切れません。");

        if (showDebugMarker)
            CreateDebugMarker();
    }

    // 回転が実際に起きているかを目で確認するための、回転軸から外れた位置に付ける目印。
    // ホイールのメッシュ自体が回転対称（トレッドやボルト跡の無い円柱状）だと、
    // 正しく回転していても見た目では判別できないため、代わりにこの目印で確認する。
    // ※ Y方向にオフセットしているが、もし目印が動いて見えない場合はズレている軸が違う可能性があるので
    //   下のlocalPositionをX方向やZ方向に変えて試してみてください
    void CreateDebugMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "DebugRotationMarker";

        // 物理判定は不要なのでColliderは削除（地面判定などに影響しないように）
        Collider col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);

        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, markerOffset, 0f);
        marker.transform.localScale = markerScale;

        Renderer rend = marker.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.yellow;
        rend.material = mat;
    }

    void Update()
    {
        if (carRigidbody == null) return;

        // ── 前進速度を取得 ──────────────────────────
        // 車の前方向への速度成分のみ抽出（後退中は負の値になり、逆回転として扱われる）
        float speed = Vector3.Dot(
            carRigidbody.linearVelocity,
            carRigidbody.transform.forward
        );

        // ── ホイール回転（Z軸）──────────────────────
        // タイヤ半径から円周を求め、「地面を転がる」実際の回転速度(度/秒)に変換する。
        // 円周 = 2πr。この円周分だけ進むとタイヤはちょうど1回転(360度)する。
        // speedはcarRigidbodyの実速度そのものなので、通常走行はもちろん、
        // ミニターボ（ブースト）でRigidbodyの速度が上がった瞬間も、この計算式が
        // そのまま追従して回転を速くする。ブースト用の分岐は不要。
        float circumference = 2f * Mathf.PI * Mathf.Max(wheelRadius, 0.01f); // 0除算防止
        float degreesPerSecond = (speed / circumference) * 360f * rotationMultiplier;

        wheelRotation += degreesPerSecond * Time.deltaTime;

        // 角度を-360〜360に正規化してfloatの肥大化を防ぐ
        wheelRotation = Mathf.Repeat(wheelRotation, 360f);

        // ── ハンドル操舵（Y軸・前輪のみ）───────────
        float targetSteer = 0f;
        if (isFrontWheel && m_InputReader != null)
        {
            targetSteer = m_InputReader.MoveInput.x * steerAngle;
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