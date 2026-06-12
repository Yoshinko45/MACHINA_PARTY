using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tanks.Complete
{
    namespace Tanks.Complete
    {
        //=========================================================
        // ■ GameUIHandler
        //=========================================================
        //
        // タイトル画面（スタートメニュー）と
        // ポーズメニューの管理を行うクラス。
        //
        // 主な役割
        // ・参加プレイヤー数の管理
        // ・プレイヤー/CPUの切り替え
        // ・ゲーム開始処理
        // ・ポーズメニューの表示
        // ・モバイルUIの表示切替
        //
        //=========================================================

        public class GameUIHandler : MonoBehaviour
        {
            //=====================================================
            // ■ GameManager参照
            //=====================================================

            // 実際にゲームを開始する管理クラス
            public GameManager m_GameManager;


            //=====================================================
            // ■ スタートメニュー関連
            //=====================================================

            [Header("Start Menu")]

            // スタート画面全体
            public RectTransform m_StartMenuRoot;

            // スタートボタン
            public Button m_StartButton;

            // プレイヤースロット
            // （最大4人分）
            [Tooltip("The slot in the UI that can be taken by a player or computer tank")]
            public StartMenuSlot[] m_PlayerSlots;


            //=====================================================
            // ■ モバイルUI
            //=====================================================

            // モバイル用ポーズボタン
            public OnScreenButton m_PauseMenuButton;


            //=====================================================
            // ■ 内部変数
            //=====================================================

            // Startボタンの文字
            private TextMeshProUGUI m_StartButtonText;

            // 現在使用されているスロット数
            private int m_SlotUsed = 0;

            // ポーズメニュー
            private PauseMenu m_PauseMenu;

            // ポーズ入力
            private InputAction m_PauseAction;

            // UIスケーリング制御
            private CanvasScaler m_CanvasScaler;


            //=====================================================
            // ■ Awake
            //=====================================================

            private void Awake()
            {
                //-------------------------------------------------
                // URPカメラスタック登録
                //-------------------------------------------------
                //
                // UI専用カメラをMainCameraの
                // Camera Stackへ追加する。
                //
                //-------------------------------------------------

                Camera cam =
                    GetComponentInParent<Camera>();

                m_CanvasScaler =
                    GetComponentInParent<CanvasScaler>();

                var data =
                    Camera.main
                        .GetUniversalAdditionalCameraData();

                if (!data.cameraStack.Contains(cam))
                {
                    data.cameraStack.Add(cam);
                }
            }


            //=====================================================
            // ■ Start
            //=====================================================

            private void Start()
            {
                //-------------------------------------------------
                // モバイルUIを一旦非表示
                //-------------------------------------------------

                if (MobileUIControl.Instance != null)
                {
                    MobileUIControl.Instance.Hide();
                }


                //-------------------------------------------------
                // Startボタン設定
                //-------------------------------------------------

                m_StartButton.onClick
                    .AddListener(StartGame);

                // 最初は2人未満なので押せない
                m_StartButton.interactable = false;

                m_StartButtonText =
                    m_StartButton
                        .GetComponentInChildren<TextMeshProUGUI>();

                m_StartButtonText.text =
                    "2 Tanks required";


                //-------------------------------------------------
                // ポーズボタン非表示
                //-------------------------------------------------

                m_PauseMenuButton
                    .gameObject
                    .SetActive(false);


                //-------------------------------------------------
                // ポーズメニュー取得
                //-------------------------------------------------

                m_PauseMenu =
                    FindAnyObjectByType<PauseMenu>(
                        FindObjectsInactive.Include
                    );

                if (m_PauseMenu != null)
                {
                    // 初期化
                    m_PauseMenu.Init();

                    // InputActionを複製
                    m_PauseAction =
                        InputSystem.actions
                            .FindAction("Pause")
                            .Clone();

                    // 常に最前面へ
                    var rectTransform =
                        m_PauseMenuButton
                            .GetComponent<RectTransform>();

                    rectTransform.SetAsLastSibling();
                }


                //-------------------------------------------------
                // タンクPrefab一覧作成
                //-------------------------------------------------

                var tanksPrefabs =
                    new[]
                    {
                        m_GameManager.m_Tank1Prefab,
                        m_GameManager.m_Tank2Prefab,
                        m_GameManager.m_Tank3Prefab,
                        m_GameManager.m_Tank4Prefab
                    };


                //-------------------------------------------------
                // 各プレイヤースロット初期化
                //-------------------------------------------------

                for (int i = 0; i < m_PlayerSlots.Length; ++i)
                {
                    var slot =
                        m_PlayerSlots[i];

                    // タンクプレビュー表示
                    slot.SetTankPreview(
                        tanksPrefabs.Length > i
                            ? tanksPrefabs[i]
                            : tanksPrefabs[0]
                    );

                    int i1 = i;

                    //=============================================
                    // 参加ボタン
                    //=============================================

                    slot.m_AddControlButton.onClick
                        .AddListener(() =>
                        {
                            slot.AddTank();

                            m_SlotUsed++;

                            //-------------------------------------
                            // Player1が存在するか確認
                            //-------------------------------------

                            bool player1Present = false;

                            for (int j = 0;
                                j < m_PlayerSlots.Length;
                                ++j)
                            {
                                if (i1 == j)
                                    continue;

                                if (m_PlayerSlots[j]
                                    .PlayerControlling == 1)
                                {
                                    player1Present = true;
                                }
                            }

                            //-------------------------------------
                            // 最初の参加者はPlayer1
                            //-------------------------------------

                            if (!player1Present)
                            {
                                slot.SetPlayerControlling(1);
                            }
                            else
                            {
                                // それ以外はCPU
                                slot.SetPlayerControlling(-1);
                            }

                            //-------------------------------------
                            // 2人以上でゲーム開始可能
                            //-------------------------------------

                            if (m_SlotUsed >= 2)
                            {
                                m_StartButtonText.text =
                                    "Start";

                                m_StartButton.interactable =
                                    true;
                            }
                        });


                    //=============================================
                    // 参加解除
                    //=============================================

                    slot.m_OffControlButton.onClick
                        .AddListener(() =>
                        {
                            slot.RemoveTank();

                            m_SlotUsed--;

                            // 2人未満なら開始不可
                            if (m_SlotUsed < 2)
                            {
                                m_StartButtonText.text =
                                    "2 Tanks required";

                                m_StartButton.interactable =
                                    false;
                            }
                        });


                    //=============================================
                    // Player1設定
                    //=============================================

                    slot.m_P1ControlButton.onClick
                        .AddListener(() =>
                        {
                            slot.SetPlayerControlling(1);

                            // 他のPlayer1をCPUへ変更
                            for (int j = 0;
                                j < m_PlayerSlots.Length;
                                ++j)
                            {
                                var localSlot =
                                    m_PlayerSlots[j];

                                if (localSlot.IsOpen ||
                                    localSlot == slot)
                                {
                                    continue;
                                }

                                if (localSlot
                                    .PlayerControlling == 1)
                                {
                                    localSlot
                                        .SetPlayerControlling(-1);
                                }
                            }
                        });


                    //=============================================
                    // Player2設定
                    //=============================================

                    slot.m_P2ControlButton.onClick
                        .AddListener(() =>
                        {
                            slot.SetPlayerControlling(2);

                            // 他のPlayer2をCPUへ変更
                            for (int j = 0;
                                j < m_PlayerSlots.Length;
                                ++j)
                            {
                                var localSlot =
                                    m_PlayerSlots[j];

                                if (localSlot.IsOpen ||
                                    localSlot == slot)
                                {
                                    continue;
                                }

                                if (localSlot
                                    .PlayerControlling == 2)
                                {
                                    localSlot
                                        .SetPlayerControlling(-1);
                                }
                            }
                        });

                        //=============================================
                        // Player3設定
                        //=============================================
                        
                        slot.m_P3ControlButton.onClick
                            .AddListener(() =>
                            {
                                slot.SetPlayerControlling(3);

                                // 他のPlayer3をCPUへ変更
                                for (int j = 0;
                                    j < m_PlayerSlots.Length;
                                    ++j)
                                {
                                    var localSlot =
                                        m_PlayerSlots[j];

                                    if (localSlot.IsOpen ||
                                        localSlot == slot)
                                    {
                                        continue;
                                    }

                                    if (localSlot
                                        .PlayerControlling == 3)
                                    {
                                        localSlot
                                            .SetPlayerControlling(-1);
                                    }
                                }
                            });


                        //=============================================
                        // Player4設定
                        //=============================================

                        slot.m_P4ControlButton.onClick
                            .AddListener(() =>
                            {
                                slot.SetPlayerControlling(4);

                                // 他のPlayer4をCPUへ変更
                                for (int j = 0;
                                    j < m_PlayerSlots.Length;
                                    ++j)
                                {
                                    var localSlot =
                                        m_PlayerSlots[j];

                                    if (localSlot.IsOpen ||
                                        localSlot == slot)
                                    {
                                        continue;
                                    }

                                    if (localSlot
                                        .PlayerControlling == 4)
                                    {
                                        localSlot
                                            .SetPlayerControlling(-1);
                                    }
                                }
                            });

                    //=============================================
                    // CPU設定
                    //=============================================

                    slot.m_ComputerControlButton    
                        .onClick
                        .AddListener(() =>
                        {
                            slot.SetPlayerControlling(-1);
                        });
                }
            }


            //=====================================================
            // ■ ゲーム開始
            //=====================================================

            void StartGame()
            {
                // スタート画面を閉じる
                m_StartMenuRoot.gameObject
                    .SetActive(false);

                //-------------------------------------------------
                // プレイヤーデータ生成
                //-------------------------------------------------

                List<GameManager.PlayerData>
                    playerData =
                        new List<GameManager.PlayerData>();

                foreach (var slot in m_PlayerSlots)
                {
                    if (!slot.IsOpen)
                    {
                        playerData.Add(
                            new GameManager.PlayerData()
                            {
                                TankColor =
                                    slot.m_SlotColor,

                                IsComputer =
                                    slot.IsComputer,

                                ControlIndex =
                                    slot.PlayerControlling,

                                UsedPrefab =
                                    slot.TankPrefab
                            });
                    }
                }

                //-------------------------------------------------
                // GameManagerへ渡して開始
                //-------------------------------------------------

                m_GameManager.StartGame(
                    playerData.ToArray()
                );

                //-------------------------------------------------
                // プレビュー削除
                //-------------------------------------------------

                foreach (var slot in m_PlayerSlots)
                {
                    Destroy(slot.TankPreview);
                }

                //-------------------------------------------------
                // モバイルUI表示
                //-------------------------------------------------

                if (MobileUIControl.Instance != null)
                {
                    MobileUIControl.Instance.Show();
                }

                //-------------------------------------------------
                // ポーズ機能有効化
                //-------------------------------------------------

                if (m_PauseMenu != null)
                {
                    m_PauseAction.performed +=
                        evt =>
                        {
                            TogglePause();
                        };

                    m_PauseAction.Enable();

                    m_PauseMenuButton
                        .gameObject
                        .SetActive(true);
                }
            }


            //=====================================================
            // ■ ポーズ切替
            //=====================================================

            private void TogglePause()
            {
                m_PauseMenu.TogglePause();
            }


            //=====================================================
            // ■ Update
            //=====================================================

            private void Update()
            {
                //-------------------------------------------------
                // 画面比率に応じてUIサイズ調整
                //
                // 横画面：
                //     幅基準
                //
                // 縦画面：
                //     高さ基準
                //-------------------------------------------------

                float ratio =
                    Screen.width /
                    (float)Screen.height;

                m_CanvasScaler.matchWidthOrHeight =
                    ratio > 1.0f
                        ? 1.0f
                        : 0.0f;
            }
        }
    }
}