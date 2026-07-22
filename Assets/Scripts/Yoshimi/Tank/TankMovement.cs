using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    // =====================================================
    // ■ 他のスクリプトより先に実行
    // =====================================================
    // InputReaderより少し後、
    // 他の処理より先に動かしたい場合に使用
    [DefaultExecutionOrder(-10)]

    public class TankMovement : MonoBehaviour
    {
        // =====================================================
        // ■ 移動モード
        // =====================================================
        //
        // TankMovementは
        // タンクバトルモードと
        // レースモードの両方に対応している。
        //
        // TankBattle
        //     従来の戦車操作
        //
        // Race
        //     車のような加速方式
        //
        public enum MovementProfile
        {
            TankBattle // タンクvsタンクモード
        }

        // =====================================================
        // ■ プレイヤー設定
        // =====================================================

        [Header("Player")]

        // デバッグ確認用プレイヤー番号
        // Inspectorで誰のタンクか確認しやすくする
        public int m_PlayerNumber = 1;


        // =====================================================
        // ■ 移動設定
        // =====================================================

        [Header("Movement")]

        public MovementProfile m_MovementProfile =
            MovementProfile.TankBattle;

        // タンク移動速度
        public float m_Speed = 12f;

        // タンク回転速度
        public float m_TurnSpeed = 180f;

        // trueならカメラ方向基準で移動する
        // TPS風操作になる
        public bool m_IsDirectControl;

        // 右スティックの向き入力を
        // 有効とみなす最小の倒し具合
        // これより小さい間は今まで通り
        // 移動方向を向く
        [Range(0f, 1f)]
        public float m_LookDeadzone = 0.3f;

        // 右スティックの向きに合わせる速さ（度/秒）
        // 大きいほど素早く向きが追従する
        // （m_TurnSpeedとは別。こちらは右スティック専用）
        public float m_LookTurnSpeed = 720f;

        // =====================================================
        // ■ サウンド設定
        // =====================================================

        [Header("Audio")]

        // エンジン音再生用AudioSource
        public AudioSource m_MovementAudio;

        // 停止中エンジン音
        public AudioClip m_EngineIdling;

        // 移動中エンジン音
        public AudioClip m_EngineDriving;

        // 音程ランダム幅
        // 毎回少し音を変えて自然にする
        public float m_PitchRange = 0.2f;


        // =====================================================
        // ■ 操作設定
        // =====================================================

        [Header("Control")]

        // CPU操作かどうか
        // trueならTankAIで動く
        public bool m_IsComputerControlled = false;


        // =====================================================
        // ■ コンポーネント参照
        // =====================================================

        // CharacterController
        // Rigidbodyを使わず移動制御する
        private CharacterController m_CharacterController;

        // 入力管理クラス
        private InputReader m_InputReader;

        // HP管理
        private TankHealth m_TankHealth;

        // =====================================================
        // ■ 入力値
        // =====================================================

        // 前後入力
        // W/S や スティック上下
        private float m_MovementInputValue;

        // 左右入力
        // A/D や スティック左右
        private float m_TurnInputValue;

        // 向き入力（右スティック）
        // ゲームパッド時、移動方向と別に
        // 向きたい方向を指定する
        private Vector2 m_LookInputValue;

        // ゲームパッド操作中か
        // trueならカメラ方向移動を使用
        private bool m_UsingGamepadScheme;

        // =====================================================
        // ■ 爆風関連
        // =====================================================

        // 爆風ノックバック速度
        // ShellExplosionから加算される
        private Vector3 m_ExplosionVelocity;


        // =====================================================
        // ■ サウンド関連
        // =====================================================

        // 元の音程
        // ランダム音程計算に使用
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
            // =================================================
            // CharacterController取得
            // =================================================

            m_CharacterController =
                GetComponent<CharacterController>();


            // 無ければ自動追加
            if (m_CharacterController == null)
            {
                m_CharacterController =
                    gameObject.AddComponent<CharacterController>();
            }


            // =================================================
            // CharacterController設定
            // =================================================

            // 段差乗り越え高さ
            m_CharacterController.stepOffset = 0.15f;

            // 登れる坂の最大角度
            m_CharacterController.slopeLimit = 35f;

            // 壁めり込み防止
            m_CharacterController.skinWidth = 0.1f;

            // 最小移動距離
            m_CharacterController.minMoveDistance = 0f;


            // =================================================
            // カプセル下端の地面めり込み補正
            // =================================================
            //
            // CharacterControllerのカプセルは、実際の高さが
            // 「最低でも半径×2」に内部で拡張される。
            // （例：Height=2でもRadius=1.5なら実質高さ3のカプセル）
            //
            // そのためCenter.Yが「半径」や「高さの半分」より小さいと、
            // カプセル下端がタンクの原点（＝地面）より下へ突き抜ける。
            // このゲームはFixPosition()で毎フレームY=0に固定するので
            // めり込みが解消されず、Move()が毎フレーム地面と衝突して
            // 移動距離のほとんどが食われてしまう（＝異常に遅くなる）。
            //
            // HeavyTank（Radius=1.5, Center.Y=1 → 下端-0.5）が
            // まさにこの状態だったため、下端がちょうど0（接地）に
            // なるようCenterを自動補正する。
            //
            // =================================================

            float capsuleHalfExtent =
                Mathf.Max(
                    m_CharacterController.radius,
                    m_CharacterController.height * 0.5f
                );

            Vector3 capsuleCenter =
                m_CharacterController.center;

            if (capsuleCenter.y < capsuleHalfExtent)
            {
                Debug.LogWarning(
                    gameObject.name +
                    " : CharacterControllerのカプセル下端が原点より " +
                    (capsuleHalfExtent - capsuleCenter.y) +
                    " 下に突き抜けていたため、Center.Y を " +
                    capsuleCenter.y + " → " + capsuleHalfExtent +
                    " に自動補正しました。" +
                    "Prefab側のCenter/Radius/Heightを修正すると" +
                    "この警告は出なくなります。"
                );

                capsuleCenter.y = capsuleHalfExtent;

                m_CharacterController.center = capsuleCenter;
            }


            // =================================================
            // InputReader取得
            // =================================================

            m_InputReader =
                GetComponent<InputReader>();

                // TankHealth取得
            m_TankHealth =
                GetComponent<TankHealth>();

            // 無ければエラー表示
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
            m_LookInputValue = Vector2.zero;

            // 爆風速度初期化
            m_ExplosionVelocity = Vector3.zero;

            // =================================================
            // Particle取得
            // =================================================
            m_particleSystems =
                GetComponentsInChildren<ParticleSystem>();

            // =================================================
            // Particle再生
            // =================================================

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


                // AIが無ければ追加
                if (ai == null)
                {
                    gameObject.AddComponent<TankAI>();
                }

                return;
            }


            // =================================================
            // 元音程保存
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
            // プレイヤー操作なら入力取得
            // =================================================

            if (!m_IsComputerControlled)
            {
                if (m_InputReader != null)
                {
                    // 前後入力
                    m_MovementInputValue =
                        m_InputReader.MoveInput.y;

                    // 左右入力
                    m_TurnInputValue =
                        m_InputReader.MoveInput.x;

                    // 向き入力（右スティック）
                    m_LookInputValue =
                        m_InputReader.LookInput;

                    // ゲームパッド操作中か
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


            // 移動処理
            Move();

            // 回転処理
            Turn();

            // X/Z回転ズレ修正
            FixRotation();

            // 地面Y位置固定
            FixPosition();
        }


        // =====================================================
        // ■ エンジン音処理
        // =====================================================

        private void EngineAudio()
        {
            // 動いているか判定
            bool isMoving =
                Mathf.Abs(m_MovementInputValue) > 0.1f ||
                Mathf.Abs(m_TurnInputValue) > 0.1f;

            // =================================================
            // 停止中
            // =================================================

            if (!isMoving)
            {
                // 現在走行音なら停止音へ変更
                if (m_MovementAudio.clip ==
                    m_EngineDriving)
                {
                    // 停止音へ切り替え
                    m_MovementAudio.clip =
                        m_EngineIdling;

                    // 音程を少しランダム化
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
                // 現在停止音なら走行音へ変更
                if (m_MovementAudio.clip ==
                    m_EngineIdling)
                {
                    // 走行音へ切り替え
                    m_MovementAudio.clip =
                        m_EngineDriving;

                    // 音程を少しランダム化
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
            // 実際に進む方向
            Vector3 moveDirection;

            // =================================================
            // ゲームパッド操作
            // =================================================
            //
            // 右スティックの方向へm_LookTurnSpeedの速さで
            // 向きを合わせる（即座ではなく素早く追従、
            // という程度の速さにして不自然な瞬間反転を防ぐ）。
            // 移動は左スティック上下のみを使い、
            // 常に「今向いている方向」基準で前後する。
            // 実車の戦車のように、向きと関係ない方向へ
            // 横移動することは無い（左スティック左右は不使用）。
            //
            // =================================================

            if (m_UsingGamepadScheme &&
                Camera.main != null)
            {
                // カメラ前方向取得
                Vector3 camForward =
                    Camera.main.transform.forward;

                // 上下成分を除去
                camForward.y = 0f;

                // 正規化
                camForward.Normalize();


                // カメラ右方向取得
                Vector3 camRight =
                    Camera.main.transform.right;

                // 上下成分除去
                camRight.y = 0f;

                // 正規化
                camRight.Normalize();


                // 右スティックの向き
                Vector3 lookDirection =
                    camForward * m_LookInputValue.y +
                    camRight * m_LookInputValue.x;

                // 一定以上倒れていればその方向へ向き直す
                // （倒れていない間は今の向きを維持）
                if (lookDirection.sqrMagnitude >
                    m_LookDeadzone * m_LookDeadzone)
                {
                    // 向きたい方向
                    Quaternion targetRotation =
                        Quaternion.LookRotation(
                            lookDirection
                        );

                    // m_LookTurnSpeedの速さで向きを合わせる
                    transform.rotation =
                        Quaternion.RotateTowards(
                            transform.rotation,
                            targetRotation,
                            m_LookTurnSpeed * Time.deltaTime
                        );
                }


                // 今向いている方向へ前後移動
                moveDirection =
                    transform.forward *
                    m_MovementInputValue;
            }


            // =================================================
            // カメラ方向基準移動（DirectControl / TPS風）
            // =================================================

            else if (m_IsDirectControl &&
                     Camera.main != null)
            {
                // カメラ前方向取得
                Vector3 camForward =
                    Camera.main.transform.forward;

                // 上下成分を除去
                camForward.y = 0f;

                // 正規化
                camForward.Normalize();


                // カメラ右方向取得
                Vector3 camRight =
                    Camera.main.transform.right;

                // 上下成分除去
                camRight.y = 0f;

                // 正規化
                camRight.Normalize();


                // 入力方向作成
                moveDirection =
                    camForward * m_MovementInputValue +
                    camRight * m_TurnInputValue;


                // 斜め移動速度補正
                if (moveDirection.sqrMagnitude > 1f)
                {
                    moveDirection.Normalize();
                }


                // 移動方向を向く
                if (moveDirection != Vector3.zero)
                {
                    // 向きたい方向
                    Quaternion targetRotation =
                        Quaternion.LookRotation(
                            moveDirection
                        );

                    // 徐々に回転
                    transform.rotation =
                        Quaternion.RotateTowards(
                            transform.rotation,
                            targetRotation,
                            m_TurnSpeed * Time.deltaTime
                        );
                }
            }


            // =================================================
            // 通常タンク移動
            // =================================================

            else
            {
                // 前方向へ進む
                moveDirection =
                    transform.forward *
                    m_MovementInputValue;
            }


            // =================================================
            // 移動速度計算
            // =================================================

            Vector3 velocity =
                moveDirection * m_Speed;


            // =================================================
            // 爆風ノックバック追加
            // =================================================

            velocity += m_ExplosionVelocity;


            // =================================================
            // CharacterController移動
            // =================================================

            m_CharacterController.Move(
                velocity * Time.deltaTime
            );


            // =================================================
            // 爆風速度減衰
            // =================================================

            m_ExplosionVelocity =
                Vector3.Lerp(
                    m_ExplosionVelocity,
                    Vector3.zero,
                    Time.deltaTime * 5f
                );
        }

        private void Turn()
        {
            // =================================================
            // カメラ方向移動中は不要
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
        // ■ 回転ズレ修正
        // =====================================================

        private void FixRotation()
        {
            Vector3 rot =
                transform.eulerAngles;

            // X/Z回転を固定
            // 横転防止
            rot.x = 0f;
            rot.z = 0f;

            transform.eulerAngles = rot;
        }


        // =====================================================
        // ■ Y位置ズレ修正
        // =====================================================

        private void FixPosition()
        {
            Vector3 pos =
                transform.position;

            // 常に地面高さへ固定
            pos.y = 0f;

            transform.position = pos;
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
            // =================================================
            // 無敵中はノックバック無効
            // =================================================

            if (m_TankHealth != null &&
                m_TankHealth.IsInvincible)
            {
                return;
            }

            // =================================================
            // 爆風方向計算
            // =================================================

            Vector3 explosionDir =
                transform.position -
                explosionPosition;


            // =================================================
            // Y方向ノックバック無効
            // =================================================

            // タンクが吹っ飛んで浮かないようにする
            explosionDir.y = 0f;


            // =================================================
            // 距離取得
            // =================================================

            float explosionDistance =
                explosionDir.magnitude;


            // =================================================
            // 正規化
            // =================================================

            explosionDir.Normalize();


            // =================================================
            // 距離による減衰
            // =================================================

            float attenuation =
                1f - Mathf.Clamp01(
                    explosionDistance /
                    explosionRadius
                );


            // =================================================
            // 爆風速度計算
            // =================================================

            m_ExplosionVelocity =
                explosionDir *
                explosionForce *
                attenuation;
        }
    }
}