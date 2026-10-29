using UnityEngine;
 
namespace Tanks.Complete
{
    // =====================================================
    // ■ レース用分割画面カメラ管理
    // =====================================================
    //
    // ・プレイヤー人数に応じてカメラ生成
    // ・画面分割レイアウト設定
    //     1人＝全画面／2人＝左右（または上下）／3人＝2×2で右下を空ける（または上1つ＋下2つ）／4人＝2×2
    // ・各カメラへ追跡対象を設定
    // ・どのカメラにも映らない空き枠（3人の時の右下など）の範囲と色を、RaceGameManager に教える
    //     （塗りつぶすのは、表示側の RaceHud。スクリプトでカメラを作ると、Unity 6.2 の
    //       URP の不具合で UI の上に重なってしまうため、カメラでは塗らない）
    // ・2人以上の時は、Cキーで全員のカメラが同時に反転しないようにする
    // ・オンライン時は1画面表示
    //
    public class RaceSplitScreenCameraManager : MonoBehaviour
    {
        // =====================================================
        // ■ 2人プレイ時の画面分割方式
        // =====================================================
 
        public enum TwoPlayerLayout
        {
            Vertical,   // 左右分割
            Horizontal  // 上下分割
        }


        // =====================================================
        // ■ 3人プレイ時の画面分割方式
        // =====================================================

        public enum ThreePlayerLayout
        {
            QuadWithEmptySlot,  // 2×2に分けて、右下を空ける（全員同じ大きさ。空き枠はミニマップなどに使える）
            TopWide             // 上に大きく1つ、下に小さく2つ（1人目だけ画面が大きい）
        }
 
 
        // =====================================================
        // ■ カメラ設定
        // =====================================================
 
        [Header("Cameras")]
 
        // カメラプレハブ
        public RaceCameraFollow m_CameraPrefab;
 
        // 元になるカメラ
        public Camera m_SourceCamera;
 
        // 最大4台のレースカメラ
        public RaceCameraFollow[] m_Cameras =
            new RaceCameraFollow[4];
 
 
        // =====================================================
        // ■ レイアウト設定
        // =====================================================
 
        [Header("Layout")]
 
        // 2人プレイ時の分割方式
        public TwoPlayerLayout m_TwoPlayerLayout =
            TwoPlayerLayout.Vertical;

        // 3人プレイ時の分割方式
        public ThreePlayerLayout m_ThreePlayerLayout =
            ThreePlayerLayout.QuadWithEmptySlot;

        // オフライン最大人数
        public int m_MaxOfflinePlayers = 4;


        // =====================================================
        // ■ 空き枠の塗りつぶし
        // =====================================================

        [Header("Empty Area")]

        [Tooltip("どのカメラにも映らない部分（3人の時の右下など）を、下の色で塗りつぶす" +
            "（塗るのは表示側の RaceHud。ミニマップは、この上に置かれる）。" +
            "OFFだと、その部分に、シーンに残っているカメラの絵（車など）が映ることがある")]
        public bool m_FillEmptyArea = true;

        // 空き枠の色
        public Color m_EmptyAreaColor =
            new Color(0.04f, 0.05f, 0.09f, 1f);


        // =====================================================
        // ■ 外から見える状態
        // =====================================================

        // Setup で決まった、使っているカメラの数
        private int m_ActiveCameraCount;

        // どのカメラにも映らない空き枠（無ければ幅0）
        private Rect m_EmptyArea;

        /// <summary>Setup で使うことにした、カメラ（プレイヤー）の数</summary>
        public int ActiveCameraCount
        {
            get { return m_ActiveCameraCount; }
        }

        /// <summary>
        /// どのカメラにも映らない空き枠があるか
        /// （3人でQuadWithEmptySlotの時の右下など）。
        /// </summary>
        public bool HasEmptyArea
        {
            get { return m_EmptyArea.width > 0f && m_EmptyArea.height > 0f; }
        }

        /// <summary>
        /// 空き枠の範囲（画面全体を 0〜1 とした割合）。空き枠が無ければ幅・高さが0。
        /// ミニマップなどを、この中に置くのに使う。
        /// </summary>
        public Rect EmptyArea
        {
            get { return m_EmptyArea; }
        }

        /// <summary>
        /// 空き枠を塗りつぶす範囲と色を求める。
        /// 空き枠が無い時、または「Fill Empty Area」がOFFの時は false（塗らない）。
        /// </summary>
        /// <param name="area">塗る範囲（画面全体を 0〜1 とした割合）</param>
        /// <param name="color">塗る色</param>
        public bool TryGetEmptyAreaFill(out Rect area, out Color color)
        {
            area = m_EmptyArea;
            color = m_EmptyAreaColor;

            return m_FillEmptyArea && HasEmptyArea;
        }
 
 
        // =====================================================
        // ■ カメラ初期化
        // =====================================================
        //
        // targets
        //   各プレイヤーのTransform
        //
        // isOnlineMode
        //   オンラインかどうか
        //
        // =====================================================
 
        public void Setup(
            Transform[] targets,
            bool isOnlineMode)
        {
            // プレイヤー人数取得
            int targetCount =
                targets == null
                    ? 0
                    : targets.Length;
 
 
            // =================================================
            // 使用カメラ数決定
            // =================================================
 
            int cameraCount =
                isOnlineMode
                    ? Mathf.Min(1, targetCount)
                    : Mathf.Clamp(
                        targetCount,
                        0,
                        Mathf.Clamp(
                            m_MaxOfflinePlayers,
                            1,
                            4
                        )
                    );
 
 
            // カメラ配列保証
            EnsureCameraArray();
 
            // 必要数生成
            EnsureCameras(cameraCount);
 
 
            // =================================================
            // ビューポート取得
            // =================================================
 
            Rect[] viewports =
                GetViewports(cameraCount);

            m_ActiveCameraCount = cameraCount;
            m_EmptyArea = GetEmptyArea(cameraCount);


            // =================================================
            // カメラ設定
            // =================================================
 
            for (int i = 0;
                 i < m_Cameras.Length;
                 i++)
            {
                RaceCameraFollow cameraFollow =
                    m_Cameras[i];
 
                if (cameraFollow == null)
                {
                    continue;
                }
 
 
                // 有効判定
                bool active =
                    i < cameraCount &&
                    targets[i] != null;
 
 
                cameraFollow.gameObject.SetActive(
                    active
                );
 
 
                // 無効なら追跡解除
                if (!active)
                {
                    cameraFollow.SetTarget(null);
                    continue;
                }
 
 
                // 画面領域設定
                cameraFollow.SetViewport(
                    viewports[i]
                );

                // Cキーでの反転は、1人プレイの時だけ有効にする。
                // 2人以上だと、誰かがCキーを押すと全員のカメラが反転してしまうため。
                // （各プレイヤーの後方確認ボタンは、その人のカメラだけを反転する）
                cameraFollow.m_UseReverseKey =
                    cameraCount <= 1;

                // 追跡対象設定
                cameraFollow.SetTarget(
                    targets[i]
                );
            }
        }
 
 
        // =====================================================
        // ■ カメラ配列保証
        // =====================================================
        //
        // 必ず4要素の配列にする
        //
        // =====================================================
 
        private void EnsureCameraArray()
        {
            if (m_Cameras == null ||
                m_Cameras.Length != 4)
            {
                RaceCameraFollow[] cameras =
                    new RaceCameraFollow[4];
 
                if (m_Cameras != null)
                {
                    for (int i = 0;
                         i < Mathf.Min(
                             m_Cameras.Length,
                             cameras.Length);
                         i++)
                    {
                        cameras[i] =
                            m_Cameras[i];
                    }
                }
 
                m_Cameras = cameras;
            }
        }
 
 
        // =====================================================
        // ■ 必要数のカメラ生成
        // =====================================================
 
        private void EnsureCameras(int cameraCount)
        {
            for (int i = 0;
                 i < cameraCount;
                 i++)
            {
                if (m_Cameras[i] != null)
                {
                    continue;
                }
 
                m_Cameras[i] =
                    CreateCamera(i);
            }
        }
 
 
        // =====================================================
        // ■ カメラ生成
        // =====================================================
        //
        // index
        //   カメラ番号
        //
        // =====================================================
 
        private RaceCameraFollow CreateCamera(int index)
        {
            // =================================================
            // プレハブ生成
            // =================================================
 
            if (m_CameraPrefab != null)
            {
                RaceCameraFollow instance =
                    Instantiate(
                        m_CameraPrefab,
                        transform
                    );
 
                instance.name =
                    $"Race Camera {index + 1}";
 
                return instance;
            }
 
 
            // =================================================
            // 元カメラ取得
            // =================================================
 
            Camera source =
                m_SourceCamera != null
                    ? m_SourceCamera
                    : Camera.main;
 
 
            // =================================================
            // カメラが存在しない場合
            // =================================================
 
            if (source == null)
            {
                GameObject cameraObject =
                    new GameObject(
                        $"Race Camera {index + 1}"
                    );
 
                cameraObject.transform.SetParent(
                    transform
                );
 
                cameraObject.AddComponent<Camera>();
 
                return cameraObject
                    .AddComponent<RaceCameraFollow>();
            }
 
 
            // =================================================
            // カメラ複製
            // =================================================
 
            GameObject newCameraObject =
                index == 0
                    ? source.gameObject
                    : Instantiate(
                        source.gameObject,
                        transform
                    );
 
            newCameraObject.name =
                $"Race Camera {index + 1}";
 
 
            // =================================================
            // AudioListener重複防止
            // =================================================
 
            if (index > 0)
            {
                AudioListener audioListener =
                    newCameraObject
                        .GetComponent<AudioListener>();
 
                if (audioListener != null)
                {
                    Destroy(audioListener);
                }
            }
 
 
            // =================================================
            // RaceCameraFollow取得
            // =================================================
 
            RaceCameraFollow follow =
                newCameraObject
                    .GetComponent<RaceCameraFollow>();
 
            if (follow == null)
            {
                follow =
                    newCameraObject
                        .AddComponent<RaceCameraFollow>();
            }
 
            return follow;
        }
 
 
        // =====================================================
        // ■ 空き枠の範囲
        // =====================================================
        //
        // どのカメラにも映らない部分。
        // 今は「3人で、2×2に分けて右下を空ける」時だけ。
        //
        // =====================================================

        private Rect GetEmptyArea(int cameraCount)
        {
            if (cameraCount == 3 &&
                m_ThreePlayerLayout ==
                ThreePlayerLayout.QuadWithEmptySlot)
            {
                return new Rect(0.5f, 0f, 0.5f, 0.5f);
            }

            // 空き枠なし
            return new Rect(0f, 0f, 0f, 0f);
        }


        // =====================================================
        // ■ ビューポート取得
        // =====================================================
        //
        // プレイヤー人数ごとの
        // 画面分割設定を返す
        //
        // =====================================================
 
        private Rect[] GetViewports(int cameraCount)
        {
            Rect[] viewports =
                new Rect[4];
 
 
            // =================================================
            // 1人
            // =================================================
 
            if (cameraCount <= 1)
            {
                viewports[0] =
                    new Rect(0f, 0f, 1f, 1f);
            }
 
 
            // =================================================
            // 2人
            // =================================================
 
            else if (cameraCount == 2)
            {
                // 上下分割
                if (m_TwoPlayerLayout ==
                    TwoPlayerLayout.Horizontal)
                {
                    viewports[0] =
                        new Rect(0f, 0.5f, 1f, 0.5f);
 
                    viewports[1] =
                        new Rect(0f, 0f, 1f, 0.5f);
                }
 
                // 左右分割
                else
                {
                    viewports[0] =
                        new Rect(0f, 0f, 0.5f, 1f);
 
                    viewports[1] =
                        new Rect(0.5f, 0f, 0.5f, 1f);
                }
            }
 
 
            // =================================================
            // 3人
            // =================================================
 
            else if (cameraCount == 3)
            {
                // 上に大きく1つ、下に小さく2つ
                if (m_ThreePlayerLayout ==
                    ThreePlayerLayout.TopWide)
                {
                    viewports[0] =
                        new Rect(0f, 0.5f, 1f, 0.5f);

                    viewports[1] =
                        new Rect(0f, 0f, 0.5f, 0.5f);

                    viewports[2] =
                        new Rect(0.5f, 0f, 0.5f, 0.5f);
                }

                // 2×2に分けて、右下を空ける
                else
                {
                    viewports[0] =
                        new Rect(0f, 0.5f, 0.5f, 0.5f);

                    viewports[1] =
                        new Rect(0.5f, 0.5f, 0.5f, 0.5f);

                    viewports[2] =
                        new Rect(0f, 0f, 0.5f, 0.5f);
                }
            }
 
 
            // =================================================
            // 4人
            // =================================================
 
            else
            {
                viewports[0] =
                    new Rect(0f, 0.5f, 0.5f, 0.5f);
 
                viewports[1] =
                    new Rect(0.5f, 0.5f, 0.5f, 0.5f);
 
                viewports[2] =
                    new Rect(0f, 0f, 0.5f, 0.5f);
 
                viewports[3] =
                    new Rect(0.5f, 0f, 0.5f, 0.5f);
            }
 
            return viewports;
        }
    }
}