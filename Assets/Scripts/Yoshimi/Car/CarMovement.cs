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
        [Tooltip("物理演算用のRigidbody（Start時に自身のRigidbodyで上書きされるため、通常は空のままでOK）")]
        public Rigidbody rb;               // 物理演算用のRigidbody（Start()で自動取得・上書きされる）
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

        [Tooltip("入力が無い時（ドリフト中・ブースト中を除く）に速度をゼロへ滑らかに減衰させる強さ。" +
            "速度に比例して弱まる指数的な減衰なので、値が同じでも高速なほど「効き始め」が強く感じる。" +
            "目安：2〜3＝数秒かけてじわっと惰性で流れる／6〜8＝1秒前後でキビキビ止まる")]
        public float coastDeceleration = 3f; // 減速時の減衰係数（1-exp(-coastDeceleration*dt)をLerpで使用）


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
        // ■ 接地判定（ドリフト開始ホップの多重発生防止／坂道のピッチ追従用）
        // =====================================================

        [Header("接地判定")]

        [Tooltip("接地チェックに使うRayの長さ（車体中心から地面までの距離より少し長めに設定）")]
        public float groundCheckDistance = 0.6f;

        [Tooltip("接地判定の対象レイヤー（未設定なら全レイヤーが対象）")]
        public LayerMask groundMask = ~0;

        [Tooltip("坂道の傾斜に車体のピッチ（前後の傾き）を合わせる速さ（大きいほど素早く追従）")]
        public float groundAlignSpeed = 10f;

        [Tooltip("これより急な角度（度）の法線は「坂」ではなく「壁」とみなし、ピッチ追従に使わない。" +
            "台の縁などをRayがかすめて真横に近い法線を拾った時に、車体が変な方向へ傾くのを防ぐ")]
        public float maxSlopeAngle = 70f;


        // =====================================================
        // ■ 落下速度設定
        // =====================================================

        [Header("落下速度設定")]

        [Tooltip("上方向の最大速度（ドリフト開始ホップ用の上限）")]
        public float maxUpwardSpeed = 0.5f;

        [Tooltip("下方向の最大落下速度（絶対値）。小さいとふわふわ浮く感じに、大きいと自然に重力で落ちる感じになる")]
        public float maxFallSpeed = 20f;

        [Tooltip("落下速度がmaxFallSpeedに達するまでの加速度。RigidbodyのLinear Damping(Drag)が" +
            "設定されていると、標準の重力だけでは頭打ちになってしまうことがあるため、" +
            "Dragの影響を受けず直接この加速度でY速度をmaxFallSpeedへ近づける")]
        public float fallAcceleration = 25f;

        [Tooltip("空中に出てからこの秒数が経つまでは、fallAccelerationによる強制的な加速を行わない。" +
            "ドリフト開始ホップのような一瞬の跳ねまで強く引き戻されて速く感じてしまうのを防ぐための猶予時間")]
        public float fallAccelDelay = 0.3f;


        // =====================================================
        // ■ 車体モデル設定
        // =====================================================

        [Header("車体モデル設定")]

        [Tooltip("傾けたい車体モデル（Tank_Crawler_Model）")]
        public Transform kartModel;        // 実際に傾ける車体モデル（子オブジェクト想定）

        [Tooltip("ドリフト中のY軸傾き角度")]
        public float driftTiltAngle = 15f; // ドリフト中の車体の「傾き」角度（見た目）

        [Tooltip("傾きの補間速度 ※現在は未使用です（driftBodyAngleSpeedに統合済みのため、この値を変えても効果はありません）")]
        public float driftTiltSpeed = 5f;  // ※未使用：補間には下のdriftBodyAngleSpeedのみが使われている

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

        // =====================================================
        // ■ ミニターボ（ドリフトブースト）設定
        // =====================================================

        [Header("ミニターボ設定")]

        [Tooltip("ミニターボの色。弱→中→強の順で3色を設定")]
        public Color[] turboColors;

        [Tooltip("ドリフトゲージが溜まる速さ（1秒あたりの増加量）")]
        public float driftPowerRate = 60f;

        [Tooltip("各段階に到達するのに必要なドリフトゲージ量。3要素＝段階1,2,3。差を広げるほど上の段階が入りにくくなる")]
        public float[] driftPowerThresholds = new float[] { 60f, 160f, 300f };

        [Tooltip("各段階のミニターボ中、最高速度に掛かる倍率。3要素＝段階1,2,3")]
        public float[] boostSpeedMultipliers = new float[] { 1.3f, 1.6f, 2.0f };

        [Tooltip("各段階のミニターボ中、Forward Accelに掛かる倍率。3要素＝段階1,2,3")]
        public float[] boostAccelMultipliers = new float[] { 2.5f, 3.5f, 5.0f };

        [Tooltip("各段階のミニターボ持続時間（秒）。3要素＝段階1,2,3")]
        public float[] boostDurations = new float[] { 0.5f, 1.0f, 1.6f };


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // キー入力の値（-1・0・1）
        private float moveInput;    // 前進：1、後退：-1、ニュートラル：0
        private float turnInput;    // 右：1、左：-1、ニュートラル：0

        // ドリフト状態
        private bool drifting;      // ドリフト中かどうか
        private int driftDirection; // ドリフト方向（1:右、-1:左）

        // 空中にいる継続時間（ドリフト開始ホップと本格的な落下を区別するため）
        private float airborneTime;

        // 坂道のピッチ追従用：現在の傾き（Up方向）を滑らかに保持する内部状態
        private Vector3 currentUp = Vector3.up;

        // 現在の速度倍率（通常:1.0、Ground:0.5）
        private float currentSpeedMultiplier = 1.0f;

        // 車体モデルの現在のY軸角度（localEulerAnglesに依存しないよう自前管理）
        private float currentModelAngle;

        // ミニターボ（ドリフトブースト）関連
        // ※ KartControllerのprimaryParticles/secondaryParticlesと同じく、Startで自動収集されPlay中にInspectorで中身を確認できる
        [Header("収集されたパーティクル（自動入力・確認用）")]
        [Tooltip("タイヤの煙パーティクル一覧（Start時にWheel Particlesの子から自動収集。手動設定は不要）")]
        public List<ParticleSystem> wheelParticleList = new List<ParticleSystem>(); // タイヤの煙パーティクル一覧

        [Tooltip("段階アップ時のフラッシュパーティクル一覧（Start時にFlash Particlesの子から自動収集。手動設定は不要）")]
        public List<ParticleSystem> flashParticleList = new List<ParticleSystem>(); // 段階アップ時のフラッシュパーティクル一覧

        [Tooltip("ブースト時の排気パーティクル一覧（Start時にTube Right/Tube Leftの子から自動収集。手動設定は不要）")]
        public List<ParticleSystem> tubeParticleList = new List<ParticleSystem>();  // ブースト時の排気パーティクル一覧（左右まとめて）

        private float driftPower;   // ドリフトで溜まっているゲージ量
        private int driftMode;      // 現在のミニターボ段階（0〜3）
        private bool wasDrifting;   // 前フレームでドリフトしていたか（終了検知用）
        private float boostTimer;   // ミニターボの残り持続時間
        private float boostLogTimer; // ブースト中の速度ログを間引くためのタイマー
        private float activeBoostSpeedMultiplier; // 発動中のミニターボの速度倍率（発動した瞬間の段階で固定）
        private float activeBoostAccelMultiplier; // 発動中のミニターボの加速倍率（発動した瞬間の段階で固定）


        // =====================================================
        // ■ 外部公開プロパティ（カメラ演出などから参照する用）
        // =====================================================

        // 現在のミニターボ充填段階（0〜3）。ドリフトでゲージが上がるとリアルタイムで増え、
        // ドリフトが終わる（EndDrift）と0に戻る。カメラの「段階アップで揺れる」演出などに使う
        public int DriftStage => driftMode;


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

            // ミニターボ関連の配列がどれも3要素揃っているか確認（足りないと該当段階で無色/デフォルト扱いになる）
            CheckArraySize("Turbo Colors", turboColors, 3);
            CheckArraySize("Drift Power Thresholds", driftPowerThresholds, 3);
            CheckArraySize("Boost Speed Multipliers", boostSpeedMultipliers, 3);
            CheckArraySize("Boost Accel Multipliers", boostAccelMultipliers, 3);
            CheckArraySize("Boost Durations", boostDurations, 3);

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
                // 地面に接地している時だけホップ＆ドリフト開始を許可する
                // （接地チェックが無いと、着地前にShiftを連打するたびにインパルスが
                //   加算され続けてしまい、無限に宙に浮き続けるバグになる）
                if (!drifting && IsGrounded())
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

        // 3要素を期待する配列が足りているか確認し、足りなければConsoleに警告を出す共通チェック
        void CheckArraySize(string fieldName, float[] arr, int expected)
        {
            if (arr == null || arr.Length < expected)
                Debug.LogWarning(gameObject.name + " : " + fieldName + " が" + expected + "個設定されていません（現在" +
                    (arr == null ? 0 : arr.Length) + "個）");
        }

        void CheckArraySize(string fieldName, Color[] arr, int expected)
        {
            if (arr == null || arr.Length < expected)
                Debug.LogWarning(gameObject.name + " : " + fieldName + " が" + expected + "個設定されていません（現在" +
                    (arr == null ? 0 : arr.Length) + "個）");
        }

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

            // ゲージがdriftPowerThresholds[段階-1]を超えるたびに段階を1つずつ上げる
            if (driftPowerThresholds != null)
            {
                if (driftPowerThresholds.Length > 2 && driftPower > driftPowerThresholds[2]) driftMode = 3;
                else if (driftPowerThresholds.Length > 1 && driftPower > driftPowerThresholds[1]) driftMode = 2;
                else if (driftPowerThresholds.Length > 0 && driftPower > driftPowerThresholds[0]) driftMode = 1;
            }

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

        // ドリフト終了時：溜めた段階に応じてミニターボ（一定時間の速度上限＆加速力アップ）を開始し、ゲージ関連をリセット
        void EndDrift()
        {
            // ミニターボが1段階以上溜まっていたら、その段階の強さ・持続時間でブーストを開始する
            // （瞬間的に速度を足すと"テレポート"のように見えるため、FixedUpdate側で毎フレーム自然に加速させる方式）
            if (driftMode > 0)
            {
                int idx = driftMode - 1;
                activeBoostSpeedMultiplier = (boostSpeedMultipliers != null && boostSpeedMultipliers.Length > idx) ? boostSpeedMultipliers[idx] : 1.3f;
                activeBoostAccelMultiplier = (boostAccelMultipliers != null && boostAccelMultipliers.Length > idx) ? boostAccelMultipliers[idx] : 2f;
                boostTimer = (boostDurations != null && boostDurations.Length > idx) ? boostDurations[idx] : 0.5f;

                Debug.Log(gameObject.name + " : ミニターボ発動！ 段階=" + driftMode + " / 持続時間=" + boostTimer +
                    "秒 / 速度上限=" + (maxSpeed * activeBoostSpeedMultiplier) + "m/s / 加速倍率=" + activeBoostAccelMultiplier +
                    " / Tube再生数=" + tubeParticleList.Count);

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
            // ── 跳ね防止 ＆ 落下速度の制御 ───────────────────
            // 車が勝手に跳ね上がったり沈みすぎたりしないように制御。
            // ※単純にClampするだけだと、RigidbodyのLinear Damping(Drag)が効いている場合に
            //   標準の重力だけではmaxFallSpeedまで加速しきれず頭打ちになってしまう。
            //   そのためDragの影響を受けない形で、直接Y速度をmaxFallSpeedへ向けて加速させる。
            // ※ただし空中に出た瞬間から常にこの強制加速をかけると、ドリフト開始ホップのような
            //   一瞬の跳ねまで素早く引き戻されてしまうため、fallAccelDelay秒が経つまでは
            //   従来通りRigidbody標準の重力・Dragに任せ、本格的な落下と判断できてから加速させる。
            // ※さらに、この上限・加速はどちらも「空中にいる間」だけに限定する。
            //   接地中（坂を登っている時など）までY速度を制限すると、坂なりに上昇しようとする
            //   自然な速度成分まで頭打ちにされてしまい、地面との当たり判定と競合して
            //   登り坂で不自然に失速する原因になっていた。
            if (IsGrounded())
            {
                airborneTime = 0f;
            }
            else
            {
                airborneTime += Time.fixedDeltaTime;

                Vector3 vel = rb.linearVelocity;
                vel.y = Mathf.Min(vel.y, maxUpwardSpeed);

                if (airborneTime > fallAccelDelay)
                {
                    vel.y = Mathf.MoveTowards(vel.y, -maxFallSpeed, fallAcceleration * Time.fixedDeltaTime);
                }

                rb.linearVelocity = vel;
            }

            // ── 前進・後退（加速） ──────────────────────────
            // 現在の速度が上限未満なら加速（ミニターボ中は上限と加速力の両方を一時的に引き上げる）
            float speedCap = maxSpeed * currentSpeedMultiplier;
            float accelMultiplier = 1f;

            // 最高速度の判定は水平方向だけで行う。
            // ※rb.linearVelocity.magnitudeのままだと、坂・段差・落下などでY速度が乗った分まで
            //   speedCapの消費に数えられてしまい、前進の加速がかかりにくくなる（巡航速度が落ちる）
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // ブースト中に入力を離しても、ブーストの間だけは前進を続けてほしいための実効入力
            // （通常時はmoveInputそのまま。後退キーを押していればそちらを優先し無理に前進させない）
            float boostedMoveInput = moveInput;

            if (boostTimer > 0f)
            {
                speedCap *= activeBoostSpeedMultiplier;
                accelMultiplier = activeBoostAccelMultiplier;
                boostTimer = Mathf.Max(0f, boostTimer - Time.fixedDeltaTime);

                // 入力がニュートラルな間だけ、ブーストの推進力を維持するため前進扱いにする
                if (Mathf.Abs(moveInput) < 0.1f)
                {
                    boostedMoveInput = 1f;
                }

                // 実際に速度が伸びているか確認用のログ（0.2秒間隔に間引き）
                boostLogTimer -= Time.fixedDeltaTime;
                if (boostLogTimer <= 0f)
                {
                    boostLogTimer = 0.2f;
                    Debug.Log(gameObject.name + " : [ブースト中] 現在速度=" + horizontalVelocity.magnitude.ToString("F1") +
                        "m/s / 上限=" + speedCap.ToString("F1") + "m/s / 残り時間=" + boostTimer.ToString("F2") + "秒");
                }
            }

            if (horizontalVelocity.magnitude < speedCap)
            {
                // 前進か後退かで加速度を切り替え（ブースト中はさらにaccelMultiplierを掛ける）
                float accel = (boostedMoveInput > 0 ? forwardAccel : reverseAccel) * accelMultiplier;

                // 進行方向に力を加える（ForceMode.Accelerationなので質量無視）
                rb.AddForce(
                    transform.forward * accel * boostedMoveInput * currentSpeedMultiplier,
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
            // ドリフト中・ブースト中は減速しない（アクセルオフでも滑り続ける）
            // ※boostTimerのチェックが無いと、ミニターボ発動直後に入力を離した瞬間
            //   ブースト中でも強制的に速度0まで減速してしまう不具合があったため追加
            if (Mathf.Abs(moveInput) < 0.1f && !drifting && boostTimer <= 0f)
            {
                // 速度をゼロへ「じわっと」減衰させる。
                // 一定量ずつ引くMoveTowards（＝常に同じ力でブレーキを踏み続ける動き）だと
                // 慣性が無く機械的にピタッと止まって見えるため、カメラのダンピングと同じ
                // 「今の速度に比例して弱まる」指数的な減衰にして、惰性で転がる感じを出す
                float coastBlend =
                    1f - Mathf.Exp(-coastDeceleration * Time.fixedDeltaTime);

                rb.linearVelocity =
                    Vector3.Lerp(
                        rb.linearVelocity,
                        Vector3.zero,
                        coastBlend
                    );

                // ほぼ止まっていれば完全に停止させる
                // （指数的な減衰は理論上ゼロに到達しないので、どこかで打ち切って完全停止させる必要がある）
                if (rb.linearVelocity.magnitude < 0.05f)
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

                // 現在の回転に掛け合わせて回転適用（以前と同じく即座にフル反映）
                rb.MoveRotation(rb.rotation * turnRotation);
            }

            // ── 坂道の傾斜にピッチ（前後の傾き）だけを追従 ─────────
            // Rightを常に水平（Y成分ゼロ）に固定した状態でForward/Upを組み立てるため、
            // ロール（左右の傾き）は構造的に発生しない。旋回（Yaw）は上のブロックで
            // 即座に適用済みなので、ここではその結果を使ってピッチだけを滑らかに追従させる。
            Vector3 currentForward = rb.rotation * Vector3.forward;
            Vector3 flatForward = new Vector3(currentForward.x, 0f, currentForward.z);

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                flatForward.Normalize();

                // 水平なRight（ワールドUpとflatForwardから求めるので必ずY成分ゼロになる）
                Vector3 horizontalRight = Vector3.Cross(Vector3.up, flatForward);

                Vector3 upRef = Vector3.up;
                if (IsGrounded(out Vector3 groundNormal))
                {
                    // ワールドUpとの角度がmaxSlopeAngleを超える法線は「坂」ではなく「壁」とみなして無視する。
                    // （台の縁などをRayがかすめて真横に近い法線を拾った時の誤った傾きを防ぐため）
                    float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
                    if (slopeAngle <= maxSlopeAngle)
                    {
                        // 地面の法線からhorizontalRight方向の成分（カント・台の角の丸みなど左右の傾き）を
                        // 除去し、ピッチ（前後方向の傾き）に関係する成分だけを残す。
                        // ※これをしないと、地面のわずかな左右方向の傾きがRightに漏れてロールを起こし、
                        //   横滑り防止処理と干渉して速度が不自然に落ちる原因になる。
                        upRef = Vector3.ProjectOnPlane(groundNormal, horizontalRight).normalized;
                    }
                }

                currentUp = Vector3.Slerp(currentUp, upRef, Time.fixedDeltaTime * groundAlignSpeed).normalized;

                // horizontalRightを軸として保ったまま、currentUpに合わせてForwardを傾ける
                Vector3 tiltedForward = Vector3.Cross(horizontalRight, currentUp);

                if (tiltedForward.sqrMagnitude > 0.0001f)
                {
                    rb.MoveRotation(Quaternion.LookRotation(tiltedForward.normalized, currentUp));
                }
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


        // =====================================================
        // ■ 接地判定（Raycast）
        // =====================================================

        // 車体中心から真下にRayを飛ばして、地面に触れているか・地面の法線を判定する。
        // ドリフト開始ホップの多重発生防止（Update側）と、坂道のピッチ追従（FixedUpdate側）に使用。
        bool IsGrounded(out Vector3 normal)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
            {
                normal = hit.normal;
                return true;
            }

            normal = Vector3.up;
            return false;
        }

        // 法線が不要な呼び出し用（Update側の !drifting && IsGrounded()、空中時間の判定など）
        bool IsGrounded()
        {
            return IsGrounded(out _);
        }

#if UNITY_EDITOR
        // Scene view上でRayを可視化（groundCheckDistanceの調整用。緑=接地中/赤=非接地）
        void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
        }
#endif
    }
}