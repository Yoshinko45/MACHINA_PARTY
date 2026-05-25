using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    // =====================================================
    // ■ 他のスクリプトより先に実行
    // =====================================================
    [DefaultExecutionOrder(-10)]

    public class TankMovement : MonoBehaviour
    {
        // =====================================================
        // ■ プレイヤー設定
        // =====================================================

        [Header("Player")]

        // デバッグ確認用
        public int m_PlayerNumber = 1;


        // =====================================================
        // ■ 移動設定
        // =====================================================

        [Header("Movement")]

        // 移動速度
        public float m_Speed = 12f;

        // 回転速度
        public float m_TurnSpeed = 180f;

        // カメラ方向移動を強制するか
        public bool m_IsDirectControl;


        // =====================================================
        // ■ サウンド設定
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
        // ■ 操作設定
        // =====================================================

        [Header("Control")]

        // CPU操作か
        public bool m_IsComputerControlled = false;


        // =====================================================
        // ■ コンポーネント
        // =====================================================

        // CharacterController
        private CharacterController m_CharacterController;

        // 入力管理
        private InputReader m_InputReader;


        // =====================================================
        // ■ 入力関連
        // =====================================================

        // 前後入力
        private float m_MovementInputValue;

        // 左右入力
        private float m_TurnInputValue;

        // ゲームパッド操作か
        private bool m_UsingGamepadScheme;


        // =====================================================
        // ■ 爆風関連
        // =====================================================

        // 爆風ノックバック
        private Vector3 m_ExplosionVelocity;


        // =====================================================
        // ■ サウンド関連
        // =====================================================

        // 元の音程
        private float m_OriginalPitch;


        // =====================================================
        // ■ エフェクト関連
        // =====================================================

        // 子Particle一覧
        private ParticleSystem[] m_particleSystems;


        // =====================================================
        // ■ Awake
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


            // =================================================
            // InputReader取得
            // =================================================

            m_InputReader =
                GetComponent<InputReader>();


            // 無ければエラー
            if (m_InputReader == null)
            {
                Debug.LogError(
                    gameObject.name +
                    " に InputReader がありません"
                );
            }
        }


        // =====================================================
        // ■ 有効化時
        // =====================================================

        private void OnEnable()
        {
            // 入力初期化
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;

            // 爆風初期化
            m_ExplosionVelocity = Vector3.zero;


            // =================================================
            // Particle取得
            // =================================================

            m_particleSystems =
                GetComponentsInChildren<ParticleSystem>();


            // Particle再生
            for (int i = 0; i < m_particleSystems.Length; ++i)
            {
                m_particleSystems[i].Play();
            }
        }


        // =====================================================
        // ■ 無効化時
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
        // ■ Start
        // =====================================================

        private void Start()
        {
            // =================================================
            // CPU操作ならAI追加
            // =================================================

            if (m_IsComputerControlled)
            {
                TankAI ai =
                    GetComponent<TankAI>();


                // 無ければ追加
                if (ai == null)
                {
                    gameObject.AddComponent<TankAI>();
                }

                return;
            }


            // =================================================
            // 元の音程保存
            // =================================================

            if (m_MovementAudio != null)
            {
                m_OriginalPitch =
                    m_MovementAudio.pitch;
            }
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            // =================================================
            // CPUでなければ入力取得
            // =================================================

            if (!m_IsComputerControlled)
            {
                // InputReader存在確認
                if (m_InputReader != null)
                {
                    // 前後入力
                    m_MovementInputValue =
                        m_InputReader.MoveInput.y;

                    // 左右入力
                    m_TurnInputValue =
                        m_InputReader.MoveInput.x;

                    // ゲームパッド操作か
                    m_UsingGamepadScheme =
                        m_InputReader.IsUsingGamepad;
                }
            }


            // =================================================
            // エンジン音更新
            // =================================================

            if (m_MovementAudio != null)
            {
                EngineAudio();
            }


            // 移動
            Move();

            // 回転
            Turn();
        }


        // =====================================================
        // ■ エンジン音処理
        // =====================================================

        private void EngineAudio()
        {
            // 動いているか
            bool isMoving =
                Mathf.Abs(m_MovementInputValue) > 0.1f ||
                Mathf.Abs(m_TurnInputValue) > 0.1f;


            // =================================================
            // 停止中
            // =================================================

            if (!isMoving)
            {
                if (m_MovementAudio.clip ==
                    m_EngineDriving)
                {
                    // 停止音へ変更
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


            // =================================================
            // 移動中
            // =================================================

            else
            {
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
        // ■ 移動処理
        // =====================================================

        private void Move()
        {
            // 移動方向
            Vector3 moveDirection;


            // =================================================
            // カメラ方向基準移動
            // =================================================

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


                // 入力方向
                moveDirection =
                    camForward * m_MovementInputValue +
                    camRight * m_TurnInputValue;


                // 斜め移動補正
                if (moveDirection.sqrMagnitude > 1f)
                {
                    moveDirection.Normalize();
                }


                // =================================================
                // 移動方向を向く
                // =================================================

                if (moveDirection != Vector3.zero)
                {
                    Quaternion targetRotation =
                        Quaternion.LookRotation(
                            moveDirection
                        );

                    transform.rotation =
                        Quaternion.RotateTowards(
                            transform.rotation,
                            targetRotation,
                            m_TurnSpeed * Time.deltaTime
                        );
                }
            }


            // =================================================
            // 通常移動
            // =================================================

            else
            {
                moveDirection =
                    transform.forward *
                    m_MovementInputValue;
            }


            // =================================================
            // 実際の速度
            // =================================================

            Vector3 velocity =
                moveDirection * m_Speed;


            // 爆風追加
            velocity += m_ExplosionVelocity;


            // 実際に移動
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
        // ■ 回転処理
        // =====================================================

        private void Turn()
        {
            // =================================================
            // カメラ方向移動時は回転しない
            // =================================================

            if (m_UsingGamepadScheme ||
                m_IsDirectControl)
            {
                return;
            }


            // =================================================
            // 通常タンク回転
            // =================================================

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


        // =====================================================
        // ■ 爆風追加
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