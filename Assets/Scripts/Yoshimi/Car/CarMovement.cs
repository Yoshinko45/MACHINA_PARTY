using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// 車（カート）の移動・旋回・ドリフト・モデル演出を制御するコンポーネント。
    /// 入力 → モデル演出（Update） → 物理演算（FixedUpdate）の流れで処理。
    /// </summary>
    public class CarMovement : MonoBehaviour
    {
        // =====================================================
        // ■ コンポーネント
        // =====================================================

        [Header("コンポーネント")]
        public Rigidbody rb;               // 物理演算用のRigidbody
        private InputReader m_InputReader; // 入力読み取り用コンポーネント


        // =====================================================
        // ■ プレイヤー設定
        // =====================================================

        [Header("プレイヤー設定")]
        [HideInInspector]
        public int m_PlayerNumber = 1;     // プレイヤー番号（外部から参照される想定）


        // =====================================================
        // ■ 前進・後退の設定
        // =====================================================

        [Header("前進・後退の設定")]

        [Tooltip("前進する力（大きいほど加速が速い）")]
        public float forwardAccel = 150f;  // 前進時の加速度（ForceMode.Acceleration）

        [Tooltip("後退する力（大きいほど加速が速い）")]
        public float reverseAccel = 75f;   // 後退時の加速度（前進より弱め）

        [Tooltip("最高速度（これ以上は加速しない）")]
        public float maxSpeed = 35f;       // 速度上限（currentSpeedMultiplierを掛けた値と比較）


        // =====================================================
        // ■ 旋回の設定
        // =====================================================

        [Header("旋回の設定")]

        [Tooltip("曲がる強さ（大きいほど急旋回）")]
        public float turnStrength = 100f;  // 旋回の強さ（毎フレームの回転量に影響）


        // =====================================================
        // ■ ブレーキの設定
        // =====================================================

        [Header("ブレーキの設定")]

        [Tooltip("減速の強さ（大きいほど早く止まる）")]
        public float brakeStrength = 0.1f; // 減速時の速度減少量（MoveTowardsのstep）


        // =====================================================
        // ■ 地面速度設定
        // =====================================================

        [Header("地面速度設定")]

        [Tooltip("Groundタグの地面に触れたときの速度倍率（0.5 = 半減）")]
        public float groundSpeedMultiplier = 0.5f; // 特定の地面では速度を落とすための倍率


        // =====================================================
        // ■ ドリフト設定
        // =====================================================

        [Header("ドリフト設定")]

        [Tooltip("ドリフト中の横方向への滑りやすさ（大きいほど滑る）")]
        public float driftSidewaysFriction = 0.5f; // 横方向の速度をどの程度残すか（スライド量）

        [Tooltip("ドリフト中の旋回強さ倍率")]
        public float driftTurnMultiplier = 1.5f;   // ドリフト中は旋回が強くなる倍率


        // =====================================================
        // ■ 車体モデル設定
        // =====================================================

        [Header("車体モデル設定")]

        [Tooltip("傾けたい車体モデル（Tank_Crawler_Model）")]
        public Transform kartModel;        // 実際に傾ける車体モデル（子オブジェクト想定）

        [Tooltip("ドリフト中のY軸傾き角度")]
        public float driftTiltAngle = 15f; // ドリフト中の車体の「傾き」角度（見た目）

        [Tooltip("傾きの補間速度（大きいほど素早く傾く）")]
        public float driftTiltSpeed = 5f;  // 傾きの補間速度（実際はdriftBodyAngleSpeedで兼用）

        [Tooltip("車体の向きと進行方向のズレ角度（大きいほどズレる）")]
        public float driftBodyAngle = 20f; // ドリフト中の「向きのズレ」角度（マリカー風）

        [Tooltip("車体の向きのズレの補間速度")]
        public float driftBodyAngleSpeed = 8f; // ズレ角度の補間速度


        // =====================================================
        // ■ パーティクル設定
        // =====================================================

        [Header("パーティクル設定")]

        [Tooltip("ドリフト中に再生するタイヤの煙・火花パーティクルの親（例：WheelParticles）")]
        public Transform wheelParticles;

        [Tooltip("ミニターボの段階が上がった瞬間に発生するフラッシュパーティクルの親（例：Particles）")]
        public Transform flashParticles;

        [Tooltip("ブースト発動時に噴射する排気パーティクル・右側（例：Tube_Right）")]
        public Transform tubeRight;

        [Tooltip("ブースト発動時に噴射する排気パーティクル・左側（例：Tube_Left）")]
        public Transform tubeLeft;

        [Tooltip("ミニターボの色。弱→中→強の順で3色を設定")]
        public Color[] turboColors;

        [Tooltip("ドリフトゲージが溜まる速さ（1秒あたりの増加量）")]
        public float driftPowerRate = 60f;

        [Tooltip("ミニターボ発動中、最高速度に掛かる倍率（例:1.4なら通常の1.4倍まで出せる）")]
        public float boostSpeedMultiplier = 1.4f;

        [Tooltip("ミニターボ発動中、Forward Accelに掛かる倍率。上限だけ上げても押す力が同じだと届かないため必須")]
        public float boostAccelMultiplier = 3f;

        [Tooltip("ミニターボの持続時間。段階1につき何秒ブーストが続くか（段階3なら3倍の時間）")]
        public float boostDurationPerLevel = 0.4f;


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // キー入力の値（-1・0・1）
        private float moveInput;    // 前進：1、後退：-1、ニュートラル：0
        private float turnInput;    // 右：1、左：-1、ニュートラル：0

        // ドリフト状態
        private bool drifting;      // ドリフト中かどうか
        private int driftDirection; // ドリフト方向（1:右、-1:左）

        // 現在の速度倍率（通常:1.0、Ground:0.5）
        private float currentSpeedMultiplier = 1.0f;

        // 車体モデルの現在のY軸角度（localEulerAnglesに依存しないよう自前管理）
        private float currentModelAngle;

        // ミニターボ（ドリフトブースト）関連
        // ※ KartControllerのprimaryParticles/secondaryParticlesと同じく、Startで自動収集されPlay中にInspectorで中身を確認できる
        [Header("収集されたパーティクル（自動入力・確認用）")]
        public List<ParticleSystem> wheelParticleList = new List<ParticleSystem>(); // タイヤの煙パーティクル一覧
        public List<ParticleSystem> flashParticleList = new List<ParticleSystem>(); // 段階アップ時のフラッシュパーティクル一覧
        public List<ParticleSystem> tubeParticleList = new List<ParticleSystem>();  // ブースト時の排気パーティクル一覧（左右まとめて）

        private float driftPower;   // ドリフトで溜まっているゲージ量
        private int driftMode;      // 現在のミニターボ段階（0〜3）
        private bool wasDrifting;   // 前フレームでドリフトしていたか（終了検知用）
        private float boostTimer;   // ミニターボの残り持続時間
        private float boostLogTimer; // ブースト中の速度ログを間引くためのタイマー


        // =====================================================
        // ■ 初期化
        // =====================================================

        void Start()
        {
            // Rigidbodyを取得（Inspectorで未設定なら自身から取得）
            rb = GetComponent<Rigidbody>();

            // 重心を少し下げて安定させる（車がひっくり返りにくくする）
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

            // InputReaderコンポーネントを取得（入力処理を分離）
            m_InputReader = GetComponent<InputReader>();

            // InputReaderがない場合はエラー（入力が取れないため）
            if (m_InputReader == null)
                Debug.LogError(
                    gameObject.name +
                    " に InputReader がありません"
                );

            // ドリフトパーティクルを子オブジェクトから収集
            if (wheelParticles != null)
                wheelParticles.GetComponentsInChildren<ParticleSystem>(true, wheelParticleList);
            else
                Debug.LogWarning(gameObject.name + " : Wheel Particles が未設定です（タイヤの煙が出ません）");

            if (flashParticles != null)
                flashParticles.GetComponentsInChildren<ParticleSystem>(true, flashParticleList);
            else
                Debug.LogWarning(gameObject.name + " : Flash Particles が未設定です（段階アップ時のフラッシュが出ません）");

            // ブースト排気パーティクルを左右まとめて収集
            if (tubeRight != null)
                tubeParticleList.AddRange(tubeRight.GetComponentsInChildren<ParticleSystem>(true));
            else
                Debug.LogWarning(gameObject.name + " : Tube Right が未設定です");

            if (tubeLeft != null)
                tubeParticleList.AddRange(tubeLeft.GetComponentsInChildren<ParticleSystem>(true));
            else
                Debug.LogWarning(gameObject.name + " : Tube Left が未設定です");

            // 見つかったパーティクルが0個なら、Transformは設定されているが子孫にParticleSystemが無いパターン
            if (wheelParticles != null && wheelParticleList.Count == 0)
                Debug.LogWarning(gameObject.name + " : Wheel Particles の子孫にParticleSystemが見つかりませんでした");
            if (flashParticles != null && flashParticleList.Count == 0)
                Debug.LogWarning(gameObject.name + " : Flash Particles の子孫にParticleSystemが見つかりませんでした");

            // ミニターボの色が3色揃っているか確認（足りないと段階アップ時に見た目が無色のままになる）
            if (turboColors == null || turboColors.Length < 3)
                Debug.LogWarning(gameObject.name + " : Turbo Colors が3色設定されていません（現在" + (turboColors == null ? 0 : turboColors.Length) + "色）。ミニターボの色演出が正しく出ません");

            // 収集結果をまとめて1行で表示（ここが0の項目は、対応するTransformの割り当て or 子孫のParticleSystem有無を確認）
            Debug.Log(gameObject.name + " : パーティクル収集結果 → Wheel=" + wheelParticleList.Count +
                "個 / Flash=" + flashParticleList.Count + "個 / Tube=" + tubeParticleList.Count + "個");
        }


        // =====================================================
        // ■ 入力取得・モデル演出
        // =====================================================

        void Update()
        {
            // InputReaderがなければ何もしない
            if (m_InputReader == null) return;

            // ── 入力の正規化（-1・0・1） ──────────────────────
            // Signで-1・0・1に丸めてnormalizedの影響を排除
            moveInput = Mathf.Sign(m_InputReader.MoveInput.y);
            turnInput = Mathf.Sign(m_InputReader.MoveInput.x);

            // 入力が小さいときはニュートラル扱い（デッドゾーン）
            if (Mathf.Abs(m_InputReader.MoveInput.y) < 0.1f) moveInput = 0f;
            if (Mathf.Abs(m_InputReader.MoveInput.x) < 0.1f) turnInput = 0f;

            // ── ドリフト開始・終了判定 ────────────────────────
            if (Input.GetKey(KeyCode.LeftShift) && moveInput > 0f)
            {
                // 前進中かつShiftキー押下 → ドリフト可能
                if (!drifting)
                {
                    // ドリフト開始した瞬間だけ上方向に小さく跳ねる（演出）
                    rb.AddForce(Vector3.up * 20f, ForceMode.Impulse);

                    // 開始時のみ方向を決定、以降は変更しない
                    driftDirection = turnInput > 0 ? 1 : -1;
                    drifting = true;

                    // ドリフト開始 → タイヤの煙パーティクルを再生
                    StartDriftParticles();
                }
                // ドリフト中は方向を更新しない（開始時の方向を維持）
            }
            else
            {
                // Shiftを離すか、前進していない → ドリフト終了
                drifting = false;
            }

            // ── ミニターボ（ドリフトゲージ）処理 ────────────────
            if (drifting)
            {
                // ドリフトを続けている間、ゲージを溜めてパーティクルの色を更新
                driftPower += driftPowerRate * Time.deltaTime;
                UpdateDriftParticles();
            }

            // ドリフトが終了した瞬間（前フレームはtrue→今フレームはfalse）にブースト
            if (wasDrifting && !drifting)
            {
                EndDrift();
            }
            wasDrifting = drifting;

            // ── 車体モデルの演出（傾き・向きのズレ） ───────────
            if (kartModel != null)
            {
                // ① ドリフト中はdriftDirection方向に傾き、通常時は0に戻す
                float targetTilt = drifting ? driftDirection * driftTiltAngle : 0f;

                // ② ドリフト中は車体がドリフト方向と逆に向く（マリカー風）
                float targetAngle = drifting ? driftDirection * driftBodyAngle : 0f;

                // localEulerAnglesから読み取らず別変数で管理することで
                // 0〜360正規化による角度の肥大化を防ぐ
                currentModelAngle = Mathf.LerpAngle(
                    currentModelAngle,
                    targetAngle + targetTilt,
                    Time.deltaTime * driftBodyAngleSpeed
                );

                // 車体モデルのY軸回転のみを更新（X, Zは元のまま）
                kartModel.localEulerAngles = new Vector3(
                    kartModel.localEulerAngles.x,
                    currentModelAngle,
                    kartModel.localEulerAngles.z
                );
            }
        }


        // =====================================================
        // ■ パーティクル・ミニターボ処理
        // =====================================================

        // ドリフト開始時：タイヤの煙パーティクルを再生（最初は無色）
        void StartDriftParticles()
        {
            foreach (ParticleSystem p in wheelParticleList)
            {
                var main = p.main;
                main.startColor = Color.clear;
                p.Play();
            }
        }

        // ドリフト中：ゲージ量からミニターボの段階を判定し、パーティクルの色を更新
        void UpdateDriftParticles()
        {
            int previousMode = driftMode;

            // ゲージが閾値を超えるたびに段階を1つずつ上げる
            if (driftPower > 150f) driftMode = 3;
            else if (driftPower > 100f) driftMode = 2;
            else if (driftPower > 50f) driftMode = 1;

            // turboColorsが3色揃っていなくても落ちないようにガード（この場合は無色のまま）
            Color c = Color.clear;
            if (driftMode > 0 && turboColors != null && turboColors.Length >= driftMode)
                c = turboColors[driftMode - 1];

            // 段階が上がった瞬間だけフラッシュパーティクルを再生
            if (driftMode != previousMode && driftMode > 0)
            {
                Debug.Log(gameObject.name + " : 段階" + driftMode + "に到達 → 色(RGBA)=" + c + " / Wheel再生対象=" + wheelParticleList.Count + "個 / Flash再生対象=" + flashParticleList.Count + "個");
                PlayFlashParticles(c);
            }

            foreach (ParticleSystem p in wheelParticleList)
            {
                var main = p.main;
                main.startColor = c;
            }
        }

        // 段階が上がった瞬間に発生するフラッシュパーティクルを再生
        void PlayFlashParticles(Color c)
        {
            foreach (ParticleSystem p in flashParticleList)
            {
                var main = p.main;
                main.startColor = c;
                p.Play();
            }
        }

        // ドリフト終了時：溜めた段階に応じてミニターボ（一定時間の速度上限アップ）を開始し、ゲージ関連をリセット
        void EndDrift()
        {
            // ミニターボが1段階以上溜まっていたら、一定時間だけ最高速度を引き上げる
            // （瞬間的に速度を足すと"テレポート"のように見えるため、FixedUpdate側で毎フレーム自然に加速させる方式）
            if (driftMode > 0)
            {
                boostTimer = boostDurationPerLevel * driftMode;
                Debug.Log(gameObject.name + " : ミニターボ発動！ 段階=" + driftMode + " / 持続時間=" + boostTimer +
                    "秒 / 速度上限=" + (maxSpeed * boostSpeedMultiplier) + "m/s / Tube再生数=" + tubeParticleList.Count);

                // ブーストの瞬間だけTubeの排気パーティクルを噴射
                foreach (ParticleSystem p in tubeParticleList)
                    p.Play();
            }
            else
            {
                // ここが出る場合はブーストではなく「ドリフトが短すぎて段階0のまま終わった」状態
                Debug.Log(gameObject.name + " : ドリフト終了（段階0のためブースト無し。driftPowerRateに対してドリフト時間が短すぎる可能性）");
            }

            driftPower = 0f;
            driftMode = 0;

            // タイヤの煙パーティクルを停止
            foreach (ParticleSystem p in wheelParticleList)
            {
                var main = p.main;
                main.startColor = Color.clear;
                p.Stop();
            }
        }


        // =====================================================
        // ■ 物理演算
        // =====================================================

        void FixedUpdate()
        {
            // ── 跳ね防止：Y方向の速度を制限 ───────────────────
            // 車が勝手に跳ね上がったり沈みすぎたりしないように制御
            Vector3 vel = rb.linearVelocity;
            vel.y = Mathf.Clamp(vel.y, -2f, 0.5f);
            rb.linearVelocity = vel;

            // ── 前進・後退（加速） ──────────────────────────
            // 現在の速度が上限未満なら加速（ミニターボ中は上限と加速力の両方を一時的に引き上げる）
            float speedCap = maxSpeed * currentSpeedMultiplier;
            float accelMultiplier = 1f;
            if (boostTimer > 0f)
            {
                speedCap *= boostSpeedMultiplier;
                accelMultiplier = boostAccelMultiplier;
                boostTimer = Mathf.Max(0f, boostTimer - Time.fixedDeltaTime);

                // 実際に速度が伸びているか確認用のログ（0.2秒間隔に間引き）
                boostLogTimer -= Time.fixedDeltaTime;
                if (boostLogTimer <= 0f)
                {
                    boostLogTimer = 0.2f;
                    Debug.Log(gameObject.name + " : [ブースト中] 現在速度=" + rb.linearVelocity.magnitude.ToString("F1") +
                        "m/s / 上限=" + speedCap.ToString("F1") + "m/s / 残り時間=" + boostTimer.ToString("F2") + "秒");
                }
            }

            if (rb.linearVelocity.magnitude < speedCap)
            {
                // 前進か後退かで加速度を切り替え（ブースト中はさらにaccelMultiplierを掛ける）
                float accel = (moveInput > 0 ? forwardAccel : reverseAccel) * accelMultiplier;

                // 進行方向に力を加える（ForceMode.Accelerationなので質量無視）
                rb.AddForce(
                    transform.forward * accel * moveInput * currentSpeedMultiplier,
                    ForceMode.Acceleration
                );
            }

            // ── 横方向のズレを補正 / ドリフト ─────────────────
            // 速度をローカル座標系に変換して横方向だけを操作
            Vector3 localVelocity =
                transform.InverseTransformDirection(rb.linearVelocity);

            if (drifting)
            {
                // ドリフト中は横方向の速度を残して滑らせる
                localVelocity.x = Mathf.Lerp(
                    localVelocity.x,
                    driftDirection * rb.linearVelocity.magnitude * driftSidewaysFriction,
                    Time.fixedDeltaTime * 5f
                );
            }
            else
            {
                // 通常時は横方向の速度を0にする（カートが横滑りしないように）
                localVelocity.x = 0f;
            }

            // ローカル速度をワールド速度に戻す
            rb.linearVelocity =
                transform.TransformDirection(localVelocity);

            // ── 減速・停止 ─────────────────────────────────
            // ドリフト中は減速しない（アクセルオフでも滑り続ける）
            if (Mathf.Abs(moveInput) < 0.1f && !drifting)
            {
                // 速度をゼロに向かって徐々に減速
                rb.linearVelocity = Vector3.MoveTowards(
                    rb.linearVelocity,
                    Vector3.zero,
                    brakeStrength * Time.fixedDeltaTime
                );

                // ほぼ止まっていれば完全に停止させる
                if (rb.linearVelocity.magnitude < 0.01f)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep(); // 物理演算を一時停止して負荷軽減
                }
            }

            // ── 旋回 ──────────────────────────────────────
            // 前進または後退中のみ旋回可能
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                // ドリフト中は旋回倍率を掛ける
                float turnMultiplier = drifting ? driftTurnMultiplier : 1.0f;

                // 回転量 = 入力方向 × 旋回強さ × 倍率 × 時間 × 前進/後退の符号
                float turn =
                    turnInput * turnStrength * turnMultiplier *
                    Time.fixedDeltaTime * Mathf.Sign(moveInput);

                // Y軸回転のQuaternionを作成
                Quaternion turnRotation =
                    Quaternion.Euler(0f, turn, 0f);

                // 現在の回転に掛け合わせて回転適用
                rb.MoveRotation(rb.rotation * turnRotation);
            }
        }


        // =====================================================
        // ■ 地面との接触判定
        // =====================================================

        void OnCollisionEnter(Collision collision)
        {
            // Groundタグのオブジェクトに触れたら速度倍率を下げる
            if (collision.gameObject.CompareTag("Ground"))
                currentSpeedMultiplier = groundSpeedMultiplier;
        }

        void OnCollisionExit(Collision collision)
        {
            // Groundタグのオブジェクトから離れたら速度倍率を元に戻す
            if (collision.gameObject.CompareTag("Ground"))
                currentSpeedMultiplier = 1.0f;
        }
    }
}