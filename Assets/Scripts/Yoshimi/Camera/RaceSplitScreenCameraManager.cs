using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ レース用分割画面カメラ管理
    // =====================================================
    //
    // ・プレイヤー人数に応じてカメラ生成
    // ・画面分割レイアウト設定
    // ・各カメラへ追跡対象を設定
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

        // オフライン最大人数
        public int m_MaxOfflinePlayers = 4;


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
                viewports[0] =
                    new Rect(0f, 0.5f, 1f, 0.5f);

                viewports[1] =
                    new Rect(0f, 0f, 0.5f, 0.5f);

                viewports[2] =
                    new Rect(0.5f, 0f, 0.5f, 0.5f);
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