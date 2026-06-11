//using Fusion;
//using UnityEngine;

//namespace Tanks.Complete
//{
//    // =====================================================
//    // 他のスクリプトより先に実行
//    // =====================================================
//    [DefaultExecutionOrder(-10)]

//    public class TankMove : NetworkBehaviour
//    {
//        // =====================================================
//        // プレイヤー設定
//        // =====================================================

//        [Header("Player")]

//        // =====================================================
//        // 移動設定
//        // =====================================================

//        [Header("Movement")]
//        // 移動速度
//        public float m_Speed = 12f;
//        // 回転速度
//        public float m_TurnSpeed = 180f;
//        // カメラ方向基準移動
//        public bool m_IsDirectControl = false;

//        [Header("Network")]

//        // 補間係数.
//        private const float m_NetworkSmooth = 10f;

//        // =====================================================
//        // サウンド設定
//        // =====================================================

//        [Header("Audio")]

//        // エンジン音
//        public AudioSource m_MovementAudio;

//        // 停止音
//        public AudioClip m_EngineIdling;

//        // 走行音
//        public AudioClip m_EngineDriving;

//        // 音程ランダム幅
//        public float m_PitchRange = 0.2f;

//        // =====================================================
//        // 操作設定
//        // =====================================================

//        [Header("Control")]

//        // CPU操作か
//        public bool m_IsComputerControlled = false;

//        // 使用する操作番号
//        //public int ControlIndex { get; set; } = -1;

//        // =====================================================
//        // コンポーネント
//        // =====================================================

//        // CharacterController
//        private NetworkCharacterController m_CharacterController;

//        // =====================================================
//        // 入力関連
//        // =====================================================

//        private Vector2 moveInput;  // 入力受け取り用変数.

//        // ゲームパッド方式か
//        //private bool m_UsingGamepadScheme;

//        // =====================================================
//        // 爆風関連
//        // =====================================================

//        // 爆風ノックバック速度
//        private Vector3 m_ExplosionVelocity;

//        // =====================================================
//        // サウンド関連
//        // =====================================================

//        // 元の音程
//        private float m_OriginalPitch;

//        // =====================================================
//        // エフェクト関連
//        // =====================================================

//        // 子Particle一覧
//        private ParticleSystem[] m_particleSystems;

//        // 
//        private Transform cachedTransform;
//        private Transform camTransform;

//        // =====================================================
//        // Awake
//        // =====================================================

//        private void Awake()
//        {
//            cachedTransform = transform;
//            // CharacterController取得
//            m_CharacterController =
//                GetComponent<NetworkCharacterController>();

//            // 無ければ追加
//            if (m_CharacterController == null)
//            {
//                m_CharacterController =
//                    gameObject.AddComponent<NetworkCharacterController>();
//            }
//        }

//        // =====================================================
//        // 有効化時
//        // =====================================================

//        private void OnEnable()
//        {
//            // 爆風初期化
//            m_ExplosionVelocity = Vector3.zero;

//            // Particle取得
//            m_particleSystems =
//                GetComponentsInChildren<ParticleSystem>();

//            // Particle再生
//            for (int i = 0; i < m_particleSystems.Length; ++i)
//            {
//                m_particleSystems[i].Play();
//            }

//            var cam = Camera.main;
//            camTransform = cam != null ? cam.transform : null;
//        }

//        // =====================================================
//        // 無効化時
//        // =====================================================

//        private void OnDisable()
//        {
//            // Particle停止
//            for (int i = 0; i < m_particleSystems.Length; ++i)
//            {
//                m_particleSystems[i].Stop();
//            }
//        }

//        // =====================================================
//        // Start
//        // =====================================================

//        private void Start()
//        {
//            // =====================================================
//            // CPU操作ならAI追加
//            // =====================================================

//            if (m_IsComputerControlled)
//            {
//                var ai = GetComponent<TankAI>();

//                // 無ければ追加
//                if (ai == null)
//                {
//                    gameObject.AddComponent<TankAI>();
//                }

//                return;
//            }

//            // =====================================================
//            // 元の音程保存
//            // =====================================================

//            if (m_MovementAudio)
//            {
//                m_OriginalPitch =
//                    m_MovementAudio.pitch;
//            }

//            if (camTransform == null)
//            {
//                var cam = Camera.main;
//                camTransform = cam != null ? cam.transform : null;
//            }
//        }

//        public override void Spawned()
//        {
//            if (!Object.HasInputAuthority)
//                return;

//            camTransform = Camera.main.transform;
//        }

//        // TankVehicleのFixedUpdateNetworkから呼び出される移動処理.
//        public void TickMove(float deltaTime)
//        {
//            // StateAuthority(ホスト)が移動処理を実行してネットワーク値を書き込む.
//            if (Object.HasStateAuthority)
//            {
//                if (m_MovementAudio) EngineAudio();

//                //Turn(deltaTime);
//                Move(deltaTime);
//            }

//            Debug.Log(
//                $"Pos={m_CharacterController.transform.position} " +
//                $"Rot={transform.eulerAngles.y}"
//            );
//        }

//        // =====================================================
//        // エンジン音処理
//        // =====================================================

//        private void EngineAudio()
//        {
//            // 動いているか
//            bool isMoving =
//                Mathf.Abs(moveInput.y) > 0.1f ||
//                Mathf.Abs(moveInput.x) > 0.1f;

//            // 停止中
//            if (!isMoving)
//            {
//                if (m_MovementAudio.clip ==
//                    m_EngineDriving)
//                {
//                    // 停止音へ
//                    m_MovementAudio.clip =
//                        m_EngineIdling;

//                    // ランダム音程
//                    m_MovementAudio.pitch =
//                        Random.Range(
//                            m_OriginalPitch - m_PitchRange,
//                            m_OriginalPitch + m_PitchRange
//                        );

//                    // 再生
//                    m_MovementAudio.Play();
//                }
//            }

//            // 移動中
//            else
//            {
//                if (m_MovementAudio.clip ==
//                    m_EngineIdling)
//                {
//                    // 走行音へ
//                    m_MovementAudio.clip =
//                        m_EngineDriving;

//                    // ランダム音程
//                    m_MovementAudio.pitch =
//                        Random.Range(
//                            m_OriginalPitch - m_PitchRange,
//                            m_OriginalPitch + m_PitchRange
//                        );

//                    // 再生
//                    m_MovementAudio.Play();
//                }
//            }
//        }

//        public void SetMoveInput(Vector2 input)
//        {
//            if (input.sqrMagnitude > 1f) input.Normalize();
//            moveInput = input;
//        }

//        // =====================================================
//        // 移動処理
//        // =====================================================

//        private void Move(float deltaTime)
//        {
//            Debug.Log($"MoveCall {Object.HasStateAuthority}");

//            if (moveInput.sqrMagnitude < 0.001f && m_ExplosionVelocity.sqrMagnitude < 0.001f)
//            {
//                return;
//            }

//            // 移動方向
//            Vector3 moveDirection;

//            // =====================================================
//            // カメラ方向基準
//            // =====================================================

//            if (m_IsDirectControl && camTransform != null)
//            {
//                // カメラ前方向
//                Vector3 camForward =
//                    camTransform.forward;
//                camForward.y = 0f;
//                camForward.Normalize();

//                // カメラ右方向
//                Vector3 camRight =
//                    camTransform.right;
//                camRight.y = 0f;
//                camRight.Normalize();

//                // 移動方向
//                moveDirection =
//                    camForward * moveInput.y +
//                    camRight * moveInput.x;

//                // 斜め補正
//                if (moveDirection.sqrMagnitude > 1f)
//                {
//                    moveDirection.Normalize();
//                }

//            }
//            else
//            {
//                moveDirection =
//                    cachedTransform.forward *
//                    moveInput.y;
//            }


//            // 実際の速度
//            Vector3 velocity =
//                moveDirection * m_Speed;

//            // 爆風追加
//            velocity += m_ExplosionVelocity;

//            Debug.Log($"BeforeMove {transform.position}");

//            // 移動
//            m_CharacterController.Move(
//                velocity * deltaTime
//            );

//            Debug.Log($"CC Pos={m_CharacterController.transform.position}");

//            Debug.Log($"AfterMove {transform.position}");

//            // 爆風減衰
//            m_ExplosionVelocity =
//                Vector3.Lerp(
//                    m_ExplosionVelocity,
//                    Vector3.zero,
//                    deltaTime * 5f
//                );

//            Debug.Log($"MoveDir={moveDirection}");
//            Debug.Log($"Velocity={velocity}");
//            Debug.Log($"Delta={velocity * deltaTime}");
//        }

//        // =====================================================
//        // 回転処理
//        //// =====================================================
//        private void Turn(float deltaTime)
//        {
//            const float deadZone = 0.05f;

//            float forward = moveInput.y;
//            float steer = moveInput.x;

//            // カメラ基準移動時はカメラ方向を考慮した移動ベクトルを算出（ただしLookRotationは前進時のみ使用）
//            if (m_IsDirectControl && camTransform != null)
//            {
//                Vector3 camForward = camTransform.forward; camForward.y = 0f; camForward.Normalize();
//                Vector3 camRight = camTransform.right; camRight.y = 0f; camRight.Normalize();
//                Vector3 desireDir = camForward * forward + camRight * steer;

//                desireDir.y = 0f;

//                if (desireDir.sqrMagnitude > 0.01f)
//                {
//                    Quaternion targetRotation = Quaternion.LookRotation(desireDir.normalized, Vector3.up);

//                    Quaternion nextRotation = Quaternion.RotateTowards(cachedTransform.rotation, targetRotation, m_TurnSpeed * deltaTime);

//                    cachedTransform.rotation = nextRotation;
//                }

//                return;
//            }

//            if (Mathf.Abs(steer) < deadZone) return;

//            if (forward < 0f)
//            {
//                steer *= -1f;
//            }

//            float turnamount = steer * m_TurnSpeed * deltaTime;

//            Quaternion nextRot = cachedTransform.rotation * Quaternion.Euler(0f, turnamount, 0f);

//            cachedTransform.rotation = nextRot;

//        }

//        // =====================================================
//        // 爆風追加
//        // =====================================================

//        public void AddExplosionForce(
//            float explosionForce,
//            Vector3 explosionPosition,
//            float explosionRadius,
//            float upwardsModifier = 0f)
//        {
//            // 爆風方向
//            Vector3 explosionDir =
//                cachedTransform.position -
//                explosionPosition;

//            // 距離
//            float explosionDistance =
//                explosionDir.magnitude;

//            // 上方向補正
//            if (upwardsModifier != 0f)
//            {
//                explosionDir.y += upwardsModifier;
//            }

//            // 正規化
//            explosionDir.Normalize();

//            // 距離減衰
//            float attenuation =
//                1f - Mathf.Clamp01(
//                    explosionDistance /
//                    explosionRadius
//                );

//            // 爆風速度
//            m_ExplosionVelocity =
//                explosionDir *
//                explosionForce *
//                attenuation;
//        }
//    }
//}

using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class TankMove : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 12f;
        [SerializeField] float turnSpeed = 180f;

        Rigidbody rb;

        [Networked] float throttle { get; set; }
        [Networked] float steering {  get; set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        // TankVehicleのSetInputから呼び出される移動入力のセット処理.
        public void SetMoveInput(float throttleInput, float steeringInput)
        {
            throttle = throttleInput;
            steering = steeringInput;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            Move();
            Rotate();

            // 旧式の移動処理（positionとrotationを直接いじってるので非推奨）.
            //transform.Rotate(0, steering * turnSpeed * Runner.DeltaTime, 0);

            //transform.position += 
            //    transform.forward *
            //    throttle *
            //    moveSpeed *
            //    Runner.DeltaTime;
        }

        void Rotate()
        {
            float turn = steering * turnSpeed * Runner.DeltaTime;

            Quaternion rotation = Quaternion.Euler(0, turn, 0);

            rb.MoveRotation(rb.rotation * rotation);
        }

        void Move()
        {
            Vector3 velocity = transform.forward * throttle * moveSpeed * Runner.DeltaTime;

            rb.MovePosition(rb.position + velocity);
        }
    }
}