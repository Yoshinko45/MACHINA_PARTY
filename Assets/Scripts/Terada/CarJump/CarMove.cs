using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// 車（カート）の移動・旋回・ドリフト・モデル演出を制御するコンポーネント。
    /// 入力 → モデル演出（Update） → 物理演算（FixedUpdate）の流れで処理。
    ///
    /// 【操作感の方式について】
    /// マリオカート再現プロジェクト（Player.cs）と同じ
    /// 「速度スカラー直接制御」方式を採用している。
    ///
    /// ・従来：AddForceで力を加える → 慣性が強く、曲がると速度が抜けて
    /// 　もっさりした操作感になっていた
    /// ・現在：currentSpeed（1つのfloat）をLerpで管理し、毎FixedUpdateで
    /// 　rb.linearVelocity = 前方向 × currentSpeed を直接代入する。
    /// 　旋回すると進行方向が即座に付いてくるので、
    /// 　「向けた方向にそのまま走る」マリカーの操作感になる。
    /// 　横滑りはドリフト中だけ意図的に加える。
    /// </summary>
    public class CarMove : MonoBehaviour
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
        // ■ 速度設定（マリカー式）
        // =====================================================

        [Header("速度設定（マリカー式）")]

        [Tooltip("最高速度（これ以上は加速しない）")]
        public float maxSpeed = 35f;       // 通常走行時の速度上限

        [Tooltip("アクセルの効き（1秒あたりの追従率）。大きいほど最高速度まで素早く到達する。" +
            "目安：0.5＝マリカー再現ソースと同じゆったり加速／1.0＝実際のマリカー8に近い体感／2.0以上＝即最高速")]
        public float accelResponse = 1.0f;

        [Tooltip("アクセルを離した時の惰性減速の強さ。小さいほど惰性で長く転がる（マリカー相当＝0.5）")]
        public float coastResponse = 0.5f;

        [Tooltip("ブレーキ・後退の効き。大きいほど素早く減速・後退に移る（マリカー相当＝1.5）")]
        public float brakeResponse = 1.5f;

        [Tooltip("後退の最高速度（maxSpeedに対する倍率）。マリカー相当＝0.6程度")]
        public float reverseSpeedRatio = 0.6f;

        [Tooltip("上限超過時（ブースト終了・ダート進入・ドリフト中の減速）に" +
            "超過分の速度が自然に抜けていく速さ")]
        public float overSpeedDecay = 3f;

        [Tooltip("常時かかる下向きの押し付け力（m/s²）。坂道やジャンプの着地で" +
            "車体を地面に吸い付かせ、クレスト（坂の頂上）で飛びすぎるのを防ぐ。" +
            "マリカー再現ソースの常時ダウンフォースに相当")]
        public float downForce = 30f;


        // =====================================================
        // ■ 旋回設定（マリカー式）
        // =====================================================
        //
        // マリカーは「速度帯によって曲がりやすさが変わる」のが特徴。
        // ・ほぼ停止中　　… 曲がれない
        // ・低速　　　　　… 曲がりにくい（×0.5）
        // ・中速　　　　　… 普通（×1.3）
        // ・高速　　　　　… 一番よく曲がる（×1.75）
        // ・後退中　　　　… ハンドル反転（実車のバックと同じ）
        // ・ドリフト中　　… ロックした方向へ常時旋回（×1.2ベース）
        //
        // 具体的なレートは下のconstで定義（マリカー再現ソースの値そのまま）。
        //
        // =====================================================

        [Header("旋回設定（マリカー式）")]

        [Tooltip("旋回の基本の強さ（度/秒）。速度帯ごとのレートがこれに掛かる。" +
            "例：75なら中速で約98度/秒、高速で約131度/秒")]
        public float steerStrength = 75f;

        // 速度帯ごとの旋回レート（マリカー再現ソースの値）
        private const float STEER_RATE_LOW = 0.5f;  // 低速
        private const float STEER_RATE_MID = 1.3f;  // 中速
        private const float STEER_RATE_HIGH = 1.75f; // 高速
        private const float STEER_RATE_DRIFT = 1.2f;  // ドリフト中ベース
        private const float STEER_RATE_REVERSE = 0.5f;  // 後退中（式の中で反転させる）

        // 速度帯のしきい値（maxSpeedに対する割合。元ソースの3/10/40 ÷ 75を正規化）
        private const float STEER_MIN_SPEED_RATIO = 0.04f; // これ未満は旋回不可
        private const float STEER_MID_SPEED_RATIO = 0.13f; // これ以上で中速扱い
        private const float STEER_HIGH_SPEED_RATIO = 0.53f; // これ以上で高速扱い
        private const float STEER_REVERSE_SPEED_RATIO = 0.07f; // これ以上後退していたら反転ハンドル

        // 空中で緩やかに水平姿勢へ戻す速さ
        private const float AIR_LEVEL_SPEED = 2f;


        // =====================================================
        // ■ 地面速度設定
        // =====================================================

        [Header("地面速度設定")]

        [Tooltip("Groundタグの地面（ダート）に触れたときの速度倍率（0.5 = 半減）。" +
            "ブースト中はマリカーと同じくダートの影響を受けない")]
        public float groundSpeedMultiplier = 0.5f;


        // =====================================================
        // ■ ドリフト設定
        // =====================================================
        //
        // マリカー式ドリフトの流れ：
        // ①ドリフトボタンを押すとホップ（視覚演出）して方向をロック
        // 　（スティック中立で押した場合は、最初に倒した方向でロック）
        // ②ロックした方向へ常時曲がり続ける。スティックで曲がりの
        // 　深さだけ調整できる（内側に倒す＝鋭く／外側＝緩く／中立＝普通）。
        // 　逆に倒しても絶対に逆方向へは曲がらない（これがマリカーの肝）
        // ③ドリフト中は横方向へ滑り、ゲージが溜まって3段階に成長
        // ④ボタンを離すと段階に応じたミニターボ発動
        // ⑤速度が落ちすぎるとキャンセル（ブースト無し）
        //
        // =====================================================

        [Header("ドリフト設定")]

        [Tooltip("ドリフト中の横方向への滑りやすさ（前進速度に対する割合）。" +
            "マリカー相当＝0.3前後。大きいほど外へ大きく流れる")]
        public float driftSidewaysFriction = 0.3f;

        [Tooltip("ドリフトを開始・維持できる最低速度（maxSpeedに対する割合）。" +
            "マリカー相当＝0.5（最高速の半分以上で走っている時だけドリフト可能）")]
        public float driftMinSpeedRatio = 0.5f;

        [Tooltip("ドリフト中の最高速度倍率（マリカーはドリフト中わずかに減速する。相当値＝0.87）")]
        public float driftMaxSpeedRatio = 0.87f;

        [Tooltip("ドリフト中、スティックを内側（ドリフト方向）に倒した時の曲がりの深さ（マリカー相当＝2.1）")]
        public float driftSteerTight = 2.1f;

        [Tooltip("ドリフト中、スティックを外側（逆方向）に倒した時の曲がりの浅さ（マリカー相当＝0.5）")]
        public float driftSteerLoose = 0.5f;

        [Tooltip("ドリフト中、スティック中立時の曲がりの深さ")]
        public float driftSteerNeutral = 1.0f;

        [Tooltip("ドリフト開始直後は「曲がり・横滑り・車体の傾き」が全て緩く始まり、" +
            "この速さで本来の強さまで成長する（マリカー相当＝3）。" +
            "ホップ直後にいきなり鋭角に曲がったり横へ飛んだりせず、" +
            "ワイドに入ってから締まっていく挙動を作る")]
        public float driftSteerRampSpeed = 3f;

        [Tooltip("ドリフト開始ホップの高さ（見た目だけ。物理には影響しない）")]
        public float hopHeight = 0.35f;

        [Tooltip("ドリフト開始ホップの所要時間（秒）")]
        public float hopDuration = 0.25f;


        // =====================================================
        // ■ 接地判定
        // =====================================================

        [Header("接地判定")]

        [Tooltip("接地チェックに使うRayの長さ（車体中心から地面までの距離より少し長めに設定）")]
        public float groundCheckDistance = 0.6f;

        [Tooltip("接地判定の対象レイヤー（未設定なら全レイヤーが対象）")]
        public LayerMask groundMask = ~0;

        [Tooltip("坂道の傾斜に車体の姿勢を合わせる速さ（大きいほど素早く追従）")]
        public float groundAlignSpeed = 10f;

        [Tooltip("これより急な角度（度）の法線は「坂」ではなく「壁」とみなし、姿勢追従に使わない。" +
            "台の縁などをRayがかすめて真横に近い法線を拾った時に、車体が変な方向へ傾くのを防ぐ")]
        public float maxSlopeAngle = 70f;

        [Tooltip("姿勢追従用のRayの長さ。接地チェックより長めにして、" +
            "坂の起伏で一瞬浮いた程度では姿勢追従が途切れないようにする")]
        public float groundAlignRayDistance = 2f;


        // =====================================================
        // ■ 落下速度設定
        // =====================================================

        [Header("落下速度設定")]

        [Tooltip("下方向の最大落下速度（絶対値）。小さいとふわふわ浮く感じに、大きいと自然に重力で落ちる感じになる")]
        public float maxFallSpeed = 20f;


        // =====================================================
        // ■ 車体モデル設定
        // =====================================================

        [Header("車体モデル設定")]

        [Tooltip("傾けたい車体モデル（Tank_Crawler_Model）")]
        public Transform kartModel;        // 実際に傾ける車体モデル（子オブジェクト想定）

        [Tooltip("ドリフト中に内側へ倒す車体のロール角度（Z軸）。" +
            "※以前はY軸回転に合算されていて車体が曲がりすぎて見えたため、" +
            "本来の「傾き」としてロールに分離した")]
        public float driftTiltAngle = 15f; // ドリフト中の車体の「傾き」角度（見た目のロール）

        [Tooltip("車体の向きと進行方向のズレ角度（Y軸・大きいほどズレる）")]
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
        public float[] driftPowerThresholds = new float[] { 90f, 240f, 420f };

        [Tooltip("各段階のミニターボ中、最高速度に掛かる倍率。3要素＝段階1,2,3")]
        public float[] boostSpeedMultipliers = new float[] { 1.3f, 1.6f, 2.0f };

        [Tooltip("各段階のミニターボ持続時間（秒）。3要素＝段階1,2,3（マリカー相当＝0.75／1.5／2.5）")]
        public float[] boostDurations = new float[] { 0.75f, 1.5f, 2.5f };

        [Tooltip("ブースト中に速度が上限へ向かう速さ。大きいほど「ドンッ」と伸びる")]
        public float boostAccelResponse = 2.5f;




        // ===============
        // 11/12 追加項目
        // ===============
        [Header("チキンレース設定")]

        [Tooltip("使用できるブレーキ回数")]
        public int maxBrakeUses = 3;

        [Tooltip("ブレーキ1回で減少する速度(km/h)")]
        public float brakeReductionKmh = 20f;

        [Tooltip("ブレーキによる減速にかける時間")]
        public float brakeDuration = 1f;

        // 現在残っているブレーキ回数
        private int brakeUsesRemaining;

        // アクセルを使用したか
        private bool acceleratorUsed;

        // ブレーキ入力の前回状態
        private bool previousBrakeHeld;

        // ブレーキ処理中か
        private bool isChickenBraking;

        // ブレーキ開始時の速度
        private float chickenBrakeStartSpeed;

        // ブレーキ終了時の目標速度
        private float chickenBrakeTargetSpeed;

        // ブレーキ経過時間
        private float chickenBrakeTimer;

        // CarController
        private CarController carController;

        // 外部参照プロパティ.
        public int BrakeUsesRemaining => brakeUsesRemaining;

        // 外部から現在速度をkm/hで取得する.
        public float SpeedKmh => Mathf.Abs(currentSpeed) * 3.6f;


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // キー入力の値（-1・0・1）
        private float moveInput;    // 前進：1、後退：-1、ニュートラル：0
        private float steerInput;   // 右：1、左：-1、ニュートラル：0

        // マリカー式の速度スカラー（これが操作感の心臓部。
        // 毎FixedUpdateでrb.linearVelocity = forward * currentSpeed される）
        private float currentSpeed;

        // 実際の前方向速度（rb.linearVelocityの前方成分。旋回レートの判定に使う）
        private float forwardSpeed;

        // ドリフト状態
        private bool driftActive;   // ドリフトボタンを押してドリフト状態に入っているか
        private bool drifting;      // 実際に滑っているか（方向ロック済みのドリフト状態）
        private int driftDirection; // ドリフト方向（1:右、-1:左、0:ロック保留中）
        private float driftSteerRamp = 1f; // ドリフト開始直後の曲がり緩和（0.1→1に成長）
        private bool prevDriftHeld; // 前フレームでドリフトボタンが押されていたか（押した瞬間の検出用）

        // ホップ演出（見た目だけ。kartModelを上下させる）
        private float hopTimer;
        private Vector3 kartModelBaseLocalPos; // kartModelの基準ローカル位置

        // ダート（Groundタグ）の上にいるか
        private bool onDirt;

        // 車体モデルの現在のY軸角度（localEulerAnglesに依存しないよう自前管理）
        private float currentModelAngle;

        // 車体モデルの現在のZ軸ロール角度（ドリフト中の内側への傾き）
        private float currentModelRoll;

        // kartModelの元々のZ軸角度（ロールはここへの加算として扱う）
        private float kartModelBaseEulerZ;

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
        private float boostTimer;   // ミニターボの残り持続時間
        private float activeBoostSpeedMultiplier; // 発動中のミニターボの速度倍率（発動した瞬間の段階で固定）


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


            // ===============
            // 11/12 追加項目
            // ===============
            carController = GetComponent<CarController>();
            brakeUsesRemaining = maxBrakeUses;



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

            // ホップ演出用に車体モデルの基準位置・基準ロール角を記憶
            if (kartModel != null)
            {
                kartModelBaseLocalPos = kartModel.localPosition;
                kartModelBaseEulerZ = kartModel.localEulerAngles.z;
            }

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
            CheckArraySize("Boost Durations", boostDurations, 3);

            // 収集結果をまとめて1行で表示（ここが0の項目は、対応するTransformの割り当て or 子孫のParticleSystem有無を確認）
            Debug.Log(gameObject.name + " : パーティクル収集結果 → Wheel=" + wheelParticleList.Count +
                "個 / Flash=" + flashParticleList.Count + "個 / Tube=" + tubeParticleList.Count + "個");
        }


        // =====================================================
        // ■ 有効化時のリセット
        // =====================================================
        //
        // ラウンドやり直し等でSetActiveのOFF→ONをされた時に、
        // 前回の速度・ドリフト状態を持ち越さないようにする。
        //
        // =====================================================

        void OnEnable()
        {
            currentSpeed = 0f;
            boostTimer = 0f;
            onDirt = false;
            prevDriftHeld = false;
            hopTimer = 0f;

            ResetDriftState();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }


        // =====================================================
        // ■ 入力取得・ドリフト状態管理・モデル演出
        // =====================================================

        void Update()
        {
            // InputReaderがなければ何もしない
            if (m_InputReader == null) return;




            // ===============
            // 11/12 追加項目
            // ===============
            // 入力を取得
            float rawMove = m_InputReader.CarMoveInput.y;

            steerInput = Mathf.Sign(m_InputReader.CarMoveInput.x);

            // デッドゾーン
            if (Mathf.Abs(m_InputReader.CarMoveInput.x) < 0.1f)
            {
                steerInput = 0f;
            }

            // モードごとのアクセル・ブレーキ処理
            if (carController.CurrentMode == CarController.CarMode.ChickenRace)
            {
                // アクセル
                if (rawMove > 0.1f)
                {
                    if (!acceleratorUsed)
                    {
                        moveInput = 1f;
                        acceleratorUsed = true;
                    }
                }
                // ブレーキ
                else if (rawMove < -0.1f)
                {
                    moveInput = 0f;

                    // 押した瞬間だけブレーキを使用
                    if (!previousBrakeHeld && brakeUsesRemaining > 0)
                    {
                        StartChickenBrake();
                    }

                    previousBrakeHeld = true;
                }
                else
                {
                    moveInput = 0f;
                    previousBrakeHeld = false;
                }
            }
            else
            {
                // 通常モード
                moveInput = Mathf.Abs(rawMove) < 0.1f
                    ? 0f
                    : Mathf.Sign(rawMove);
            }




            // ── 入力の正規化（-1・0・1） ──────────────────────
            // Signで-1・0・1に丸めてnormalizedの影響を排除
            // ※CarMoveInputはレースモード用の入力（X＝ハンドル、Y＝アクセル/ブレーキ）。
            //   ゲームパッドではYがスティックではなくボタン式（東=アクセル/南=ブレーキ）になる。
            //   マリカーもハンドルは実質デジタル（倒しているか否か）で、曲がりの深さは
            //   速度帯とドリフトで決まるため、Sign方式のままで操作感は一致する。
            moveInput = Mathf.Sign(m_InputReader.CarMoveInput.y);
            steerInput = Mathf.Sign(m_InputReader.CarMoveInput.x);

            // 入力が小さいときはニュートラル扱い（デッドゾーン）
            if (Mathf.Abs(m_InputReader.CarMoveInput.y) < 0.1f) moveInput = 0f;
            if (Mathf.Abs(m_InputReader.CarMoveInput.x) < 0.1f) steerInput = 0f;


            // ── ドリフト状態機械 ─────────────────────────────
            // ドリフトボタンはデバイスごとにInputReaderが解決する
            // （WASD＝左Shift、ゲームパッド＝Rボタン（右肩ボタン））
            bool driftHeld = m_InputReader.DriftHeld;
            bool driftPressed = driftHeld && !prevDriftHeld;
            prevDriftHeld = driftHeld;

            float driftMinSpeed = maxSpeed * driftMinSpeedRatio;

            // ① 開始：押した瞬間＋接地中＋一定速度以上
            if (driftPressed && !driftActive && currentSpeed > driftMinSpeed && IsGrounded())
            {
                driftActive = true;

                // 押した瞬間のスティック方向でドリフト方向をロック。
                // 中立で押した場合は0（保留）にし、最初に倒した方向でロックする
                // （旧実装は中立だと必ず左ドリフトになる不具合があった）
                driftDirection = steerInput > 0.5f ? 1 : (steerInput < -0.5f ? -1 : 0);

                // 開始直後は曲がりを緩くして、徐々に本来の深さへ（マリカーのホップ直後挙動）
                driftSteerRamp = 0.1f;

                // ホップは見た目だけ（kartModelを上下させる）。
                // 物理ジャンプ方式は落下制御と干渉して多重ホップバグの原因になっていたため廃止
                hopTimer = hopDuration;

                // ドリフト開始 → タイヤの煙パーティクルを再生
                StartDriftParticles();
            }

            if (driftActive)
            {
                // ② 方向が保留中なら、最初に倒された方向でロック
                if (driftDirection == 0 && steerInput != 0f)
                {
                    driftDirection = steerInput > 0f ? 1 : -1;
                }

                // ⑤ 速度が落ちすぎたらキャンセル（ブースト無し。マリカー仕様）
                if (currentSpeed < driftMinSpeed)
                {
                    ResetDriftState();
                }
                // ④ ボタンを離したら終了 → 溜めた段階に応じてブースト
                else if (!driftHeld)
                {
                    EndDrift();
                }
                // ③ 継続中：方向ロック済み＆接地中ならゲージを充填
                else
                {
                    if (driftDirection != 0 && IsGrounded())
                    {
                        driftPower += driftPowerRate * Time.deltaTime;
                        UpdateDriftParticles();
                    }

                    // 曲がりの深さを本来の値へ徐々に戻す
                    driftSteerRamp = Mathf.Lerp(
                        driftSteerRamp, 1f,
                        driftSteerRampSpeed * Time.deltaTime
                    );
                }
            }

            // 実際に滑るのは方向ロックが済んでから
            drifting = driftActive && driftDirection != 0;


            // ── 車体モデルの演出（向きのズレ・ロール傾き・ホップ） ────
            //
            // ※以前はdriftBodyAngle＋driftTiltAngleを両方Y軸に合算していて、
            // 　合計35°もズレて「車体が曲がりすぎ」に見えていた。
            // 　現在はY軸のズレ＝driftBodyAngleのみ、
            // 　driftTiltAngle＝内側へのロール（Z軸）に分離。
            // 　さらにdriftSteerRampを掛けて、ドリフト開始直後は浅く入り
            // 　徐々に本来の角度まで深くなるようにしている。
            //
            if (kartModel != null)
            {
                // ① ドリフト中は車体がドリフト方向と逆に向く（マリカー風・Y軸）
                float targetAngle =
                    drifting
                        ? driftDirection * driftBodyAngle * driftSteerRamp
                        : 0f;

                // ② ドリフト中は内側へ車体をロールで傾ける（Z軸）
                float targetRoll =
                    drifting
                        ? -driftDirection * driftTiltAngle * driftSteerRamp
                        : 0f;

                // localEulerAnglesから読み取らず別変数で管理することで
                // 0〜360正規化による角度の肥大化を防ぐ
                currentModelAngle = Mathf.LerpAngle(
                    currentModelAngle,
                    targetAngle,
                    Time.deltaTime * driftBodyAngleSpeed
                );

                currentModelRoll = Mathf.LerpAngle(
                    currentModelRoll,
                    targetRoll,
                    Time.deltaTime * driftBodyAngleSpeed
                );

                // 車体モデルのY軸・Z軸回転を更新（Xは元のまま）
                kartModel.localEulerAngles = new Vector3(
                    kartModel.localEulerAngles.x,
                    currentModelAngle,
                    kartModelBaseEulerZ + currentModelRoll
                );

                // ③ ホップ演出（sinカーブで上がって下りる）
                if (hopTimer > 0f)
                {
                    hopTimer = Mathf.Max(0f, hopTimer - Time.deltaTime);
                }

                float hopProgress =
                    hopDuration > 0f
                        ? Mathf.Clamp01(1f - hopTimer / hopDuration)
                        : 1f;

                float hopOffset =
                    Mathf.Sin(hopProgress * Mathf.PI) * hopHeight;

                kartModel.localPosition =
                    kartModelBaseLocalPos + Vector3.up * hopOffset;
            }
        }




        // ===============
        // 11/12 追加項目
        // ===============
        private void StartChickenBrake()
        {
            brakeUsesRemaining--;

            chickenBrakeStartSpeed = currentSpeed;

            float reduction = brakeReductionKmh / 3.6f;

            chickenBrakeTargetSpeed = Mathf.Max(
                0f,
                chickenBrakeStartSpeed - reduction
            );

            chickenBrakeTimer = 0f;
            isChickenBraking = true;
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

        // ドリフト終了時（ボタン離し）：溜めた段階に応じてミニターボを開始し、状態をリセット
        void EndDrift()
        {
            // ミニターボが1段階以上溜まっていたら、その段階の強さ・持続時間でブーストを開始する
            // （瞬間的に速度を足すと"テレポート"のように見えるため、FixedUpdate側で
            // 　上限を引き上げつつboostAccelResponseの速さで自然に加速させる方式）
            if (driftMode > 0)
            {
                int idx = driftMode - 1;
                activeBoostSpeedMultiplier = (boostSpeedMultipliers != null && boostSpeedMultipliers.Length > idx) ? boostSpeedMultipliers[idx] : 1.3f;
                boostTimer = (boostDurations != null && boostDurations.Length > idx) ? boostDurations[idx] : 0.5f;

                Debug.Log(gameObject.name + " : ミニターボ発動！ 段階=" + driftMode + " / 持続時間=" + boostTimer +
                    "秒 / 速度上限=" + (maxSpeed * activeBoostSpeedMultiplier) + "m/s / Tube再生数=" + tubeParticleList.Count);

                // ブーストの瞬間だけTubeの排気パーティクルを噴射
                foreach (ParticleSystem p in tubeParticleList)
                    p.Play();
            }
            else
            {
                // ここが出る場合はブーストではなく「ドリフトが短すぎて段階0のまま終わった」状態
                Debug.Log(gameObject.name + " : ドリフト終了（段階0のためブースト無し。driftPowerRateに対してドリフト時間が短すぎる可能性）");
            }

            ResetDriftState();
        }

        // ドリフト状態を初期状態に戻す（ブーストの有無に関わらず共通で呼ぶ）
        void ResetDriftState()
        {
            driftPower = 0f;
            driftMode = 0;
            driftActive = false;
            drifting = false;
            driftDirection = 0;
            driftSteerRamp = 1f;

            // タイヤの煙パーティクルを停止
            foreach (ParticleSystem p in wheelParticleList)
            {
                var main = p.main;
                main.startColor = Color.clear;
                p.Stop();
            }
        }




        // ===============
        // 11/12 追加項目
        // ===============
        public void ResetMovementState()
        {
            moveInput = 0f;
            steerInput = 0f;

            currentSpeed = 0f;
            forwardSpeed = 0f;

            acceleratorUsed = false;
            previousBrakeHeld = false;

            brakeUsesRemaining = maxBrakeUses;

            isChickenBraking = false;
            chickenBrakeTimer = 0f;

            boostTimer = 0f;

            ResetDriftState();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }




        // =====================================================
        // ■ 物理演算（マリカー式：速度スカラー直接制御）
        // =====================================================

        void FixedUpdate()
        {
            if (rb == null) return;

            float dt = Time.fixedDeltaTime;

            // 実際の前方向速度（旋回レートの判定に使う）
            forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);


            // ── ブーストタイマー ─────────────────────────────
            if (boostTimer > 0f)
            {
                boostTimer = Mathf.Max(0f, boostTimer - dt);
            }

            bool boosting = boostTimer > 0f;


            // ── 目標上限速度（キャップ）の決定 ─────────────────
            float cap = maxSpeed;

            if (boosting)
            {
                // ブーストはダートより優先（マリカーはブースト中ダートの影響を受けない）
                cap = maxSpeed * activeBoostSpeedMultiplier;
            }
            else if (onDirt)
            {
                cap = maxSpeed * groundSpeedMultiplier;
            }

            // ドリフト中はわずかに減速（マリカー仕様）
            if (drifting && !boosting)
            {
                cap *= driftMaxSpeedRatio;
            }


            // ── スカラー速度の更新（マリカーのLerp方式・フレームレート非依存） ──
            //
            // Lerpの係数を 1-exp(-response*dt) にすることで、
            // 「今の速度差に比例して縮まっていく」指数追従になり、
            // フレームレートが変わっても同じ体感になる。
            //

            // ===============
            // 11/12 追加項目
            // ===============
            // チキンレースのブレーキ処理
            if (isChickenBraking)
            {
                chickenBrakeTimer += dt;

                float progress = Mathf.Clamp01(
                    chickenBrakeTimer / brakeDuration
                );

                currentSpeed = Mathf.Lerp(
                    chickenBrakeStartSpeed,
                    chickenBrakeTargetSpeed,
                    progress
                );

                if (progress >= 1f)
                {
                    isChickenBraking = false;
                }
            }                           // 11/12追加項目ここまで==
            else if (moveInput < 0f)
            {
                // ブレーキ・後退（前進中なら減速し、そのまま後退へ移行）
                currentSpeed = Mathf.Lerp(
                    currentSpeed,
                    -maxSpeed * reverseSpeedRatio,
                    1f - Mathf.Exp(-brakeResponse * dt)
                );
            }
            else if (moveInput > 0f || boosting)
            {
                // アクセル
                // （ブースト中は入力が無くても前進を維持する＝マリカー仕様。
                // 　ブースト中はboostAccelResponseで「ドンッ」と伸ばす）
                float response = boosting ? boostAccelResponse : accelResponse;

                currentSpeed = Mathf.Lerp(
                    currentSpeed,
                    cap,
                    1f - Mathf.Exp(-response * dt)
                );
            }
            else
            {
                // 惰性減速（何も押していない時。じわっと転がって止まる）
                currentSpeed = Mathf.Lerp(
                    currentSpeed,
                    0f,
                    1f - Mathf.Exp(-coastResponse * dt)
                );

                // ほぼ止まったら完全停止
                if (Mathf.Abs(currentSpeed) < 0.05f)
                {
                    currentSpeed = 0f;
                }
            }

            // キャップ超過分（ブースト終了・ダート進入・ドリフト減速）は自然に抜く
            if (currentSpeed > cap)
            {
                currentSpeed = Mathf.Lerp(
                    currentSpeed,
                    cap,
                    1f - Mathf.Exp(-overSpeedDecay * dt)
                );
            }


            // ── 速度をRigidbodyへ直接代入（マリカーの核心部分） ──
            //
            // 常に「今向いている方向」へcurrentSpeedで進む。
            // これにより旋回した瞬間に進行方向も付いてくる。
            // ドリフト中だけ、ロック方向の外側へ横滑り成分を足す。
            //
            Vector3 vel = transform.forward * currentSpeed;

            if (drifting)
            {
                // 右ドリフト中は左（外側）へ、左ドリフト中は右（外側）へ滑る。
                // driftSteerRampを掛けることで、開始直後は横滑りがほぼ0から
                // 始まり徐々に本来の量まで増える。
                // （これが無いとホップした瞬間に速度×driftSidewaysFriction分の
                // 　横速度がいきなり乗り、車が横へ「ズンッ」と飛んでしまう）
                vel += -transform.right *
                    (driftDirection *
                     Mathf.Abs(currentSpeed) *
                     driftSidewaysFriction *
                     driftSteerRamp);
            }

            // Y速度：「登り坂は前方ベクトルに任せ、落下は物理に任せる」（マリカー仕様）
            // 前方ベクトル由来のY成分と物理のY速度のうち、より下向きの方を採用する
            float y = Mathf.Min(vel.y, rb.linearVelocity.y);

            // 落下速度の上限
            y = Mathf.Max(y, -maxFallSpeed);

            vel.y = y;

            rb.linearVelocity = vel;

            // 坂に吸い付かせる下向きの押し付け力
            rb.AddForce(Vector3.down * downForce, ForceMode.Acceleration);


            // ── 旋回（マリカー式：速度帯でレートが変わる） ───────
            float steerAmount = 0f;

            if (drifting)
            {
                // ドリフト中：ロックした方向へ常時旋回。
                // スティックで深さだけ変わり、逆方向へは絶対曲がらない
                float tightness;

                if (steerInput * driftDirection > 0f)
                {
                    tightness = driftSteerTight;   // 内側に倒す＝鋭く
                }
                else if (steerInput * driftDirection < 0f)
                {
                    tightness = driftSteerLoose;   // 外側に倒す＝緩く
                }
                else
                {
                    tightness = driftSteerNeutral; // 中立＝普通
                }

                steerAmount =
                    driftDirection *
                    tightness *
                    STEER_RATE_DRIFT *
                    steerStrength *
                    driftSteerRamp *
                    dt;
            }
            else if (steerInput != 0f)
            {
                float steerRate = 0f;

                if (forwardSpeed < -maxSpeed * STEER_REVERSE_SPEED_RATIO &&
                    moveInput <= 0f)
                {
                    // 後退中はハンドル反転（実車のバックと同じ）
                    steerRate = -STEER_RATE_REVERSE;
                }
                else if (forwardSpeed >= maxSpeed * STEER_HIGH_SPEED_RATIO)
                {
                    steerRate = STEER_RATE_HIGH;
                }
                else if (forwardSpeed >= maxSpeed * STEER_MID_SPEED_RATIO)
                {
                    steerRate = STEER_RATE_MID;
                }
                else if (forwardSpeed >= maxSpeed * STEER_MIN_SPEED_RATIO)
                {
                    steerRate = STEER_RATE_LOW;
                }
                // これ未満（ほぼ停止）は旋回不可＝steerRate 0のまま

                steerAmount = steerInput * steerRate * steerStrength * dt;
            }

            Quaternion afterYaw =
                rb.rotation *
                Quaternion.Euler(0f, steerAmount, 0f);


            // ── 坂道の傾斜に車体の姿勢を追従 ──────────────────
            //
            // マリカーと同じく、地面の法線に対して車体のUpを合わせる
            // フル追従（ピッチもロールも）にする。
            // 速度はforward直接代入なので、旧実装のような
            // 「ロールが横滑り防止処理と干渉して失速する」問題は起きない。
            //
            Quaternion finalRot = afterYaw;

            RaycastHit alignHit;

            bool alignGrounded =
                Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    out alignHit,
                    groundAlignRayDistance,
                    groundMask
                );

            if (alignGrounded &&
                Vector3.Angle(Vector3.up, alignHit.normal) <= maxSlopeAngle)
            {
                // 現在のUpを地面法線へ向ける回転を今の姿勢に掛ける
                Quaternion aligned =
                    Quaternion.FromToRotation(
                        afterYaw * Vector3.up,
                        alignHit.normal
                    ) * afterYaw;

                finalRot =
                    Quaternion.Slerp(
                        afterYaw,
                        aligned,
                        1f - Mathf.Exp(-groundAlignSpeed * dt)
                    );
            }
            else
            {
                // 空中：緩やかに水平姿勢へ戻す（変な傾きのまま着地しないように）
                Quaternion level =
                    Quaternion.FromToRotation(
                        afterYaw * Vector3.up,
                        Vector3.up
                    ) * afterYaw;

                finalRot =
                    Quaternion.Slerp(
                        afterYaw,
                        level,
                        1f - Mathf.Exp(-AIR_LEVEL_SPEED * dt)
                    );
            }

            rb.MoveRotation(finalRot);
        }


        // =====================================================
        // ■ 地面との接触判定
        // =====================================================

        void OnCollisionEnter(Collision collision)
        {
            // Groundタグのオブジェクト（ダート）に触れたら減速扱いにする
            if (collision.gameObject.CompareTag("Ground"))
                onDirt = true;
        }

        void OnCollisionExit(Collision collision)
        {
            // Groundタグのオブジェクトから離れたら元に戻す
            if (collision.gameObject.CompareTag("Ground"))
                onDirt = false;
        }


        // =====================================================
        // ■ 接地判定（Raycast）
        // =====================================================

        // 車体中心から真下にRayを飛ばして、地面に触れているか・地面の法線を判定する。
        // ドリフト開始・充填の条件判定に使用。
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

        // 法線が不要な呼び出し用
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