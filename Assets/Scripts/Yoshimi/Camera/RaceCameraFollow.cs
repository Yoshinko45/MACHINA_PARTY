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
        // ■ 内部参照
        // =====================================================

        private Camera m_Camera;

        // 外部取得用
        public Camera Camera => m_Camera;


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

            Quaternion yawRotation =
                Quaternion.Euler(
                    0f,
                    m_CurrentYaw,
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
                yawRotation * m_Offset;


            // =================================================
            // 滑らか移動
            // =================================================

            float positionBlend =
                1f -
                Mathf.Exp(
                    -m_PositionDamping *
                    Time.deltaTime
                );

            transform.position =
                Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    positionBlend
                );


            // =================================================
            // 注視点計算
            // =================================================

            Vector3 lookTarget =
                m_Target.position +
                Vector3.up * m_LookAtHeight;

            Vector3 toTarget =
                lookTarget -
                transform.position;


            // ターゲットとの距離がほぼ0
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }


            // =================================================
            // 向き計算
            // =================================================

            Quaternion desiredRotation =
                Quaternion.LookRotation(
                    toTarget.normalized,
                    Vector3.up
                );


            // =================================================
            // 滑らか回転
            // =================================================

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

            if (target == null)
            {
                return;
            }

            // カメラ用角度初期化
            m_CurrentYaw =
                target.eulerAngles.y;

            Quaternion yawRotation =
                m_FollowTargetYaw
                    ? Quaternion.Euler(
                        0f,
                        m_CurrentYaw,
                        0f
                    )
                    : Quaternion.identity;

            transform.position =
                target.position +
                yawRotation * m_Offset;

            transform.LookAt(
                target.position +
                Vector3.up * m_LookAtHeight
            );
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
    }
}