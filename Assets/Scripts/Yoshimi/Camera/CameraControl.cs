using UnityEngine;

namespace Tanks.Complete
{
    public class CameraControl : MonoBehaviour
    {
        // =====================================================
        // ■ カメラ設定
        // =====================================================

        [Header("Camera Settings")]

        // カメラ追従の滑らかさ
        // 小さい → 素早く追従
        // 大きい → ゆっくり追従
        public float m_DampTime = 0.2f;

        // 画面端の余白
        public float m_ScreenEdgeBuffer = 4f;

        // カメラ最小サイズ
        public float m_MinSize = 6.5f;

        // カメラ追従対象
        public Transform[] m_Targets;


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // 子Camera
        private Camera m_Camera;

        // SmoothDamp用ズーム速度
        private float m_ZoomSpeed;

        // SmoothDamp用移動速度
        private Vector3 m_MoveVelocity;

        // カメラが移動したい位置
        private Vector3 m_DesiredPosition;

        // カメラの視線補正オフセット
        //
        // カメラRig中心と
        // 実際にカメラが見ている位置との差
        private Vector3 m_AimToRig;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // =================================================
            // Camera取得
            // =================================================

            m_Camera =
                GetComponentInChildren<Camera>();


            // Camera未設定チェック
            if (m_Camera == null)
            {
                Debug.LogError(
                    "Camera が見つかりません"
                );

                return;
            }


            // =================================================
            // Orthographic専用警告
            // =================================================

            if (!m_Camera.orthographic)
            {
                Debug.LogWarning(
                    "CameraControl は " +
                    "Orthographic Camera 前提です"
                );
            }


            // =================================================
            // カメラ視線補正計算
            // =================================================
            //
            // カメラが
            // 「Rigの中心」を
            // 見ていない場合があるため、
            // オフセットを計算する
            //
            // これを使わないと
            // ズームや移動時に
            // 中心がズレる
            // =================================================


            // Rig高さの水平面
            Plane plane =
                new Plane(
                    Vector3.up,
                    transform.position
                );


            // カメラ前方向レイ
            Ray ray =
                new Ray(
                    m_Camera.transform.position,
                    m_Camera.transform.forward
                );


            // レイと平面が交差するか
            if (plane.Raycast(ray, out float distance))
            {
                // レイ交差地点
                Vector3 aimTarget =
                    ray.GetPoint(distance);


                // Rig中心との差分
                m_AimToRig =
                    transform.position -
                    aimTarget;
            }
            else
            {
                // 念のためゼロ
                m_AimToRig =
                    Vector3.zero;
            }
        }


        // =====================================================
        // ■ LateUpdate
        // =====================================================
        //
        // カメラは
        // 「全オブジェクト移動後」
        // に動かした方が滑らか
        //
        // FixedUpdateより
        // LateUpdate推奨
        // =====================================================

        private void LateUpdate()
        {
            // ゲーム開始前
            if (m_Targets == null ||
                m_Targets.Length == 0)
            {
                return;
            }

            Move();
            Zoom();
        }

        // =====================================================
        // ■ カメラ移動
        // =====================================================

        private void Move()
        {
            if (m_Targets == null ||
                m_Targets.Length == 0)
            {
                return;
            }

            FindAveragePosition();

            transform.position =
                Vector3.SmoothDamp(
                    transform.position,
                    m_DesiredPosition + m_AimToRig,
                    ref m_MoveVelocity,
                    m_DampTime
                );
        }


        // =====================================================
        // ■ ターゲット平均位置計算
        // =====================================================

        private void FindAveragePosition()
        {
            // 平均座標計算用
            Vector3 averagePos =
                Vector3.zero;

            // 有効ターゲット数
            int numTargets = 0;


            // =================================================
            // 全ターゲット確認
            // =================================================

            for (int i = 0;
                 i < m_Targets.Length;
                 i++)
            {
                // null対策
                if (m_Targets[i] == null)
                {
                    continue;
                }


                // 非アクティブ無視
                if (!m_Targets[i]
                    .gameObject
                    .activeSelf)
                {
                    continue;
                }


                // 座標加算
                averagePos +=
                    m_Targets[i].position;

                numTargets++;
            }


            // =================================================
            // ターゲット0人
            // =================================================

            if (numTargets == 0)
            {
                // 現在位置維持
                m_DesiredPosition =
                    transform.position;

                return;
            }


            // =================================================
            // 平均座標
            // =================================================

            averagePos /=
                numTargets;


            // =================================================
            // 高さ固定
            // =================================================
            //
            // カメラRigの高さは
            // 変えない
            // =================================================

            averagePos.y =
                transform.position.y;


            // 保存
            m_DesiredPosition =
                averagePos;
        }


        // =====================================================
        // ■ カメラズーム
        // =====================================================

        private void Zoom()
        {
            if (m_Targets == null ||
                m_Targets.Length == 0)
            {
                return;
            }

            float requiredSize =
                FindRequiredSize();

            m_Camera.orthographicSize =
                Mathf.SmoothDamp(
                    m_Camera.orthographicSize,
                    requiredSize,
                    ref m_ZoomSpeed,
                    m_DampTime
                );
        }

        // =====================================================
        // ■ 必要カメラサイズ計算
        // =====================================================

        private float FindRequiredSize()
        {
            // =================================================
            // カメラ基準へ変換
            // =================================================

            Vector3 desiredLocalPos =
                m_Camera.transform
                .InverseTransformPoint(
                    m_DesiredPosition +
                    m_AimToRig
                );


            // 必要サイズ
            float size = 0f;


            // =================================================
            // 全ターゲット確認
            // =================================================

            for (int i = 0;
                 i < m_Targets.Length;
                 i++)
            {
                // null対策
                if (m_Targets[i] == null)
                {
                    continue;
                }


                // 非表示無視
                if (!m_Targets[i]
                    .gameObject
                    .activeSelf)
                {
                    continue;
                }


                // ターゲット座標を
                // カメラ基準へ変換
                Vector3 targetLocalPos =
                    m_Camera.transform
                    .InverseTransformPoint(
                        m_Targets[i].position
                    );


                // 中心との差
                Vector3 desiredPosToTarget =
                    targetLocalPos -
                    desiredLocalPos;


                // =================================================
                // 上下方向
                // =================================================

                size =
                    Mathf.Max(
                        size,
                        Mathf.Abs(
                            desiredPosToTarget.y
                        )
                    );


                // =================================================
                // 左右方向
                // =================================================
                //
                // aspect補正必須
                // =================================================

                size =
                    Mathf.Max(
                        size,

                        Mathf.Abs(
                            desiredPosToTarget.x
                        )
                        / m_Camera.aspect
                    );
            }


            // =================================================
            // 画面余白追加
            // =================================================

            size +=
                m_ScreenEdgeBuffer;


            // =================================================
            // 最小サイズ制限
            // =================================================

            size =
                Mathf.Max(
                    size,
                    m_MinSize
                );


            return size;
        }


        // =====================================================
        // ■ ラウンド開始時設定
        // =====================================================

        public void SetStartPositionAndSize()
        {
            // 平均位置更新
            FindAveragePosition();


            // =================================================
            // 即座に移動
            // =================================================
            //
            // 視線補正込み
            // =================================================

            transform.position =
                m_DesiredPosition +
                m_AimToRig;


            // =================================================
            // 即座にズーム
            // =================================================

            m_Camera.orthographicSize =
                FindRequiredSize();
        }
    }
}