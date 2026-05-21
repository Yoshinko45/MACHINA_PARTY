using UnityEngine;

namespace Tanks.Complete
{
    public class CameraControl : MonoBehaviour
    {
        // =====================================================
        // ■ カメラ設定用の変数
        // =====================================================

        // カメラが目的地へ追従する時の滑らかさ
        // 値が小さい → 素早く追従
        // 値が大きい → ゆっくり追従
        public float m_DampTime = 0.2f;

        // ターゲットが画面端に近づきすぎないようにする余白
        public float m_ScreenEdgeBuffer = 4f;

        // カメラの最小ズームサイズ
        // 小さすぎると近すぎるため最低値を設定
        public float m_MinSize = 6.5f;

        // カメラが追いかける対象（プレイヤーなど）
        public Transform[] m_Targets;


        // =====================================================
        // ■ 内部で使用する変数
        // =====================================================

        // 子オブジェクトの Camera コンポーネント
        private Camera m_Camera;

        // SmoothDamp のズーム速度保存用
        private float m_ZoomSpeed;

        // SmoothDamp の移動速度保存用
        private Vector3 m_MoveVelocity;

        // カメラが最終的に移動したい座標
        private Vector3 m_DesiredPosition;

        // カメラの向き補正用オフセット
        private Vector3 m_AimToRig;


        // =====================================================
        // ■ 初期化
        // =====================================================
        private void Awake()
        {
            // 子オブジェクトから Camera を取得
            m_Camera = GetComponentInChildren<Camera>();


            // -------------------------------------------------
            // カメラが向いている位置を取得する処理
            // -------------------------------------------------

            // カメラリグ（親オブジェクト）の高さに水平な平面を作成
            Plane p = new Plane(Vector3.up, transform.position);

            // カメラ位置から前方向へレイを飛ばす
            Ray r = new Ray(m_Camera.transform.position,
                            m_Camera.transform.forward);

            // レイと平面が交差した距離を取得
            p.Raycast(r, out float d);

            // レイが平面と交わる地点
            var aimTarget = r.GetPoint(d);


            // -------------------------------------------------
            // カメラの向き補正
            // -------------------------------------------------
            // カメラが親オブジェクトの中心を
            // 見ていない場合があるため補正する
            // -------------------------------------------------

            m_AimToRig = transform.position - aimTarget;
        }


        // =====================================================
        // ■ 毎フレーム物理更新
        // =====================================================
        private void FixedUpdate()
        {
            // カメラ移動
            Move();

            // カメラズーム
            Zoom();
        }


        // =====================================================
        // ■ カメラ移動処理
        // =====================================================
        private void Move()
        {
            // ターゲットの平均位置を計算
            FindAveragePosition();

            // SmoothDampで滑らかに移動
            transform.position =
                Vector3.SmoothDamp(
                    transform.position,
                    m_DesiredPosition + m_AimToRig,
                    ref m_MoveVelocity,
                    m_DampTime
                );
        }


        // =====================================================
        // ■ ターゲットの平均位置を求める
        // =====================================================
        private void FindAveragePosition()
        {
            // 合計座標
            Vector3 averagePos = new Vector3();

            // 有効なターゲット数
            int numTargets = 0;


            // 全ターゲットを調べる
            for (int i = 0; i < m_Targets.Length; i++)
            {
                // 非表示なら無視
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                // 座標を加算
                averagePos += m_Targets[i].position;

                // ターゲット数加算
                numTargets++;
            }


            // ターゲットが存在する場合
            if (numTargets > 0)
            {
                // 平均座標を求める
                averagePos /= numTargets;
            }


            // y座標は現在の高さを維持
            // （高さは変えない）
            averagePos.y = transform.position.y;

            // 求めた座標を保存
            m_DesiredPosition = averagePos;
        }


        // =====================================================
        // ■ カメラズーム処理
        // =====================================================
        private void Zoom()
        {
            // 必要なズームサイズを計算
            float requiredSize = FindRequiredSize();

            // 滑らかにズーム変更
            m_Camera.orthographicSize =
                Mathf.SmoothDamp(
                    m_Camera.orthographicSize,
                    requiredSize,
                    ref m_ZoomSpeed,
                    m_DampTime
                );
        }


        // =====================================================
        // ■ 必要なカメラサイズを求める
        // =====================================================
        private float FindRequiredSize()
        {
            // -------------------------------------------------
            // カメラ基準のローカル座標へ変換
            // -------------------------------------------------

            Vector3 desiredLocalPos =
                m_Camera.transform.InverseTransformPoint(
                    m_DesiredPosition
                );

            // カメラサイズ初期値
            float size = 0f;


            // 全ターゲット確認
            for (int i = 0; i < m_Targets.Length; i++)
            {
                // 非表示ならスキップ
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;


                // ターゲット座標をカメラ基準へ変換
                Vector3 targetLocalPos =
                    m_Camera.transform.InverseTransformPoint(
                        m_Targets[i].position
                    );


                // カメラ中心からターゲットまでの距離
                Vector3 desiredPosToTarget =
                    targetLocalPos - desiredLocalPos;


                // ---------------------------------------------
                // 上下方向の距離を確認
                // ---------------------------------------------
                size = Mathf.Max(
                    size,
                    Mathf.Abs(desiredPosToTarget.y)
                );


                // ---------------------------------------------
                // 左右方向の距離を確認
                // aspect で画面比率補正
                // ---------------------------------------------
                size = Mathf.Max(
                    size,
                    Mathf.Abs(desiredPosToTarget.x)
                    / m_Camera.aspect
                );
            }


            // 画面端の余白追加
            size += m_ScreenEdgeBuffer;

            // 最小サイズ以下にならないよう制限
            size = Mathf.Max(size, m_MinSize);

            return size;
        }


        // =====================================================
        // ■ ゲーム開始時の位置・サイズ設定
        // =====================================================
        public void SetStartPositionAndSize()
        {
            // 平均位置計算
            FindAveragePosition();

            // 即座に移動
            transform.position = m_DesiredPosition;

            // 即座にズーム設定
            m_Camera.orthographicSize = FindRequiredSize();
        }
    }
}