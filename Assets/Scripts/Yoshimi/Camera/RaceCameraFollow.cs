using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ レースモード用追従カメラ
    // =====================================================
    //
    // ・指定したタンクを追いかける
    // ・タンク後方から見る三人称視点
    // ・滑らかに移動、回転する
    // ・分割画面対応
    // ・キー入力／ゲームパッド（X or Yボタン）でバックビュー（反転視点）に切り替え可能
    //
    [RequireComponent(typeof(Camera))]
    public class RaceCameraFollow : MonoBehaviour
    {
        // =====================================================
        // ■ 追従対象
        // =====================================================

        [Header("Target")]

        // 追いかけるタンク
        public Transform m_Target;


        // =====================================================
        // ■ カメラ追従設定
        // =====================================================

        [Header("Follow")]

        // タンクからの相対位置
        //
        // X = 左右
        // Y = 高さ
        // Z = 前後
        //
        // (0,5,-8)
        // → 高さ5m、後方8m
        public Vector3 m_Offset =
            new Vector3(0f, 5f, -8f);

        // タンクのどの高さを見るか
        public float m_LookAtHeight = 1.2f;

        // 位置追従の滑らかさ
        public float m_PositionDamping = 10f;

        // 回転追従の滑らかさ
        public float m_RotationDamping = 12f;

        // タンクの向きに合わせて
        // カメラも後方へ回り込むか
        public bool m_FollowTargetYaw = true;

        // =====================================================
        //  回転追従
        // =====================================================

        // 現在のカメラ用Y角度
        private float m_CurrentYaw;

        // ターゲット角度への追従速度
        public float m_YawFollowSpeed = 3f;


        // =====================================================
        // ■ 視点反転（バックビュー）設定
        // =====================================================
        //
        // 指定したキー（またはゲームパッドのX/Yボタン）で、
        // 車の前方（m_ReverseOffset基準）へカメラを切り替え、
        // 自分の車と後方から追ってくる相手の両方を見られるようにする。
        // 向きはLookAtを使わず、車の向き＋m_ReverseTiltAngleで直接指定。
        // 反転する瞬間・解除する瞬間のどちらも、追従の遅延（後追い）を
        // 無くし位置・向きとも即座に切り替える。
        //
        // =====================================================

        [Header("視点反転（バックビュー）")]

        [Tooltip("このキーでカメラを反転（後方確認）できます（ゲームパッドはX/Yボタンで固定対応）")]
        public KeyCode m_ReverseKey = KeyCode.C;

        [Tooltip("ON：キーを押している間だけ反転／OFF：押すたびに反転⇔通常を切り替え（トグル式）")]
        public bool m_HoldToReverse = false;

        [Tooltip("反転時のカメラ位置（車からの相対オフセット）\n" +
            "X＝左右、Y＝高さ、Z＝前後（プラスで車の前方）\n" +
            "自分の車と、後方から追ってくる相手の両方が画面に収まるように調整してください")]
        public Vector3 m_ReverseOffset = new Vector3(0f, 6f, 9f);

        [Tooltip("反転時のカメラの見下ろし角度（度）\n0＝水平、大きいほど下向きに見下ろす")]
        public float m_ReverseTiltAngle = 20f;


        // =====================================================
        // ■ ドリフト充填シェイク設定
        // =====================================================
        //
        // ミニターボのゲージが1段階上がる瞬間ごとに、
        // カメラを一瞬だけ揺らして「溜まった！」感を出す演出。
        //
        // =====================================================

        [Header("Drift Charge Shake")]

        [Tooltip("ミニターボの段階が上がった瞬間のカメラ揺れの強さ。3要素＝段階1,2,3")]
        public float[] m_DriftStageShakeMagnitudes = new float[] { 0.08f, 0.15f, 0.3f };

        [Tooltip("カメラ揺れが収まるまでの時間（秒）")]
        public float m_DriftStageShakeDuration = 0.15f;


        // =====================================================
        // ■ 内部参照
        // =====================================================

        private Camera m_Camera;

        // 追跡対象のCarMovement（ドリフト段階の監視用。取得できなければ演出は発生しない）
        private CarMovement m_TargetCarMovement;

        // 追跡対象のInputReader（後方確認のゲームパッド入力を、その対象自身のデバイスから読むために使用）
        private InputReader m_TargetInputReader;

        // m_TargetCarMovementを解決した時点のm_Target（変化検知用。
        // SetTarget()を経由せずInspectorで直接Targetが差し替えられても
        // 追従できるよう、毎フレームm_Targetそのものと比較する）
        private Transform m_ResolvedTargetFor;

        // ターゲットが（初回設定時、または途中で差し替えられて）変わった直後はtrue。
        // trueの間はLateUpdateで位置・向きの補間を行わず、目標地点へ即座にスナップさせる。
        // （これが無いと、m_FollowPosition/m_CurrentYawが初期値[0など]のまま
        // 　補間の開始点として使われてしまい、再生開始直後にカメラが原点付近から
        // 　車の背後へ横移動しながら滑ってくるように見えてしまう＝今回の不具合の原因）
        private bool m_NeedsSnapToTarget;

        // シェイクの影響を受けない「本来の追従位置」（シェイクをここに混ぜ込まないための専用変数）
        private Vector3 m_FollowPosition;

        // 現在のシェイクによる位置オフセット（毎フレーム再計算、蓄積はしない）
        private Vector3 m_ShakeOffset;

        // 現在進行中のシェイクの基準の強さ（段階アップの瞬間に決定される）
        private float m_ShakeMagnitude;

        // シェイクの残り時間
        private float m_ShakeTimer;

        // 前フレームで観測したドリフト段階（段階が上がった瞬間の検知に使う）
        private int m_LastObservedDriftStage;

        // 現在バックビュー（反転）中かどうか
        private bool m_IsReversed;

        // 前フレームで反転していたか（反転⇔通常の切り替わった瞬間の検知用）
        private bool m_WasReversed;

        // 外部取得用
        public Camera Camera => m_Camera;

        // 現在反転中かどうか（UI表示などから参照したい場合に公開）
        public bool IsReversed => m_IsReversed;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // Camera取得
            m_Camera =
                GetComponent<Camera>();


            // 念のため透視投影へ変更
            if (m_Camera != null)
            {
                m_Camera.orthographic = false;
            }
        }


        // =====================================================
        // ■ LateUpdate
        // =====================================================
        //
        // タンク移動後にカメラを更新
        //
        // UpdateではなくLateUpdateにすることで
        // カメラのガタつきを防ぐ
        //
        // =====================================================

        private void LateUpdate()
        {
            // =================================================
            // 反転（バックビュー）入力判定
            // =================================================
            // ゲームパッド側（対象自身のInputReaderのX/Yボタン）の状態
            bool gamepadReverseHeld    = m_TargetInputReader != null && m_TargetInputReader.LookBackHeld;
            bool gamepadReversePressed = m_TargetInputReader != null && m_TargetInputReader.LookBackPressed;

            if (m_HoldToReverse)
            {
                // 押している間だけ反転（キーボード／ゲームパッドどちらでも反応）
                m_IsReversed = Input.GetKey(m_ReverseKey) || gamepadReverseHeld;
            }
            else if (Input.GetKeyDown(m_ReverseKey) || gamepadReversePressed)
            {
                // 押すたびに反転⇔通常を切り替え（キーボード／ゲームパッドどちらでも反応）
                m_IsReversed = !m_IsReversed;
            }

            // 反転⇔通常が切り替わった瞬間か（この1フレームだけ位置・向きを即座に反映する）
            bool justToggled = m_IsReversed != m_WasReversed;
            m_WasReversed = m_IsReversed;

            // ドリフト段階の監視とシェイク量の更新（対象が無くてもここで安全に減衰させる）
            UpdateDriftStageShake();

            // ターゲット未設定
            if (m_Target == null)
            {
                return;
            }

            // =================================================
            // 角度追従
            // =================================================
            if (m_FollowTargetYaw)
            {
                m_CurrentYaw =
                    Mathf.LerpAngle(
                        m_CurrentYaw,
                        m_Target.eulerAngles.y,
                        m_YawFollowSpeed *
                        Time.deltaTime
                    );
            }

            // =================================================
            // オフセット・角度の切り替え（通常 / 反転）
            // =================================================
            //
            // 反転中は「今の車の向き」をそのまま使い、
            // m_CurrentYawの遅延を挟まないことで後追い感を無くす
            //
            // =================================================

            Vector3 offsetToUse =
                m_IsReversed
                    ? m_ReverseOffset
                    : m_Offset;

            float yawToUse =
                m_IsReversed
                    ? m_Target.eulerAngles.y
                    : m_CurrentYaw;

            Quaternion yawRotation =
                Quaternion.Euler(
                    0f,
                    yawToUse,
                    0f
                );

            // =================================================
            // 目標位置計算
            // =================================================
            //
            // タンク位置 +
            // タンク向きに合わせたオフセット
            //
            // =================================================

            Vector3 desiredPosition =
                m_Target.position +
                yawRotation * offsetToUse;


            // =================================================
            // 移動反映
            // =================================================
            //
            // シェイクの影響を受けない「本来の追従位置」はm_FollowPositionで管理し、
            // 実際に画面へ反映するtransform.positionにだけシェイクを足す。
            // （transform.positionを直接Lerpの入力にすると、シェイクの揺れが
            // 　毎フレーム追従位置に少しずつ混ざって蓄積してしまうため）
            //
            // 反転中、および反転⇔通常が切り替わった瞬間は後追いさせたくないため、
            // Lerpを使わず即座に反映する
            //
            // =================================================

            if (m_IsReversed || justToggled || m_NeedsSnapToTarget)
            {
                m_FollowPosition = desiredPosition;
            }
            else
            {
                float positionBlend =
                    1f -
                    Mathf.Exp(
                        -m_PositionDamping *
                        Time.deltaTime
                    );

                m_FollowPosition =
                    Vector3.Lerp(
                        m_FollowPosition,
                        desiredPosition,
                        positionBlend
                    );
            }

            transform.position =
                m_FollowPosition +
                m_ShakeOffset;


            // =================================================
            // 向き反映
            // =================================================
            //
            // 反転中：LookAtは使わず、車の向き＋m_ReverseTiltAngleで直接指定
            // 　　　　（毎フレーム即座に反映＝後追いなし）
            // 通常時：これまで通りLookAtベースで滑らかに追従
            // 　　　　（反転から戻った瞬間だけは即座に切り替える）
            //
            // =================================================

            if (m_IsReversed)
            {
                transform.rotation =
                    Quaternion.Euler(
                        m_ReverseTiltAngle,
                        m_Target.eulerAngles.y + 180f,
                        0f
                    );
            }
            else
            {
                Vector3 lookTarget =
                    m_Target.position +
                    Vector3.up * m_LookAtHeight;

                Vector3 toTarget =
                    lookTarget -
                    transform.position;

                // ターゲットとの距離がほぼ0のときは向きを更新しない
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRotation =
                        Quaternion.LookRotation(
                            toTarget.normalized,
                            Vector3.up
                        );

                    if (justToggled || m_NeedsSnapToTarget)
                    {
                        transform.rotation = desiredRotation;
                    }
                    else
                    {
                        float rotationBlend =
                            1f -
                            Mathf.Exp(
                                -m_RotationDamping *
                                Time.deltaTime
                            );

                        transform.rotation =
                            Quaternion.Slerp(
                                transform.rotation,
                                desiredRotation,
                                rotationBlend
                            );
                    }
                }
            }

            // このフレームで使い終わったのでリセット
            // （次のフレームからはいつも通り滑らかに補間する）
            m_NeedsSnapToTarget = false;
        }


        // =====================================================
        // ■ ターゲット設定
        // =====================================================
        //
        // レース開始時に
        // 「このタンクを追跡してください」
        // と指定する
        //
        // =====================================================

        public void SetTarget(Transform target)
        {
            m_Target = target;

            // CarMovementの取得やシェイク状態のリセットはUpdateDriftStageShake側で
            // 「m_Targetが前回と変わったか」を毎フレーム見て自動的に行う
            // （SetTarget()を介さずInspectorで直接Targetが設定されるケースでも
            // 　同じ経路で正しく解決されるようにするため、ここでは二重管理しない）

            if (target == null)
            {
                return;
            }

            // カメラ用角度初期化
            m_CurrentYaw =
                target.eulerAngles.y;

            Vector3 offsetToUse =
                m_IsReversed
                    ? m_ReverseOffset
                    : m_Offset;

            Quaternion yawRotation =
                m_FollowTargetYaw
                    ? Quaternion.Euler(
                        0f,
                        m_CurrentYaw,
                        0f
                    )
                    : Quaternion.identity;

            m_FollowPosition =
                target.position +
                yawRotation * offsetToUse;

            transform.position = m_FollowPosition;

            if (m_IsReversed)
            {
                transform.rotation =
                    Quaternion.Euler(
                        m_ReverseTiltAngle,
                        target.eulerAngles.y + 180f,
                        0f
                    );
            }
            else
            {
                transform.LookAt(
                    target.position +
                    Vector3.up * m_LookAtHeight
                );
            }
        }

        // =====================================================
        // ■ ビューポート設定
        // =====================================================
        //
        // 分割画面用
        //
        // 例
        //
        // 左半分
        // Rect(0,0,0.5,1)
        //
        // 右半分
        // Rect(0.5,0,0.5,1)
        //
        // =====================================================

        public void SetViewport(Rect viewport)
        {
            // Camera未取得なら再取得
            if (m_Camera == null)
            {
                m_Camera =
                    GetComponent<Camera>();
            }


            // =================================================
            // 表示範囲設定
            // =================================================

            if (m_Camera != null)
            {
                // 分割画面領域
                m_Camera.rect = viewport;

                // 透視投影
                m_Camera.orthographic = false;
            }
        }


        // =====================================================
        // ■ ドリフト充填シェイク
        // =====================================================
        //
        // 追跡対象のミニターボ段階（CarMovement.DriftStage）を毎フレーム監視し、
        // 前フレームより上がった瞬間だけ新しいシェイクを開始する。
        // シェイクは時間経過で自然に減衰する一時的な位置オフセットとして扱い、
        // LateUpdate側の「本来の追従位置」(m_FollowPosition)には混ぜ込まない。
        //
        // =====================================================

        private void UpdateDriftStageShake()
        {
            // ── 対象(m_Target)が前回と変わっていたらCarMovementを取得し直す ──
            // SetTarget()経由の変更だけでなく、Inspectorで直接Targetが
            // 差し替えられているだけのケース（現状このプロジェクトはこちら）でも
            // 正しく追従できるよう、m_Targetの参照そのものを毎フレーム比較する
            if (m_Target != m_ResolvedTargetFor)
            {
                m_ResolvedTargetFor = m_Target;

                m_TargetCarMovement =
                    m_Target != null
                        ? m_Target.GetComponentInParent<CarMovement>()
                        : null;

                // 後方確認のゲームパッド入力は、対象自身のInputReaderから読む
                // （分割画面で複数の車を追う場合でも、各カメラが自分の対象の
                // 　デバイスだけを見るようにするため）
                m_TargetInputReader =
                    m_Target != null
                        ? m_Target.GetComponentInParent<InputReader>()
                        : null;

                // 対象切り替え時に、前の対象のシェイク・段階を引き継がないようリセット
                m_LastObservedDriftStage =
                    m_TargetCarMovement != null
                        ? m_TargetCarMovement.DriftStage
                        : 0;

                m_ShakeTimer = 0f;
                m_ShakeOffset = Vector3.zero;

                // ── ターゲットが変わった最初のフレーム用の初期化 ──────────
                // ここでm_CurrentYawをターゲットの現在の向きに合わせておくことで、
                // このあとLateUpdate内で行われるYawの補間（LerpAngle）が
                // 0度などの初期値からではなく正しい角度から始まるようにする。
                // 位置・向きの補間そのものをスキップして即座にスナップさせる処理は
                // LateUpdate側でこのフラグを見て行う。
                if (m_Target != null)
                {
                    m_CurrentYaw = m_Target.eulerAngles.y;
                }

                m_NeedsSnapToTarget = true;
            }

            // ── 対象の現在のドリフト段階を取得 ──────────────────
            int currentStage =
                m_TargetCarMovement != null
                    ? m_TargetCarMovement.DriftStage
                    : 0;

            // ── 段階が上がった瞬間だけ新しいシェイクを開始 ─────────
            if (currentStage > m_LastObservedDriftStage)
            {
                int arrayLength =
                    m_DriftStageShakeMagnitudes != null
                        ? m_DriftStageShakeMagnitudes.Length
                        : 0;

                int idx =
                    Mathf.Clamp(
                        currentStage - 1,
                        0,
                        Mathf.Max(0, arrayLength - 1)
                    );

                m_ShakeMagnitude =
                    (arrayLength > idx && idx >= 0)
                        ? m_DriftStageShakeMagnitudes[idx]
                        : 0.1f;

                m_ShakeTimer = m_DriftStageShakeDuration;
            }

            m_LastObservedDriftStage = currentStage;

            // ── シェイクの減衰・オフセット計算 ───────────────────
            if (m_ShakeTimer > 0f)
            {
                m_ShakeTimer =
                    Mathf.Max(
                        0f,
                        m_ShakeTimer - Time.deltaTime
                    );

                float t =
                    m_DriftStageShakeDuration > 0f
                        ? m_ShakeTimer / m_DriftStageShakeDuration
                        : 0f;

                // 残り時間の割合(t)だけ毎フレーム新しいランダム方向へ揺らし、
                // 収まる直前ほど揺れ幅が小さくなるようにする
                m_ShakeOffset =
                    Random.insideUnitSphere *
                    m_ShakeMagnitude *
                    t;
            }
            else
            {
                m_ShakeOffset = Vector3.zero;
            }
        }
    }
}