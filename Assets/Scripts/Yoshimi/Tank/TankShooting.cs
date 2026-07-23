using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankShooting : MonoBehaviour
    {
        // =====================================================
        // ■ InputReader
        // =====================================================

        // このタンク専用の入力
        private InputReader m_InputReader;


        // =====================================================
        // ■ 弾関連
        // =====================================================

        [Header("Shell")]

        // 発射する弾Prefab
        public Rigidbody m_Shell;

        // 発射位置
        public Transform m_FireTransform;


        // =====================================================
        // ■ UI関連
        // =====================================================

        [Header("UI")]

        // チャージ＆クールダウンゲージ
        // （チャージ中は威力、発射後は空へ戻るまでのクールダウン経過を表示）
        public Slider m_AimSlider;


        // =====================================================
        // ■ エイム矢印演出
        // =====================================================
        //
        // ・クールダウン中は矢印を指定色（デフォルト赤）にする
        // ・フルチャージ中（発射待ち）は矢印を振動させる
        //
        // 未設定の場合、AimSliderのFill画像から自動取得を試みる。
        //
        // =====================================================

        [Header("Aim Arrow")]

        [Tooltip("エイム矢印の画像。未設定ならAimSliderのFillから自動取得")]
        public Image m_AimArrowImage;

        [Tooltip("クールダウン中の矢印カラー")]
        public Color m_CooldownColor = Color.red;

        [Tooltip("フルチャージ中の振動の大きさ（ピクセル）")]
        public float m_FullChargeShakeAmount = 5f;


        // =====================================================
        // ■ エイム矢印：プレイヤー別スプライト
        // =====================================================
        //
        // 矢印スプライトは白色ベースではなく色付きで作られている
        // ため、Image.colorでの色乗算では正しい色が再現できない
        // （例：黄色ベースの画像に青をタイントすると緑になる）。
        //
        // そのため、色を乗算するのではなく、プレイヤーごとに
        // 色違いのスプライト自体を差し替える方式にする。
        //
        // ※ どのスプライトを使うかは「プレイヤー番号(1〜4)」では
        // 　 なく、実際のプレイヤーカラー（タンク本体と同じ色）に
        // 　 一番近い基準色のスプライトを選ぶ。
        // 　 理由：プレイヤー番号はスロットの「参加順」で決まる
        // 　 （例：物理的にスロット3・4だけに参加した場合、それが
        // 　 　 Player1・Player2になる）一方、タンク本体の色は
        // 　 　 参加した物理スロット自身の色をそのまま使うため、
        // 　 　 両者は必ずしも一致しない。プレイヤー番号でスプライト
        // 　 　 を選ぶと、この2人プレイのようなケースで矢印の色と
        // 　 　 本体の色がズレてしまう。実際の色で選べば、本体の
        // 　 　 色と矢印は常に一致する。
        //
        // ここに割り当てなかった場合は、従来通り色タイントに
        // フォールバックする。
        //
        // =====================================================

        [Header("Aim Arrow - Player Sprites")]

        [Tooltip("Player1（青）用の矢印スプライト")]
        public Sprite m_ArrowSpriteP1Blue;

        [Tooltip("↑の基準色。実際のプレイヤーカラーがこれに近ければ使う")]
        public Color m_ReferenceColorP1Blue =
            new Color(0f, 0.584f, 1f);

        [Tooltip("Player2（赤）用の矢印スプライト")]
        public Sprite m_ArrowSpriteP2Red;

        [Tooltip("↑の基準色")]
        public Color m_ReferenceColorP2Red =
            new Color(1f, 0f, 0.138f);

        [Tooltip("Player3（緑）用の矢印スプライト")]
        public Sprite m_ArrowSpriteP3Green;

        [Tooltip("↑の基準色")]
        public Color m_ReferenceColorP3Green = Color.green;

        [Tooltip("Player4（黄）用の矢印スプライト")]
        public Sprite m_ArrowSpriteP4Yellow;

        [Tooltip("↑の基準色")]
        public Color m_ReferenceColorP4Yellow = Color.yellow;


        // =====================================================
        // ■ サウンド関連
        // =====================================================

        [Header("Audio")]

        // 発射音用AudioSource
        public AudioSource m_ShootingAudio;

        // チャージ音
        public AudioClip m_ChargingClip;

        // 発射音
        public AudioClip m_FireClip;


        // =====================================================
        // ■ 発射設定
        // =====================================================

        [Header("Launch Settings")]

        [Tooltip("最低発射速度")]
        public float m_MinLaunchForce = 5f;

        [Tooltip("最大発射速度")]
        public float m_MaxLaunchForce = 20f;

        [Tooltip("最大チャージ時間")]
        public float m_MaxChargeTime = 0.75f;

        [Tooltip("発射後クールタイム")]
        public float m_ShotCooldown = 1.0f;


        // =====================================================
        // ■ 爆発設定
        // =====================================================

        [Header("Explosion Settings")]

        [Tooltip("最大ダメージ")]
        public float m_MaxDamage = 100f;

        [Tooltip("爆発力")]
        public float m_ExplosionForce = 50f;

        [Tooltip("爆発半径")]
        public float m_ExplosionRadius = 5f;


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // 現在の発射力
        private float m_CurrentLaunchForce;

        // チャージ速度
        private float m_ChargeSpeed;

        // 発射済みか
        private bool m_Fired;

        // チャージ中か
        private bool m_IsCharging;

        // 最低威力保存用
        private float m_BaseMinLaunchForce;

        // クールタイムタイマー
        private float m_ShotCooldownTimer;

        // クールダウン表示の開始値（発射した瞬間のチャージ量。
        // そこから空へ向かって滑らかに減っていく際の起点として使う）
        private float m_ShotCooldownStartValue;

        // エイム矢印の初期カラー（Awake時に保存し、通常時はこれに戻す）
        private Color m_AimArrowDefaultColor = Color.white;

        // 矢印の「元のスプライト」（Awake時に保存。クールダウン中の
        // 表示、およびプレイヤー専用スプライト未設定時のフォール
        // バックとして使う）
        private Sprite m_DefaultArrowSprite;

        // SetAimArrowColor()で解決した、このプレイヤー専用の矢印
        // スプライト（未設定なら null のままで、色タイントにフォール
        // バックする）
        private Sprite m_PlayerArrowSprite;

        // このタンクのプレイヤーカラー（SetAimArrowColor()で受け取る。
        // 矢印だけでなく、発射する弾のトレイル／発光色にも使う）
        private Color m_PlayerColor = Color.white;

        // 直前フレームで加えた振動オフセット（毎フレーム打ち消してから
        // 新しい振動を加えるための記録。Sliderが毎フレーム計算する
        // 「本来の位置」を基準位置として扱うために必要）
        private Vector3 m_LastShakeOffset = Vector3.zero;


        // =====================================================
        // ■ 特殊弾関連
        // =====================================================

        // 強化弾を持っているか
        private bool m_HasSpecialShell;

        // 強化倍率
        private float m_SpecialShellMultiplier = 1f;


        // =====================================================
        // ■ 外部参照用
        // =====================================================

        // 現在のチャージ割合
        public float CurrentChargeRatio =>
            (m_CurrentLaunchForce - m_MinLaunchForce) /
            (m_MaxLaunchForce - m_MinLaunchForce);

        // チャージ中か
        public bool IsCharging => m_IsCharging;

        // CPU操作か
        public bool m_IsComputerControlled { get; set; } = false;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // InputReader取得
            m_InputReader =
                GetComponent<InputReader>();


            // 無かった場合
            if (m_InputReader == null)
            {
                Debug.LogError(
                    gameObject.name +
                    " に InputReader がありません"
                );
            }


            // =================================================
            // エイム矢印画像の自動取得
            // =================================================
            //
            // Inspectorで未設定の場合、AimSliderのFill画像を
            // 矢印画像として扱う（多くの場合Fillが矢印グラフィック）。
            //
            // =================================================

            if (m_AimArrowImage == null &&
                m_AimSlider != null &&
                m_AimSlider.fillRect != null)
            {
                m_AimArrowImage =
                    m_AimSlider.fillRect
                        .GetComponent<Image>();
            }

            // 初期カラー／スプライトを保存
            //
            // ※ 位置はここでは保存しない。Fill画像の位置はAimSlider側が
            // 　 毎フレーム（value変更のたび）自動で計算し直すため、
            // 　 Awake時点の位置を基準として固定してしまうと、
            // 　 チャージ量に応じたSlider本来の動きと食い違って
            // 　 矢印がズレて見えてしまう。
            if (m_AimArrowImage != null)
            {
                m_AimArrowDefaultColor =
                    m_AimArrowImage.color;

                // クールダウン中の表示に使う「元の矢印スプライト」
                // として保存しておく（プレイヤー専用スプライトに
                // 差し替えた後も、クールダウン中はこちらに戻す）
                m_DefaultArrowSprite =
                    m_AimArrowImage.sprite;
            }
        }


        // =====================================================
        // ■ 有効化時
        // =====================================================

        private void OnEnable()
        {
            // 発射力初期化
            m_CurrentLaunchForce =
                m_MinLaunchForce;

            // 最低威力保存
            m_BaseMinLaunchForce =
                m_MinLaunchForce;

            // 発射状態リセット
            m_Fired = false;

            // チャージ停止
            m_IsCharging = false;

            // クールタイムもリセット
            // （前ラウンド／前回有効化時の残りを持ち越さない。
            // 　これが無いと、まだ一度も発射していないのに
            // 　「クールタイム明け＝満タン」と誤判定されてしまう）
            m_ShotCooldownTimer = 0f;

            m_ShotCooldownStartValue =
                m_MinLaunchForce;


            // =================================================
            // スライダー初期化
            // =================================================

            if (m_AimSlider != null)
            {
                m_AimSlider.minValue =
                    m_MinLaunchForce;

                m_AimSlider.maxValue =
                    m_MaxLaunchForce;

                m_AimSlider.value =
                    m_MinLaunchForce;

                // このSliderはあくまで表示専用（プレイヤーが直接
                // クリック／ドラッグ操作するものではない）。
                //
                // Interactableかつ Transition = Color Tint のままだと、
                // UIのフォーカス（EventSystemの選択状態）によって
                // Selectableが自動でFill画像の色を書き換えてしまう
                // ことがある（Target GraphicがFill＝矢印画像を
                // 指している場合、Highlighted等の色に勝手に
                // 変わってしまう）。
                //
                // 参加画面から遷移した直後は、参加画面側の入力操作の
                // 影響でEventSystemの選択状態が残っていることがあり、
                // 一番最初に生成される（＝Player1の）Sliderが
                // 意図せずハイライトされ、矢印だけ色が変わって
                // 見えるケースがある。
                //
                // 表示専用である以上、そもそも操作を受け付ける
                // 必要が無いため、明示的に無効化しておく。
                m_AimSlider.interactable = false;

                m_AimSlider.transition =
                    Selectable.Transition.None;
            }


            // =================================================
            // 強化弾初期化
            // =================================================

            m_HasSpecialShell = false;

            m_SpecialShellMultiplier = 1f;
        }


        // =====================================================
        // ■ Start
        // =====================================================

        private void Start()
        {
              if (m_MaxChargeTime <= 0.0001f)
            {
                Debug.LogError("m_MaxChargeTime が不正です（0以下）");
                m_MaxChargeTime = 0.1f;
            }

            // チャージ速度計算
            m_ChargeSpeed =
                (m_MaxLaunchForce - m_MinLaunchForce) /
                m_MaxChargeTime;
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            // CPU操作かプレイヤー操作か
            if (m_IsComputerControlled)
            {
                ComputerUpdate();
            }
            else
            {
                HumanUpdate();
            }
        }


        // =====================================================
        // ■ AI用：チャージ開始
        // =====================================================

        public void StartCharging()
        {
            m_Fired = false;

            m_CurrentLaunchForce =
                m_MinLaunchForce;

            m_IsCharging = true;


            // チャージ音
            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip =
                    m_ChargingClip;

                m_ShootingAudio.Play();
            }
        }


        // =====================================================
        // ■ AI用：チャージ停止
        // =====================================================

        public void StopCharging()
        {
            if (m_IsCharging)
            {
                Fire();

                m_IsCharging = false;
            }
        }


        // =====================================================
        // ■ 非チャージ中のスライダー値（クールダウン表現）
        // =====================================================
        //
        // 発射前の初期状態、およびクールタイムが明けた後はmin（空＝通常状態）。
        // 発射した瞬間は発射時のチャージ量(m_ShotCooldownStartValue)から始まり、
        // そのタンク自身のm_ShotCooldownの長さに合わせてminへ滑らかに戻っていく。
        //
        // =====================================================

        private float GetCooldownSliderValue()
        {
            // クールタイム無し（発射前の初期状態、またはクールタイム終了後）
            // → 通常の空（待機）状態
            if (m_ShotCooldownTimer <= 0f)
            {
                return m_BaseMinLaunchForce;
            }

            // m_ShotCooldownが0以下（設定ミス）なら即座に空扱いにして
            // ゼロ除算を避ける
            if (m_ShotCooldown <= 0.0001f)
            {
                return m_BaseMinLaunchForce;
            }

            // 残り割合（1＝発射直後 〜 0＝クールタイム終了）
            float remainingRatio =
                Mathf.Clamp01(
                    m_ShotCooldownTimer /
                    m_ShotCooldown
                );

            // 発射した瞬間の値から空(m_BaseMinLaunchForce)へ、
            // クールタイムの経過に合わせて滑らかに戻す
            return Mathf.Lerp(
                m_BaseMinLaunchForce,
                m_ShotCooldownStartValue,
                remainingRatio
            );
        }


        // =====================================================
        // ■ エイム矢印カラー設定（プレイヤーカラーに合わせる）
        // =====================================================
        //
        // TankManager.Setup()から、そのタンクのプレイヤーカラー
        // （タンク本体と同じ色）を渡してもらうためのAPI。
        //
        // 実際の色に一番近い基準色を持つ専用スプライトが割り当て
        // られていればそちらを使う（色乗算に頼らないので、矢印
        // スプライトが白色ベースでなくても正しい色で表示できる）。
        // 対応スプライトが無ければ、従来通りcolorを乗算する
        // 方式にフォールバックする。
        //
        // =====================================================

        public void SetAimArrowColor(Color color)
        {
            m_AimArrowDefaultColor = color;

            // 弾のトレイル／発光色にも使うため保存しておく
            m_PlayerColor = color;

            m_PlayerArrowSprite =
                GetClosestArrowSprite(color);

            // クールダウン中でなければ即座に反映
            // （次のUpdateAimArrowVisual()でも整合するが、
            // 　Setup直後の一瞬の色ズレを防ぐため即時反映しておく）
            UpdateAimArrowVisual();
        }


        // =====================================================
        // ■ 実際のプレイヤーカラーに一番近い矢印スプライト取得
        // =====================================================
        //
        // プレイヤー番号ではなく、渡された実際の色と各基準色との
        // 距離を比較し、一番近いスプライトを返す（未割り当ての
        // スプライトは候補から除外）。
        //
        // =====================================================

        private Sprite GetClosestArrowSprite(Color color)
        {
            Sprite best = null;

            float bestDistanceSq = float.MaxValue;


            if (m_ArrowSpriteP1Blue != null)
            {
                float distSq =
                    ColorDistanceSquared(
                        color, m_ReferenceColorP1Blue);

                if (distSq < bestDistanceSq)
                {
                    bestDistanceSq = distSq;
                    best = m_ArrowSpriteP1Blue;
                }
            }

            if (m_ArrowSpriteP2Red != null)
            {
                float distSq =
                    ColorDistanceSquared(
                        color, m_ReferenceColorP2Red);

                if (distSq < bestDistanceSq)
                {
                    bestDistanceSq = distSq;
                    best = m_ArrowSpriteP2Red;
                }
            }

            if (m_ArrowSpriteP3Green != null)
            {
                float distSq =
                    ColorDistanceSquared(
                        color, m_ReferenceColorP3Green);

                if (distSq < bestDistanceSq)
                {
                    bestDistanceSq = distSq;
                    best = m_ArrowSpriteP3Green;
                }
            }

            if (m_ArrowSpriteP4Yellow != null)
            {
                float distSq =
                    ColorDistanceSquared(
                        color, m_ReferenceColorP4Yellow);

                if (distSq < bestDistanceSq)
                {
                    bestDistanceSq = distSq;
                    best = m_ArrowSpriteP4Yellow;
                }
            }

            return best;
        }


        // =====================================================
        // ■ 2色間の距離（RGB空間、平方距離）
        // =====================================================
        //
        // 平方根を取る必要は無い（大小比較にしか使わないため）
        //
        // =====================================================

        private static float ColorDistanceSquared(
            Color a,
            Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;

            return
                dr * dr +
                dg * dg +
                db * db;
        }


        // =====================================================
        // ■ エイム矢印の見た目更新（色・振動）
        // =====================================================
        //
        // ・クールダウン中（まだ撃てない）は矢印を指定色にする
        // ・フルチャージ中（発射待ち）は矢印を軽く振動させる
        //
        // =====================================================

        private void UpdateAimArrowVisual()
        {
            if (m_AimArrowImage == null)
            {
                return;
            }

            // フルチャージに達し、発射待ちの状態か
            bool isFullCharge =
                m_IsCharging &&
                m_CurrentLaunchForce >= m_MaxLaunchForce;

            // クールダウン中か（発射直後でまだ再発射できない）
            bool isCoolingDown =
                !m_IsCharging &&
                m_ShotCooldownTimer > 0f;


            // ---------------------------------------------------
            // スプライト・カラー
            // ---------------------------------------------------
            //
            // クールダウン中：元の矢印スプライトにクールダウン色を
            // 　タイントする（元スプライト×赤等は破綻しないため
            // 　問題ない）。
            //
            // 通常時：プレイヤー専用スプライトがあればそれに差し
            // 　替えてcolor=白（スプライト本来の色をそのまま出す）。
            // 　専用スプライトが無ければ、従来通り元スプライト＋
            // 　色タイントにフォールバックする。
            //
            // ---------------------------------------------------

            if (isCoolingDown)
            {
                if (m_DefaultArrowSprite != null)
                {
                    m_AimArrowImage.sprite =
                        m_DefaultArrowSprite;
                }

                m_AimArrowImage.color =
                    m_CooldownColor;
            }
            else if (m_PlayerArrowSprite != null)
            {
                m_AimArrowImage.sprite =
                    m_PlayerArrowSprite;

                m_AimArrowImage.color =
                    Color.white;
            }
            else
            {
                if (m_DefaultArrowSprite != null)
                {
                    m_AimArrowImage.sprite =
                        m_DefaultArrowSprite;
                }

                m_AimArrowImage.color =
                    m_AimArrowDefaultColor;
            }

            // ---------------------------------------------------
            // 振動（フルチャージ中のみ）
            // ---------------------------------------------------
            //
            // AimSliderは毎フレームvalueに応じてFillRectの位置を
            // 自動で動かしている。ここでは「その本来の位置」に対して
            // 振動オフセットを足し引きするだけにする（絶対座標を
            // 直接書き換えると、Slider本来の動きと衝突してズレる）。
            //
            // 前フレームに加えたオフセットを一旦打ち消してから、
            // 今フレームのオフセットを新しく加える。
            //
            // ---------------------------------------------------

            m_AimArrowImage.rectTransform.localPosition -=
                m_LastShakeOffset;

            if (isFullCharge)
            {
                Vector3 shakeOffset =
                    new Vector3(
                        Random.Range(-1f, 1f),
                        Random.Range(-1f, 1f),
                        0f
                    ) * m_FullChargeShakeAmount;

                m_AimArrowImage.rectTransform.localPosition +=
                    shakeOffset;

                m_LastShakeOffset = shakeOffset;
            }
            else
            {
                m_LastShakeOffset = Vector3.zero;
            }
        }


        // =====================================================
        // ■ プレイヤー操作更新
        // =====================================================

        private void HumanUpdate()
        {
            // InputReader未取得
            if (m_InputReader == null)
            {
                return;
            }


            // =================================================
            // 入力取得
            // =================================================

            bool firePressed =
                m_InputReader.FirePressed;

            bool fireHolding =
                m_InputReader.FireHeld;

            bool fireReleased =
                m_InputReader.FireReleased;


            // =================================================
            // クールタイム減少
            // =================================================

            if (m_ShotCooldownTimer > 0f)
            {
                m_ShotCooldownTimer -=
                    Time.deltaTime;
            }


            // =================================================
            // 待機中UI（クールダウン表現）
            // =================================================
            //
            // 発射直後は発射時のチャージ量、m_ShotCooldownの時間を
            // かけて空（通常状態）へ戻っていくように見せる
            //
            // =================================================

            if (!m_IsCharging &&
                m_AimSlider != null)
            {
                m_AimSlider.value =
                    GetCooldownSliderValue();
            }


            // =================================================
            // ボタン押した瞬間
            // =================================================

            if (m_ShotCooldownTimer <= 0f &&
                firePressed &&
                !m_IsCharging)
            {
                // 発射状態リセット
                m_Fired = false;

                // 最低威力から開始
                m_CurrentLaunchForce =
                    m_MinLaunchForce;

                // チャージ開始
                m_IsCharging = true;


                // チャージ音
                if (m_ShootingAudio != null)
                {
                    m_ShootingAudio.clip =
                        m_ChargingClip;

                    m_ShootingAudio.Play();
                }
            }


            // =================================================
            // チャージ中
            // =================================================

            if (m_IsCharging &&
                !m_Fired)
            {
                // チャージ増加
                m_CurrentLaunchForce +=
                    m_ChargeSpeed *
                    Time.deltaTime;


                // 最大値制限
                // （フルチャージになっても自動発射はしない。
                // 　ここで上限に留まり、ボタンを離すまで待機する）
                m_CurrentLaunchForce =
                    Mathf.Clamp(
                        m_CurrentLaunchForce,
                        m_MinLaunchForce,
                        m_MaxLaunchForce
                    );


                // UI更新
                if (m_AimSlider != null)
                {
                    m_AimSlider.value =
                        m_CurrentLaunchForce;
                }


                // ボタンを離した瞬間に発射
                // （フルチャージ状態でも自動発射せず、
                // 　プレイヤー自身のタイミングに委ねる）
                if (fireReleased)
                {
                    Fire();

                    return;
                }
            }


            // =================================================
            // エイム矢印の見た目更新
            // =================================================

            UpdateAimArrowVisual();
        }


        // =====================================================
        // ■ CPU操作更新
        // =====================================================

        private void ComputerUpdate()
        {
            // =================================================
            // クールタイム減少
            // =================================================
            //
            // HumanUpdate側と同様にここでも減らしておかないと、
            // CPUタンクだけクールダウン表示が発射後ずっと
            // 空のまま止まって見えてしまう
            //
            // =================================================

            if (m_ShotCooldownTimer > 0f)
            {
                m_ShotCooldownTimer -=
                    Time.deltaTime;
            }


            // UI更新（クールダウン表現。チャージ中は下で上書きされる）
            if (m_AimSlider != null)
            {
                m_AimSlider.value =
                    GetCooldownSliderValue();
            }


            // 最大チャージ到達
            if (m_CurrentLaunchForce >=
                m_MaxLaunchForce &&
                !m_Fired)
            {
                m_CurrentLaunchForce =
                    m_MaxLaunchForce;

                Fire();
            }


            // チャージ中
            else if (m_IsCharging &&
                     !m_Fired)
            {
                m_CurrentLaunchForce +=
                    m_ChargeSpeed *
                    Time.deltaTime;


                // UI更新
                if (m_AimSlider != null)
                {
                    m_AimSlider.value =
                        m_CurrentLaunchForce;
                }
            }


            // =================================================
            // エイム矢印の見た目更新
            // =================================================

            UpdateAimArrowVisual();
        }


        // =====================================================
        // ■ 発射処理
        // =====================================================

        private void Fire()
        {
            // 弾Prefab未設定
            if (m_Shell == null)
            {
                Debug.LogError(
                    "m_Shell が設定されていません"
                );

                return;
            }


            // 発射位置未設定
            if (m_FireTransform == null)
            {
                Debug.LogError(
                    "m_FireTransform が設定されていません"
                );

                return;
            }


            // 発射済み
            m_Fired = true;

            // チャージ終了
            m_IsCharging = false;


            // =================================================
            // 生成位置
            // =================================================

            Vector3 spawnPos =
                m_FireTransform.position +
                m_FireTransform.forward * 2f;


            // =================================================
            // 弾生成
            // =================================================

            Rigidbody shellInstance =
                Instantiate(
                    m_Shell,
                    spawnPos,
                    m_FireTransform.rotation
                );


            // =================================================
            // 発射速度
            // =================================================

            shellInstance.linearVelocity =
                m_CurrentLaunchForce *
                m_FireTransform.forward;


            // =================================================
            // 爆発設定
            // =================================================

            ShellExplosion explosionData =
                shellInstance.GetComponent<ShellExplosion>();


            if (explosionData != null)
            {
                 explosionData.SetOwner(gameObject);

                // 弾のトレイル／発光をこのタンクの
                // プレイヤーカラーに合わせる
                explosionData.SetTrailColor(
                    m_PlayerColor
                );

                explosionData.m_ExplosionForce =
                    m_ExplosionForce;

                explosionData.m_ExplosionRadius =
                    m_ExplosionRadius;

                explosionData.m_MaxDamage =
                    m_MaxDamage;


                // 強化弾処理
                if (m_HasSpecialShell)
                {
                    explosionData.m_MaxDamage *=
                        m_SpecialShellMultiplier;

                    m_HasSpecialShell = false;

                    m_SpecialShellMultiplier = 1f;


                    // PowerUp解除
                    PowerUpDetector powerUpDetector =
                        GetComponent<PowerUpDetector>();

                    if (powerUpDetector != null)
                    {
                        powerUpDetector.m_HasActivePowerUp =
                            false;
                    }


                    // HUD非表示
                    PowerUpHUD powerUpHUD =
                        GetComponentInChildren<PowerUpHUD>();

                    if (powerUpHUD != null)
                    {
                        powerUpHUD.DisableActiveHUD();
                    }
                }
            }


            // =================================================
            // 発射音
            // =================================================

            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip =
                    m_FireClip;

                m_ShootingAudio.Play();
            }


            // =================================================
            // リセット
            // =================================================

            // クールダウン表示の開始値として、発射した瞬間の
            // チャージ量を保存しておく（リセットで上書きされる前に）
            m_ShotCooldownStartValue =
                m_CurrentLaunchForce;

            m_CurrentLaunchForce =
                m_MinLaunchForce;

            m_ShotCooldownTimer =
                m_ShotCooldown;
        }


        // =====================================================
        // ■ 強化弾装備
        // =====================================================

        public void EquipSpecialShell(
            float damageMultiplier)
        {
            m_HasSpecialShell = true;

            m_SpecialShellMultiplier =
                damageMultiplier;
        }


        // =====================================================
        // ■ 着弾予測位置取得
        // =====================================================

        public Vector3 GetProjectilePosition(
            float chargingLevel)
        {
            // 発射位置未設定
            if (m_FireTransform == null)
            {
                return transform.position;
            }


            // 発射力補間
            float chargeLevel =
                Mathf.Lerp(
                    m_MinLaunchForce,
                    m_MaxLaunchForce,
                    chargingLevel
                );


            // 初速
            Vector3 velocity =
                chargeLevel *
                m_FireTransform.forward;


            // 放物線計算
            float a =
                0.5f * Physics.gravity.y;

            float b =
                velocity.y;

            float c =
                m_FireTransform.position.y;


            // 判別式
            float sqrtContent =
                b * b - 4 * a * c;


            // 解無し
            if (sqrtContent <= 0)
            {
                return m_FireTransform.position;
            }


            // 二次方程式
            float answer1 =
                (-b + Mathf.Sqrt(sqrtContent)) /
                (2 * a);

            float answer2 =
                (-b - Mathf.Sqrt(sqrtContent)) /
                (2 * a);


            // 正の解
            float answer =
                answer1 > 0 ?
                answer1 :
                answer2;


            // 着弾位置
            Vector3 position =
                m_FireTransform.position +
                new Vector3(
                    velocity.x,
                    0,
                    velocity.z
                ) * answer;


            // 地面高さ
            position.y = 0;

            return position;
        }
    }
}