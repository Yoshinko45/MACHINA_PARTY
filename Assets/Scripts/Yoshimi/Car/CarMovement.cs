using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ CarMovement
    // =====================================================
    //
    // レースゲーム専用の車両移動クラス。
    //
    // Rigidbodyを利用して車両を制御する。
    // CharacterControllerは使用しない。
    //
    // 主な機能
    // ・アクセル
    // ・ブレーキ
    // ・自然減速
    // ・ドリフト
    // ・ブースト
    // ・芝生減速
    // ・壁衝突減速
    //
    // =====================================================

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(InputReader))]
    public class CarMovement : MonoBehaviour
    {
        // =====================================================
        // ■ プレイヤー設定
        // =====================================================

        [Header("Player")]

        // プレイヤー番号
        public int m_PlayerNumber = 1;

        // CPU操作か
        public bool m_IsComputerControlled;


        // =====================================================
        // ■ 移動設定
        // =====================================================

        [Header("Movement")]

        // 通常最高速度
        public float m_MaxSpeed = 20f;

        // 加速度
        public float m_Acceleration = 18f;

        // ブレーキ力
        public float m_BrakePower = 35f;

        // アクセルを離した時の減速度
        public float m_CoastDeceleration = 8f;

        // ハンドル速度
        public float m_TurnSpeed = 120f;

        // バック時の最高速度倍率
        [Range(0.1f, 1f)]
        public float m_ReverseSpeedMultiplier = 0.45f;


        // =====================================================
        // ■ ドリフト設定
        // =====================================================

        [Header("Drift")]

        // ドリフト中の旋回倍率
        public float m_DriftTurnMultiplier = 1.4f;

        // ドリフト可能になる最低速度
        public float m_DriftStartSpeed = 10f;

        // ドリフト中か
        public bool IsDrifting
        {
            get { return m_IsDrifting; }
        }

        // =====================================================
        // ■ ブースト設定
        // =====================================================

        [Header("Boost")]

        // ブースト時の最高速度倍率
        public float m_BoostMultiplier = 1.5f;

        // ブースト継続時間
        public float m_BoostTime = 3f;

        // ブースト中か
        public bool IsBoosting
        {
            get { return m_IsBoosting; }
        }


        // =====================================================
        // ■ 状態
        // =====================================================

        // 現在速度
        private float m_CurrentSpeed;

        // 前後入力
        private float m_MoveInput;

        // 左右入力
        private float m_TurnInput;

        // ドリフト入力
        private bool m_DriftInput;

        // ドリフト中か
        private bool m_IsDrifting;

        // ブースト中か
        private bool m_IsBoosting;

        // 芝生上か
        private bool m_IsOnGrass;


        // =====================================================
        // ■ コンポーネント
        // =====================================================

        // Rigidbody
        private Rigidbody m_Rigidbody;

        // 入力取得クラス
        private InputReader m_InputReader;


        // =====================================================
        // ■ Awake
        // =====================================================
        //
        // 必要なコンポーネント取得。
        // Rigidbodyの初期設定もここで行う。
        //
        // =====================================================

        private void Awake()
        {
            // Rigidbody取得
            m_Rigidbody =
                GetComponent<Rigidbody>();


            // InputReader取得
            m_InputReader =
                GetComponent<InputReader>();


            // =================================================
            // Rigidbody設定
            // =================================================

            // 重力を使用
            m_Rigidbody.useGravity = true;

            // 描画補間
            m_Rigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            // 高速移動時のすり抜け防止
            m_Rigidbody.collisionDetectionMode =
                CollisionDetectionMode.Continuous;

            // 横転・前転防止
            m_Rigidbody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }


        // =====================================================
        // ■ Start
        // =====================================================
        //
        // ゲーム開始時の初期化。
        //
        // =====================================================

        private void Start()
        {
            // 初期速度
            m_CurrentSpeed = 0f;

            // 初期状態ではコース外扱い
            m_IsOnGrass = true;
        }


        // =====================================================
        // ■ Update
        // =====================================================
        //
        // 毎フレーム入力を取得する。
        // Rigidbody処理はFixedUpdateで行う。
        //
        // =====================================================

        private void Update()
        {
            // CPUなら入力取得しない
            if (m_IsComputerControlled)
            {
                return;
            }

            // InputReader未設定
            if (m_InputReader == null)
            {
                return;
            }

            // =================================================
            // 入力取得
            // =================================================

            // 前後入力
            m_MoveInput =
                m_InputReader.MoveInput.y;

            // 左右入力
            m_TurnInput =
                m_InputReader.MoveInput.x;

            // ドリフト入力
            m_DriftInput = m_InputReader.DriftHeld;
        }

        // =====================================================
        // ■ FixedUpdate
        // =====================================================
        //
        // Rigidbodyの物理処理を実行する。
        //
        // 処理順
        // ①ドリフト更新
        // ②移動
        // ③回転
        //
        // =====================================================

        private void FixedUpdate()
        {
            // ドリフト状態更新
            UpdateDrift();

            // 車両移動
            Move();

            // 車両回転
            Turn();
        }

        // =====================================================
        // ■ 移動処理
        // =====================================================
        //
        // アクセル・ブレーキ入力から現在速度を計算し、
        // Rigidbodyへ速度を反映する。
        //
        // 芝生やブーストによる最高速度補正もここで行う。
        //
        // =====================================================

        private void Move()
        {
            //=====================================================
            // 現在の最高速度
            //=====================================================

            float maxSpeed =
                m_MaxSpeed;


            // 芝生では速度半減
            if (m_IsOnGrass)
            {
                maxSpeed *= 0.5f;
            }


            // ブースト中
            if (m_IsBoosting)
            {
                maxSpeed *=
                    m_BoostMultiplier;
            }


            //=====================================================
            // アクセル
            //=====================================================

            if (m_MoveInput > 0f)
            {
                m_CurrentSpeed =
                    Mathf.MoveTowards(
                        m_CurrentSpeed,
                        maxSpeed,
                        m_Acceleration *
                        Time.fixedDeltaTime
                    );
            }


            //=====================================================
            // バック
            //=====================================================

            else if (m_MoveInput < 0f)
            {
                float reverseSpeed =
                    -maxSpeed *
                    m_ReverseSpeedMultiplier;

                m_CurrentSpeed =
                    Mathf.MoveTowards(
                        m_CurrentSpeed,
                        reverseSpeed,
                        m_BrakePower *
                        Time.fixedDeltaTime
                    );
            }


            //=====================================================
            // 自然減速
            //=====================================================

            else
            {
                m_CurrentSpeed =
                    Mathf.MoveTowards(
                        m_CurrentSpeed,
                        0f,
                        m_CoastDeceleration *
                        Time.fixedDeltaTime
                    );
            }


            //=====================================================
            // Rigidbodyへ速度反映
            //=====================================================

            Vector3 velocity =
                transform.forward *
                m_CurrentSpeed;

            // 重力だけ維持
            velocity.y =
                m_Rigidbody.linearVelocity.y;

            m_Rigidbody.linearVelocity =
                velocity;
        }


        // =====================================================
        // ■ 回転処理
        // =====================================================
        //
        // 現在速度に応じてハンドルの効きを変更する。
        //
        // ドリフト中は旋回性能を上げる。
        //
        // =====================================================

        private void Turn()
        {
            // 停止中は回転しない
            if (Mathf.Abs(m_CurrentSpeed) < 0.05f)
            {
                return;
            }


            //=====================================================
            // 現在速度割合
            //=====================================================

            float speedRatio =
                Mathf.Clamp01(
                    Mathf.Abs(m_CurrentSpeed) /
                    m_MaxSpeed
                );


            //=====================================================
            // 低速でも少しだけ曲がれるようにする
            //=====================================================

            float steerPower =
                Mathf.Lerp(
                    0.3f,
                    1f,
                    speedRatio
                );


            //=====================================================
            // ドリフト中
            //=====================================================

            if (m_IsDrifting)
            {
                steerPower *=
                    m_DriftTurnMultiplier;
            }


            //=====================================================
            // バック時は逆ハンドル
            //=====================================================

            float direction =
                m_CurrentSpeed >= 0f
                ? 1f
                : -1f;


            float turn =
                m_TurnInput *
                m_TurnSpeed *
                steerPower *
                direction *
                Time.fixedDeltaTime;


            Quaternion rotation =
                Quaternion.Euler(
                    0f,
                    turn,
                    0f
                );


            m_Rigidbody.MoveRotation(
                m_Rigidbody.rotation *
                rotation
            );
        }


        // =====================================================
        // ■ ドリフト更新
        // =====================================================
        //
        // 一定速度以上でドリフトボタンを押すと
        // ドリフト状態へ移行する。
        //
        // ボタンを離すと解除。
        //
        // =====================================================

        private void UpdateDrift()
        {
            // ドリフト開始条件
            if (m_DriftInput &&
                Mathf.Abs(m_CurrentSpeed) >=
                m_DriftStartSpeed)
            {
                m_IsDrifting = true;
            }
            else
            {
                m_IsDrifting = false;
            }
        }


        // =====================================================
        // ■ ブースト開始
        // =====================================================
        //
        // アイテム取得などから呼び出す。
        //
        // =====================================================

        public void StartBoost()
        {
            // 既にブースト中なら無視
            if (m_IsBoosting)
            {
                return;
            }

            StartCoroutine(
                BoostRoutine()
            );
        }


        // =====================================================
        // ■ ブースト処理
        // =====================================================
        //
        // 一定時間だけ最高速度を上昇させる。
        //
        // =====================================================

        private System.Collections.IEnumerator
            BoostRoutine()
        {
            m_IsBoosting = true;

            yield return new WaitForSeconds(
                m_BoostTime
            );

            m_IsBoosting = false;
        }


        // =====================================================
        // ■ ブースト停止
        // =====================================================
        //
        // 強制的にブーストを終了する。
        //
        // =====================================================

        public void StopBoost()
        {
            m_IsBoosting = false;
        }

                // =====================================================
        // ■ 衝突開始
        // =====================================================
        //
        // 壁に衝突した際に速度を減少させる。
        //
        // =====================================================

        private void OnCollisionEnter(Collision collision)
        {
            // 壁に衝突した場合
            if (collision.collider.CompareTag("Wall"))
            {
                // 現在速度を半減
                m_CurrentSpeed *= 0.5f;
            }
        }


        // =====================================================
        // ■ 接触中
        // =====================================================
        //
        // 現在走行している地面を判定する。
        //
        // Trackタグ
        //     通常速度
        //
        // Groundタグ
        //     芝生扱い
        //
        // Trackを最優先で判定することで、
        // Groundとの二重判定による誤動作を防ぐ。
        //
        // =====================================================

        private void OnCollisionStay(Collision collision)
        {
            // コース上
            if (collision.collider.CompareTag("Track"))
            {
                m_IsOnGrass = false;
                return;
            }

            // 芝生
            if (collision.collider.CompareTag("Ground"))
            {
                m_IsOnGrass = true;
            }
        }


        // =====================================================
        // ■ 接触終了
        // =====================================================
        //
        // コースから離れた場合は
        // 芝生扱いに戻す。
        //
        // =====================================================

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider.CompareTag("Track"))
            {
                m_IsOnGrass = true;
            }
        }


        // =====================================================
        // ■ 現在速度取得
        // =====================================================
        //
        // UIやカメラなど他スクリプトから
        // 現在速度を取得するためのプロパティ。
        //
        // 常に正の値を返す。
        //
        // =====================================================

        public float CurrentSpeed
        {
            get
            {
                return Mathf.Abs(
                    m_CurrentSpeed
                );
            }
        }


        // =====================================================
        // ■ 停止
        // =====================================================
        //
        // ゴール演出やゲーム終了時などで使用する。
        //
        // 車両の移動・回転を完全に停止する。
        //
        // =====================================================

        public void StopCar()
        {
            // 内部速度初期化
            m_CurrentSpeed = 0f;

            // Rigidbody速度停止
            m_Rigidbody.linearVelocity =
                Vector3.zero;

            // 回転停止
            m_Rigidbody.angularVelocity =
                Vector3.zero;

            // 入力値初期化
            m_MoveInput = 0f;
            m_TurnInput = 0f;

            // ドリフト解除
            m_IsDrifting = false;

            // ブースト解除
            m_IsBoosting = false;
        }
    }
}