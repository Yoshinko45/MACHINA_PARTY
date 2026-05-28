using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // 他のスクリプトより先に実行
    // =====================================================
    [DefaultExecutionOrder(-10)]

    public class TankMove : NetworkBehaviour
    {
        // =====================================================
        // プレイヤー設定
        // =====================================================

        [Header("Player")]

        // プレイヤー番号
        // 1 = WASD
        // 2 = 矢印キー
        //public int m_PlayerNumber = 1;

        // =====================================================
        // 移動設定
        // =====================================================

        [Header("Movement")]

        // 移動速度
        public float m_Speed = 12f;

        // 回転速度
        public float m_TurnSpeed = 180f;

        // カメラ方向基準移動
        public bool m_IsDirectControl;

        // =====================================================
        // サウンド設定
        // =====================================================

        [Header("Audio")]

        // エンジン音
        public AudioSource m_MovementAudio;

        // 停止音
        public AudioClip m_EngineIdling;

        // 走行音
        public AudioClip m_EngineDriving;

        // 音程ランダム幅
        public float m_PitchRange = 0.2f;

        // =====================================================
        // 操作設定
        // =====================================================

        [Header("Control")]

        // CPU操作か
        public bool m_IsComputerControlled = false;

        // 使用する操作番号
        //public int ControlIndex { get; set; } = -1;

        // =====================================================
        // コンポーネント
        // =====================================================

        // CharacterController
        private NetworkCharacterController m_CharacterController;

        // =====================================================
        // 入力関連
        // =====================================================

        private Vector2 moveInput;  // 入力受け取り用変数.

        // ゲームパッド方式か
        //private bool m_UsingGamepadScheme;

        // =====================================================
        // 爆風関連
        // =====================================================

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

        // 子Particle一覧
        private ParticleSystem[] m_particleSystems;

        // =====================================================
        // Awake
        // =====================================================

        private void Awake()
        {
            // CharacterController取得
            m_CharacterController =
                GetComponent<NetworkCharacterController>();

            // 無ければ追加
            if (m_CharacterController == null)
            {
                m_CharacterController =
                    gameObject.AddComponent<NetworkCharacterController>();
            }
        }

        // =====================================================
        // 有効化時
        // =====================================================

        private void OnEnable()
        {
            // 爆風初期化
            m_ExplosionVelocity = Vector3.zero;

            // Particle取得
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

                // 無ければ追加
                if (ai == null)
                {
                    gameObject.AddComponent<TankAI>();
                }

                return;
            }

            // =====================================================
            // 操作番号設定
            // =====================================================

            //if (ControlIndex == -1)
            //{
            //    ControlIndex = m_PlayerNumber;
            //}

            // =====================================================
            // 元の音程保存
            // =====================================================

            if (m_MovementAudio)
            {
                m_OriginalPitch =
                    m_MovementAudio.pitch;
            }
        }
        public void TickMove(float deltaTime)
        {
            if (m_MovementAudio)
            {
                EngineAudio();
            }

            Move(deltaTime);
            Turn(deltaTime);
        }

        // =====================================================
        // エンジン音処理
        // =====================================================

        private void EngineAudio()
        {
            // 動いているか
            bool isMoving =
                Mathf.Abs(moveInput.y) > 0.1f ||
                Mathf.Abs(moveInput.x) > 0.1f;

            // 停止中
            if (!isMoving)
            {
                if (m_MovementAudio.clip ==
                    m_EngineDriving)
                {
                    // 停止音へ
                    m_MovementAudio.clip =
                        m_EngineIdling;

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

            // 移動中
            else
            {
                if (m_MovementAudio.clip ==
                    m_EngineIdling)
                {
                    // 走行音へ
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

        public void SetMoveInput(Vector2 input)
        {
            moveInput = input;
        }

        // =====================================================
        // 移動処理
        // =====================================================

        private void Move(float deltaTime)
        {
            // 移動方向
            Vector3 moveDirection;

            // =====================================================
            // カメラ方向基準
            // =====================================================

            if (m_IsDirectControl &&
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

                // 移動方向
                moveDirection =
                    camForward * moveInput.y +
                    camRight * moveInput.x;

                // 斜め補正
                if (moveDirection.sqrMagnitude > 1f)
                {
                    moveDirection.Normalize();
                }
            }

            // =====================================================
            // タンク前方向
            // =====================================================

            else
            {
                moveDirection =
                    transform.forward *
                    moveInput.y;
            }

            // 実際の速度
            Vector3 velocity =
                moveDirection * m_Speed;

            // 爆風追加
            velocity += m_ExplosionVelocity;

            // 移動
            m_CharacterController.Move(
                velocity * deltaTime
            );

            // 爆風減衰
            m_ExplosionVelocity =
                Vector3.Lerp(
                    m_ExplosionVelocity,
                    Vector3.zero,
                    deltaTime * 5f
                );
        }

        // =====================================================
        // 回転処理
        // =====================================================

        private void Turn(float deltaTime)
        {
            float turnDirection = moveInput.x;

            if (moveInput.y < -0.1f)
            {
                turnDirection *= -1f;
            }

            // 回転量
            float turn =
                turnDirection *
                m_TurnSpeed *
                deltaTime;

            // Y軸回転
            //transform.Rotate(
            //    0f,
            //    turn,
            //    0f
            //);

            Quaternion targetRotation =
                transform.rotation *
                Quaternion.Euler(0f, turn, 0f);

            m_CharacterController.Teleport(
                transform.position,
                targetRotation
            );
        }

        // =====================================================
        // 爆風追加
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

            // 爆風速度
            m_ExplosionVelocity =
                explosionDir *
                explosionForce *
                attenuation;
        }
    }
}