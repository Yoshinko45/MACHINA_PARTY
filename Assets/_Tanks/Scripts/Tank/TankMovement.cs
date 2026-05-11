using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    // =====================================================
    // このスクリプトを他より先に実行
    // （Inputなどを先に準備するため）
    // =====================================================
    [DefaultExecutionOrder(-10)]

    public class TankMovement : MonoBehaviour
    {
        // =====================================================
        // プレイヤー設定
        // =====================================================

        [Header("Player")]

        // プレイヤー番号
        public int m_PlayerNumber = 1;

        // =====================================================
        // 移動設定
        // =====================================================

        [Header("Movement")]

        // 前進速度
        public float m_Speed = 12f;

        // 回転速度
        public float m_TurnSpeed = 180f;

        // カメラ方向に対して移動するか
        public bool m_IsDirectControl;

        // =====================================================
        // サウンド設定
        // =====================================================

        [Header("Audio")]

        // エンジン音用AudioSource
        public AudioSource m_MovementAudio;

        // 停止時の音
        public AudioClip m_EngineIdling;

        // 移動時の音
        public AudioClip m_EngineDriving;

        // 音程のランダム幅
        public float m_PitchRange = 0.2f;

        // =====================================================
        // 操作設定
        // =====================================================

        [Header("Control")]

        // CPU操作かどうか
        public bool m_IsComputerControlled = false;

        // 入力管理用
        [HideInInspector]
        public TankInputUser m_InputUser;

        // 使用する操作番号
        public int ControlIndex { get; set; } = -1;

        // =====================================================
        // コンポーネント
        // =====================================================

        // CharacterController
        private CharacterController m_CharacterController;

        // =====================================================
        // 入力関連
        // =====================================================

        // 移動入力
        private InputAction m_MoveAction;

        // 回転入力
        private InputAction m_TurnAction;

        // 前後入力値
        private float m_MovementInputValue;

        // 左右入力値
        private float m_TurnInputValue;

        // ゲームパッド操作か
        private bool m_UsingGamepadScheme;

        // =====================================================
        // 移動関連
        // =====================================================

        // 要求された移動方向
        private Vector3 m_RequestedDirection;

        // 爆風ノックバック速度
        private Vector3 m_ExplosionVelocity;

        // =====================================================
        // サウンド関連
        // =====================================================

        // 元の音程
        private float m_OriginalPitch;

        // =====================================================
        // エフェクト関連
        // =====================================================

        // 子ParticleSystem一覧
        private ParticleSystem[] m_particleSystems;

        // =====================================================
        // Awake
        // 最初に呼ばれる
        // =====================================================

        private void Awake()
        {
            // CharacterController取得
            m_CharacterController =
                GetComponent<CharacterController>();

            // 無ければ追加
            if (m_CharacterController == null)
            {
                m_CharacterController =
                    gameObject.AddComponent<CharacterController>();
            }

            // 入力管理取得
            m_InputUser =
                GetComponent<TankInputUser>();

            // 無ければ追加
            if (m_InputUser == null)
            {
                m_InputUser =
                    gameObject.AddComponent<TankInputUser>();
            }
        }

        // =====================================================
        // 有効化時
        // =====================================================

        private void OnEnable()
        {
            // 入力値初期化
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;

            // 爆風初期化
            m_ExplosionVelocity = Vector3.zero;

            // 全Particle取得
            m_particleSystems =
                GetComponentsInChildren<ParticleSystem>();

            // Particle再生
            for (int i = 0; i < m_particleSystems.Length; ++i)
            {
                m_particleSystems[i].Play();
            }
        }

        // =====================================================
        // 無効化時
        // =====================================================

        private void OnDisable()
        {
            // Particle停止
            for (int i = 0; i < m_particleSystems.Length; ++i)
            {
                m_particleSystems[i].Stop();
            }
        }

        // =====================================================
        // Start
        // =====================================================

        private void Start()
        {
            // =====================================================
            // CPU操作ならAI追加
            // =====================================================

            if (m_IsComputerControlled)
            {
                var ai = GetComponent<TankAI>();

                if (ai == null)
                {
                    gameObject.AddComponent<TankAI>();
                }

                return;
            }

            // =====================================================
            // 操作番号設定
            // =====================================================

            if (ControlIndex == -1)
            {
                ControlIndex = m_PlayerNumber;
            }

            // モバイルUI取得
            var mobileControl =
                FindAnyObjectByType<MobileUIControl>();

            // =====================================================
            // 操作方式設定
            // =====================================================

            // モバイル操作の場合
            if (mobileControl != null &&
                ControlIndex == 1)
            {
                // モバイルデバイスをペアリング
                m_InputUser.SetNewInputUser(
                    InputUser.PerformPairingWithDevice(
                        mobileControl.Device
                    )
                );

                // Gamepadスキーム有効
                TryActivateScheme("Gamepad");

                m_UsingGamepadScheme = true;
            }
            else
            {
                // キーボード操作
                TryActivateScheme("Keyboard&Mouse");

                m_UsingGamepadScheme = false;
            }

            // =====================================================
            // 入力アクション取得
            // =====================================================

            // Move取得
            m_MoveAction =
                m_InputUser.ActionAsset.FindAction("Move");

            // 無かったらエラー
            if (m_MoveAction == null)
            {
                Debug.LogError("Move Action not found.");
                enabled = false;
                return;
            }

            // Look取得
            m_TurnAction =
                m_InputUser.ActionAsset.FindAction("Look");

            // 無ければHorizontal取得
            if (m_TurnAction == null)
            {
                m_TurnAction =
                    m_InputUser.ActionAsset.FindAction("Horizontal");
            }

            // それでも無ければエラー
            if (m_TurnAction == null)
            {
                Debug.LogError("Turn Action not found.");
                enabled = false;
                return;
            }

            // 入力有効化
            m_MoveAction.Enable();
            m_TurnAction.Enable();

            // 元の音程保存
            if (m_MovementAudio)
            {
                m_OriginalPitch =
                    m_MovementAudio.pitch;
            }
        }

        // =====================================================
        // 毎フレーム更新
        // =====================================================

        private void Update()
        {
            // CPU操作でないなら入力取得
            if (!m_IsComputerControlled)
            {
                // Move入力取得
                Vector2 moveInput =
                    m_MoveAction.ReadValue<Vector2>();

                // 前後入力
                m_MovementInputValue =
                    moveInput.y;

                // 左右入力
                m_TurnInputValue =
                    moveInput.x;
            }

            // エンジン音更新
            if (m_MovementAudio)
            {
                EngineAudio();
            }

            // 移動処理
            Move();

            // 回転処理
            Turn();
        }

        // =====================================================
        // エンジン音処理
        // =====================================================

        private void EngineAudio()
        {
            // 動いているか判定
            bool isMoving =
                Mathf.Abs(m_MovementInputValue) > 0.1f ||
                Mathf.Abs(m_TurnInputValue) > 0.1f;

            // =====================================================
            // 停止中
            // =====================================================

            if (!isMoving)
            {
                // 現在走行音なら切り替える
                if (m_MovementAudio.clip ==
                    m_EngineDriving)
                {
                    // アイドル音へ変更
                    m_MovementAudio.clip =
                        m_EngineIdling;

                    // 少しランダム音程
                    m_MovementAudio.pitch =
                        Random.Range(
                            m_OriginalPitch - m_PitchRange,
                            m_OriginalPitch + m_PitchRange
                        );

                    // 再生
                    m_MovementAudio.Play();
                }
            }

            // =====================================================
            // 移動中
            // =====================================================

            else
            {
                // 現在停止音なら切り替える
                if (m_MovementAudio.clip ==
                    m_EngineIdling)
                {
                    // 走行音へ変更
                    m_MovementAudio.clip =
                        m_EngineDriving;

                    // ランダム音程
                    m_MovementAudio.pitch =
                        Random.Range(
                            m_OriginalPitch - m_PitchRange,
                            m_OriginalPitch + m_PitchRange
                        );

                    // 再生
                    m_MovementAudio.Play();
                }
            }
        }

        // =====================================================
        // 移動処理
        // =====================================================

        private void Move()
        {
            Vector3 moveDirection;

            // =====================================================
            // カメラ基準移動
            // =====================================================

            if ((m_UsingGamepadScheme ||
                 m_IsDirectControl) &&
                Camera.main != null)
            {
                // カメラ前方向
                Vector3 camForward =
                    Camera.main.transform.forward;

                camForward.y = 0f;
                camForward.Normalize();

                // カメラ右方向
                Vector3 camRight =
                    Camera.main.transform.right;

                camRight.y = 0f;
                camRight.Normalize();

                // カメラ基準移動方向
                moveDirection =
                    camForward * m_MovementInputValue +
                    camRight * m_TurnInputValue;

                // 斜め移動速度補正
                if (moveDirection.sqrMagnitude > 1f)
                {
                    moveDirection.Normalize();
                }
            }

            // =====================================================
            // タンク前方向移動
            // =====================================================

            else
            {
                moveDirection =
                    transform.forward *
                    m_MovementInputValue;
            }

            // 実際の速度
            Vector3 velocity =
                moveDirection * m_Speed;

            // 爆風ノックバック追加
            velocity += m_ExplosionVelocity;

            // CharacterController移動
            m_CharacterController.Move(
                velocity * Time.deltaTime
            );

            // 爆風減衰
            m_ExplosionVelocity =
                Vector3.Lerp(
                    m_ExplosionVelocity,
                    Vector3.zero,
                    Time.deltaTime * 5f
                );
        }

        // =====================================================
        // 回転処理
        // =====================================================

        private void Turn()
        {
            // =====================================================
            // スティック方向へ向く
            // =====================================================

            if (m_UsingGamepadScheme ||
                m_IsDirectControl)
            {
                // 向く方向
                Vector3 lookDirection =
                    new Vector3(
                        m_TurnInputValue,
                        0f,
                        m_MovementInputValue
                    );

                // 入力無し
                if (lookDirection.sqrMagnitude < 0.001f)
                {
                    return;
                }

                // 目標回転
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        lookDirection,
                        Vector3.up
                    );

                // 徐々に回転
                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        m_TurnSpeed * Time.deltaTime
                    );
            }

            // =====================================================
            // 左右回転
            // =====================================================

            else
            {
                // 回転量
                float turn =
                    m_TurnInputValue *
                    m_TurnSpeed *
                    Time.deltaTime;

                // Y軸回転
                transform.Rotate(
                    0f,
                    turn,
                    0f
                );
            }
        }

        // =====================================================
        // 爆風ノックバック追加
        // =====================================================

        public void AddExplosionForce(
            float explosionForce,
            Vector3 explosionPosition,
            float explosionRadius,
            float upwardsModifier = 0f)
        {
            // 爆風方向
            Vector3 explosionDir =
                transform.position -
                explosionPosition;

            // 距離
            float explosionDistance =
                explosionDir.magnitude;

            // 上方向補正
            if (upwardsModifier != 0f)
            {
                explosionDir.y += upwardsModifier;
            }

            // 正規化
            explosionDir.Normalize();

            // 距離減衰
            float attenuation =
                1f - Mathf.Clamp01(
                    explosionDistance /
                    explosionRadius
                );

            // 爆風速度設定
            m_ExplosionVelocity =
                explosionDir *
                explosionForce *
                attenuation;
        }

        // =====================================================
        // 操作スキーム切り替え
        // =====================================================

        private bool TryActivateScheme(
            string schemeName)
        {
            try
            {
                // スキーム有効化
                m_InputUser.ActivateScheme(
                    schemeName
                );

                return true;
            }
            catch (System.ArgumentException)
            {
                Debug.LogError(
                    $"Control Scheme '{schemeName}' not found."
                );

                return false;
            }
        }
    }
}