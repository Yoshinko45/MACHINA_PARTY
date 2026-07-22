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
            // ■ 参加カーソル関連
            //=====================================================

            [Header("Join Cursor")]

            // ゲームパッドの自由カーソル移動速度
            // （1秒あたりのUIローカル座標移動量）
            public float m_CursorMoveSpeed = 800f;

            // スティックの遊び（これ以下の入力は無視する）
            [Range(0f, 1f)]
            public float m_CursorMoveDeadzone = 0.2f;

            // カーソルをスロットの中心からどれだけ上に浮かせるか
            public float m_CursorYOffset = 28f;

            // 対象（枠やCPU/Offボタン）の実際の範囲から
            // どれだけ外側までヒット判定を広げるか
            public float m_CursorCatchMargin = 24f;


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

            // 選択中（未確定）のデバイス一覧
            private List<PendingJoin> m_PendingJoins =
                new List<PendingJoin>();

            // 参加カーソルの色一覧（参加順に割り当てる）
            private static readonly Color[] s_CursorColors =
                new Color[]
                {
                    new Color(1f,    0.35f, 0.35f), // 赤
                    new Color(0.35f, 0.6f,  1f),    // 青
                    new Color(0.45f, 1f,    0.45f), // 緑
                    new Color(1f,    0.85f, 0.25f), // 黄
                    new Color(1f,    0.4f,  1f),    // ピンク
                    new Color(0.4f,  1f,    1f),    // 水色
                };

            // 次に使うカーソル色のインデックス
            private int m_CursorColorCounter = 0;

            // 各デバイス（ControlIndex）に割り当てたカーソル色
            // 一度割り当てたら、〇ボタンでキャンセルして
            // 再参加しても同じ色を使い続ける
            private Dictionary<int, Color> m_DeviceCursorColors =
                new Dictionary<int, Color>();

            // カーソル用の丸スプライト（初回生成後はキャッシュ）
            private static Sprite s_CursorSprite;

            // ゲームパッド用カーソルの可動範囲（初回使用時に計算）
            private bool  m_CursorBoundsReady = false;
            private float m_CursorMinX;
            private float m_CursorMaxX;
            private float m_CursorMinY;
            private float m_CursorMaxY;

            // UI描画用カメラ（RectTransformUtilityの各種変換に使用）
            private Camera m_UICamera;

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

                m_UICamera =
                    GetComponentInParent<Camera>();

                m_CanvasScaler =
                    GetComponentInParent<CanvasScaler>();

                var data =
                    Camera.main
                        .GetUniversalAdditionalCameraData();

                if (!data.cameraStack.Contains(m_UICamera))
                {
                    data.cameraStack.Add(m_UICamera);
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

                    // クロージャ内で使うためのインデックス退避
                    int i1 = i;

                    // タンクプレビュー表示
                    slot.SetTankPreview(
                        tanksPrefabs.Length > i
                            ? tanksPrefabs[i]
                            : tanksPrefabs[0]
                    );

                    //=============================================
                    // 参加ボタン
                    //=============================================

                    slot.m_AddControlButton.onClick
                        .AddListener(() =>
                        {
                            // 誰かが選択中だった場合に備えて
                            // 保留リストからも外しておく
                            // （カーソルのGameObjectも一緒に破棄）
                            for (int p = m_PendingJoins.Count - 1;
                                 p >= 0;
                                 --p)
                            {
                                if (m_PendingJoins[p]
                                        .HoverSlotIndex == i1)
                                {
                                    if (m_PendingJoins[p]
                                            .CursorObject != null)
                                    {
                                        Destroy(
                                            m_PendingJoins[p]
                                                .CursorObject);
                                    }

                                    m_PendingJoins.RemoveAt(p);
                                }
                            }

                            slot.AddTank();

                            // マウスでのAddは常にCPU割り当て
                            // （人間デバイスは参加ボタンで
                            // 　自動的に割り当てられる）
                            slot.SetPlayerControlling(-1);

                            OnSlotFilled();
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
                    // P1〜P4の手動割り当てボタンは廃止
                    //=============================================
                    //
                    // 参加はデバイスの参加ボタン押下で自動的に
                    // 行われるようになったため
                    // （PollJoinInput参照）、ここでのボタン登録は
                    // 不要になった。
                    //=============================================

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
            // ■ 枠が埋まった時の共通処理
            //=====================================================

            private void OnSlotFilled()
            {
                m_SlotUsed++;

                // 2人以上でゲーム開始可能
                if (m_SlotUsed >= 2)
                {
                    m_StartButtonText.text =
                        "Start";

                    m_StartButton.interactable =
                        true;
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


                //-------------------------------------------------
                // スタートメニュー表示中のみ参加入力を監視
                //-------------------------------------------------

                if (m_StartMenuRoot.gameObject.activeSelf)
                {
                    PollJoinInput();
                    PollStartInput();
                }
            }


            //=====================================================
            // ■ 選択中デバイス情報
            //=====================================================
            //
            // 参加ボタンを1回押した後、確定（もう一度押す）まで
            // どの枠にカーソルがあるかを保持しておくためのクラス。
            //
            //=====================================================

            // カーソルが対象にできるものの種類
            private enum CursorTargetType
            {
                None,
                JoinSlot,   // 空き枠へ参加する
                CpuButton,  // その枠をCPUにする
                OffButton,  // その枠を解除する
            }

            private class PendingJoin
            {
                public int ControlIndex;
                public string DeviceLabel;
                public bool IsGamepad;
                public int HoverSlotIndex;
                public CursorTargetType HoverTargetType;

                // このデバイス専用の丸カーソル表示
                public GameObject CursorObject;
                public RectTransform CursorRect;

                // ゲームパッドの自由カーソル用：現在座標
                // （m_StartMenuRoot のローカル座標系）
                public Vector2 FreePosition;
            }


            //=====================================================
            // ■ 参加入力の監視
            //=====================================================
            //
            // WASD/矢印キー/各ゲームパッドの「発射ボタン」相当を
            // 参加ボタンとして扱う。
            //
            // キーボード勢は左右キーで隣の枠へ一段ずつ移動、
            // ゲームパッド勢は左スティックでカーソルを自由に
            // 動かし、今乗っている枠が対象になる。
            // もう一度同じボタンを押すとその枠に確定する
            // （スマブラのキャラ選択と同じ流れ）。
            //
            //=====================================================

            private void PollJoinInput()
            {
                // WASD（参加/確定=Space、移動=A/D、枠単位）
                HandleKeyboardDeviceInput(
                    1,
                    "WASD",
                    Keyboard.current != null &&
                        Keyboard.current.spaceKey
                            .wasPressedThisFrame,
                    Keyboard.current != null &&
                        Keyboard.current.aKey
                            .wasPressedThisFrame,
                    Keyboard.current != null &&
                        Keyboard.current.dKey
                            .wasPressedThisFrame
                );

                // 矢印キー（参加/確定=Enter、移動=←/→、枠単位）
                HandleKeyboardDeviceInput(
                    2,
                    "Arrow Keys",
                    Keyboard.current != null &&
                        Keyboard.current.enterKey
                            .wasPressedThisFrame,
                    Keyboard.current != null &&
                        Keyboard.current.leftArrowKey
                            .wasPressedThisFrame,
                    Keyboard.current != null &&
                        Keyboard.current.rightArrowKey
                            .wasPressedThisFrame
                );

                // 各ゲームパッド（参加/確定=南ボタン、
                // 移動=左スティックで自由カーソル）
                for (int i = 0;
                     i < Gamepad.all.Count;
                     ++i)
                {
                    Gamepad pad =
                        Gamepad.all[i];

                    HandleGamepadDeviceInput(
                        10 + i,
                        "Gamepad " + (i + 1),
                        pad.buttonSouth
                            .wasPressedThisFrame,
                        pad.buttonEast
                            .wasPressedThisFrame,
                        pad.leftStick.ReadValue()
                    );
                }
            }


            //=====================================================
            // ■ Startボタンの監視（ゲームパッド対応）
            //=====================================================
            //
            // Startボタンはマウスクリックでも押せるが、
            // ゲームパッドの「Start/Options」ボタンでも
            // 同じ動作ができるようにする。
            //
            // WASD勢のSpace／矢印キー勢のEnterは、まだ参加して
            // いないデバイスの「参加ボタン」として既に使って
            // いるため、ここでは流用しない
            // （流用すると、未参加の状態でSpaceを押した瞬間に
            // 　試合開始と参加処理が同じフレームで
            // 　同時に起きてしまう）。
            // キーボードでの試合開始が欲しい場合は、
            // 参加用と衝突しない別のキーを別途割り当てる。
            //
            //=====================================================

            private void PollStartInput()
            {
                // Startボタンが押せない状態なら何もしない
                if (!m_StartButton.interactable)
                {
                    return;
                }

                for (int i = 0;
                     i < Gamepad.all.Count;
                     ++i)
                {
                    if (Gamepad.all[i].startButton
                        .wasPressedThisFrame)
                    {
                        m_StartButton.onClick.Invoke();
                        return;
                    }
                }
            }


            //=====================================================
            // ■ キーボード勢：枠単位でのカーソル移動
            //=====================================================
            //
            // WASD/矢印キーは連続値を取れないため、これまで通り
            // 左右キーで隣の枠へ一段ずつカーソルを移動する。
            //
            //=====================================================

            private void HandleKeyboardDeviceInput(
                int controlIndex,
                string deviceLabel,
                bool confirmPressed,
                bool leftPressed,
                bool rightPressed)
            {
                if (IsControlIndexConfirmed(controlIndex))
                {
                    return;
                }

                PendingJoin pending =
                    FindPending(controlIndex);


                //-------------------------------------------------
                // まだ参加開始していない：ボタンで参加開始
                //-------------------------------------------------

                if (pending == null)
                {
                    if (!confirmPressed)
                    {
                        return;
                    }

                    int firstOpen =
                        FindFirstOpenSlot();

                    if (firstOpen < 0)
                    {
                        return;
                    }

                    CreatePendingJoin(
                        controlIndex,
                        deviceLabel,
                        firstOpen,
                        false
                    );

                    RefreshKeyboardCursorOffsets();

                    return;
                }


                //-------------------------------------------------
                // 選択中：もう一度押したら確定
                //-------------------------------------------------

                if (confirmPressed)
                {
                    ConfirmPending(
                        pending,
                        controlIndex,
                        deviceLabel
                    );

                    return;
                }


                //-------------------------------------------------
                // 選択中：左右で隣の枠へカーソル移動
                //-------------------------------------------------

                if (leftPressed || rightPressed)
                {
                    int newIndex =
                        FindNextOpenSlot(
                            pending.HoverSlotIndex,
                            rightPressed ? 1 : -1
                        );

                    if (newIndex !=
                        pending.HoverSlotIndex)
                    {
                        pending.HoverSlotIndex =
                            newIndex;

                        RefreshKeyboardCursorOffsets();
                    }
                }
            }


            //=====================================================
            // ■ ゲームパッド勢：左スティックでの自由カーソル移動
            //=====================================================
            //
            // 参加後は左スティックでカーソルをUI上で自由に動かし、
            // 今カーソルが近くにある対象（空き枠、またはCPU/Off
            // ボタン）が確定の対象になる。
            // どの対象からも離れすぎている時は南ボタンを押しても
            // 何も起きない（カーソルを対象の近くまで動かしてもらう）。
            //
            // 確定済み（自分の枠に参加中）の場合は、〇ボタンで
            // その枠を解除し、その場でカーソルへ戻れるようにする。
            //
            //=====================================================

            private void HandleGamepadDeviceInput(
                int controlIndex,
                string deviceLabel,
                bool confirmPressed,
                bool cancelPressed,
                Vector2 stickInput)
            {
                //-------------------------------------------------
                // 確定済み：〇ボタンで選択解除してカーソルに戻す
                //-------------------------------------------------

                if (IsControlIndexConfirmed(controlIndex))
                {
                    if (cancelPressed)
                    {
                        CancelConfirmedSlot(
                            controlIndex,
                            deviceLabel
                        );
                    }

                    return;
                }

                PendingJoin pending =
                    FindPending(controlIndex);


                //-------------------------------------------------
                // まだ参加開始していない：南ボタンで参加開始
                //-------------------------------------------------

                if (pending == null)
                {
                    if (!confirmPressed)
                    {
                        return;
                    }

                    int firstOpen =
                        FindFirstOpenSlot();

                    if (firstOpen < 0)
                    {
                        return;
                    }

                    pending =
                        CreatePendingJoin(
                            controlIndex,
                            deviceLabel,
                            firstOpen,
                            true
                        );

                    pending.FreePosition =
                        WorldToStartMenuLocal(
                            m_PlayerSlots[firstOpen]
                                .transform.position
                        ) + new Vector2(0f, m_CursorYOffset);

                    pending.CursorRect.anchoredPosition =
                        pending.FreePosition;

                    return;
                }


                //-------------------------------------------------
                // 左スティックでカーソルを自由に移動
                //-------------------------------------------------

                if (stickInput.sqrMagnitude >
                    m_CursorMoveDeadzone * m_CursorMoveDeadzone)
                {
                    EnsureCursorBounds();

                    pending.FreePosition +=
                        stickInput *
                        m_CursorMoveSpeed *
                        Time.deltaTime;

                    pending.FreePosition.x =
                        Mathf.Clamp(
                            pending.FreePosition.x,
                            m_CursorMinX,
                            m_CursorMaxX
                        );

                    pending.FreePosition.y =
                        Mathf.Clamp(
                            pending.FreePosition.y,
                            m_CursorMinY,
                            m_CursorMaxY
                        );

                    pending.CursorRect.anchoredPosition =
                        pending.FreePosition;

                    UpdateHoverTarget(pending);
                }


                //-------------------------------------------------
                // 南ボタン：今近くにある対象に確定
                //-------------------------------------------------

                if (confirmPressed)
                {
                    ConfirmPending(
                        pending,
                        controlIndex,
                        deviceLabel
                    );
                }
            }


            //=====================================================
            // ■ 確定済み枠のキャンセル（ゲームパッドの〇ボタン）
            //=====================================================
            //
            // 自分が確定している枠のOffボタンと同じ処理を呼び、
            // その場でカーソルを復活させて選び直せるようにする。
            //
            //=====================================================

            private void CancelConfirmedSlot(
                int controlIndex,
                string deviceLabel)
            {
                int ownSlotIndex = -1;

                for (int i = 0;
                     i < m_PlayerSlots.Length;
                     ++i)
                {
                    if (!m_PlayerSlots[i].IsOpen &&
                        !m_PlayerSlots[i].IsComputer &&
                        m_PlayerSlots[i].PlayerControlling ==
                            controlIndex)
                    {
                        ownSlotIndex = i;
                        break;
                    }
                }

                if (ownSlotIndex < 0)
                {
                    return;
                }

                StartMenuSlot slot =
                    m_PlayerSlots[ownSlotIndex];

                // Offボタンと同じ処理で枠を解放する
                if (slot.m_OffControlButton.interactable)
                {
                    slot.m_OffControlButton.onClick.Invoke();
                }

                // その場でカーソルを復活させる
                PendingJoin pending =
                    CreatePendingJoin(
                        controlIndex,
                        deviceLabel,
                        ownSlotIndex,
                        true
                    );

                pending.FreePosition =
                    WorldToStartMenuLocal(
                        slot.transform.position
                    ) + new Vector2(0f, m_CursorYOffset);

                pending.CursorRect.anchoredPosition =
                    pending.FreePosition;
            }


            //=====================================================
            // ■ 参加確定処理（キーボード/ゲームパッド共通）
            //=====================================================
            //
            // カーソルが今対象にしているものによって処理を分ける。
            // ・空き枠 → 参加確定
            // ・CPUボタン → その枠をCPUにする
            // ・Offボタン → その枠を解除する
            //
            //=====================================================

            private void ConfirmPending(
                PendingJoin pending,
                int controlIndex,
                string deviceLabel)
            {
                // カーソルがどの対象の近くにもいない
                // （ゲームパッドの自由カーソルのみ起こりうる）
                if (pending.HoverSlotIndex < 0 ||
                    pending.HoverTargetType ==
                        CursorTargetType.None)
                {
                    return;
                }

                var slot =
                    m_PlayerSlots[pending.HoverSlotIndex];


                //---------------------------------------------
                // CPUボタン：既存のボタン処理をそのまま呼ぶ
                //---------------------------------------------

                if (pending.HoverTargetType ==
                    CursorTargetType.CpuButton)
                {
                    if (slot.m_ComputerControlButton
                        .interactable)
                    {
                        slot.m_ComputerControlButton
                            .onClick.Invoke();
                    }

                    return;
                }


                //---------------------------------------------
                // Offボタン：既存のボタン処理をそのまま呼ぶ
                //---------------------------------------------

                if (pending.HoverTargetType ==
                    CursorTargetType.OffButton)
                {
                    if (slot.m_OffControlButton
                        .interactable)
                    {
                        slot.m_OffControlButton
                            .onClick.Invoke();
                    }

                    return;
                }


                //---------------------------------------------
                // 参加確定：他のデバイスに先に取られていた場合
                //---------------------------------------------

                if (!slot.IsOpen)
                {
                    // ゲームパッドの自由カーソルは
                    // 動かして選び直してもらう
                    // （勝手にワープさせない）
                    if (pending.IsGamepad)
                    {
                        return;
                    }

                    // キーボード勢は別の空き枠へ自動移動
                    int firstOpen =
                        FindFirstOpenSlot();

                    if (firstOpen >= 0)
                    {
                        pending.HoverSlotIndex =
                            firstOpen;

                        RefreshKeyboardCursorOffsets();
                    }
                    else
                    {
                        if (pending.CursorObject != null)
                        {
                            Destroy(pending.CursorObject);
                        }

                        m_PendingJoins.Remove(pending);
                    }

                    return;
                }


                //---------------------------------------------
                // 確定
                //---------------------------------------------

                slot.ClaimByDevice(
                    controlIndex,
                    deviceLabel
                );

                OnSlotFilled();

                if (pending.CursorObject != null)
                {
                    Destroy(pending.CursorObject);
                }

                m_PendingJoins.Remove(pending);

                if (!pending.IsGamepad)
                {
                    RefreshKeyboardCursorOffsets();
                }
            }


            //=====================================================
            // ■ 参加カーソルの生成・移動・整列
            //=====================================================
            //
            // 各デバイスの選択中カーソルは、m_StartMenuRoot の
            // 直接の子として配置し、UI上を自由に動かせるようにする。
            // スプライト画像を用意しなくてもいいように、
            // 円テクスチャをコード側で生成して使う。
            //
            //=====================================================

            // 新しいPendingJoinとカーソルを生成する
            private PendingJoin CreatePendingJoin(
                int controlIndex,
                string deviceLabel,
                int startSlotIndex,
                bool isGamepad)
            {
                PendingJoin pending = new PendingJoin
                {
                    ControlIndex = controlIndex,
                    DeviceLabel = deviceLabel,
                    HoverSlotIndex = startSlotIndex,
                    HoverTargetType = CursorTargetType.JoinSlot,
                    IsGamepad = isGamepad
                };

                pending.CursorObject =
                    CreateCursor(
                        GetCursorColorFor(controlIndex)
                    );

                pending.CursorRect =
                    pending.CursorObject
                        .GetComponent<RectTransform>();

                m_PendingJoins.Add(pending);

                return pending;
            }

            // 指定したデバイスのカーソル色を返す
            // （初めてのデバイスなら新しい色を割り当てて記憶する）
            private Color GetCursorColorFor(int controlIndex)
            {
                Color existing;

                if (m_DeviceCursorColors.TryGetValue(
                        controlIndex,
                        out existing))
                {
                    return existing;
                }

                Color assigned =
                    s_CursorColors[
                        m_CursorColorCounter %
                        s_CursorColors.Length];

                m_CursorColorCounter++;

                m_DeviceCursorColors[controlIndex] =
                    assigned;

                return assigned;
            }

            // カーソルのGameObjectを新規生成する
            // （m_StartMenuRootの子として、中央基準で配置）
            private GameObject CreateCursor(Color color)
            {
                GameObject cursorObj =
                    new GameObject(
                        "JoinCursor",
                        typeof(RectTransform),
                        typeof(Image)
                    );

                RectTransform rect =
                    cursorObj
                        .GetComponent<RectTransform>();

                rect.SetParent(m_StartMenuRoot, false);

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot     = new Vector2(0.5f, 0.5f);
                rect.sizeDelta =
                    new Vector2(48f, 48f);

                Image image =
                    cursorObj.GetComponent<Image>();

                image.sprite = GetCursorSprite();
                image.color  = color;
                image.raycastTarget = false;

                cursorObj.transform.SetAsLastSibling();

                return cursorObj;
            }

            // キーボード勢のカーソル位置をまとめて再計算する
            // （同じ枠に2つ乗った場合は左右にずらす）
            private void RefreshKeyboardCursorOffsets()
            {
                Dictionary<int, List<PendingJoin>> groups =
                    new Dictionary<int, List<PendingJoin>>();

                foreach (PendingJoin p in m_PendingJoins)
                {
                    if (p.IsGamepad)
                    {
                        continue;
                    }

                    if (!groups.ContainsKey(
                            p.HoverSlotIndex))
                    {
                        groups[p.HoverSlotIndex] =
                            new List<PendingJoin>();
                    }

                    groups[p.HoverSlotIndex].Add(p);
                }

                const float spacing = 28f;

                foreach (var kvp in groups)
                {
                    int slotIndex = kvp.Key;
                    List<PendingJoin> list = kvp.Value;

                    Vector2 basePos =
                        WorldToStartMenuLocal(
                            m_PlayerSlots[slotIndex]
                                .transform.position
                        ) + new Vector2(0f, m_CursorYOffset);

                    for (int i = 0; i < list.Count; i++)
                    {
                        float offsetX =
                            (i - (list.Count - 1) / 2f) *
                            spacing;

                        Vector2 pos =
                            basePos + new Vector2(offsetX, 0f);

                        list[i].FreePosition = pos;

                        if (list[i].CursorRect != null)
                        {
                            list[i].CursorRect
                                .anchoredPosition = pos;
                        }
                    }
                }
            }

            // 自由カーソルの現在地から、今どの対象の上にいるかを
            // 判定する。空き枠はカードの実際の範囲全体が参加先に、
            // 埋まっている枠はCPU/Offボタンの実際の範囲が対象になる
            // （距離ではなく矩形あたり判定。カードの端（タンクの
            // 絵の辺りや、下のテキストの辺り）でもちゃんと反応する
            // ようにするため）。
            private void UpdateHoverTarget(
                PendingJoin pending)
            {
                Vector2 cursorScreenPoint =
                    RectTransformUtility.WorldToScreenPoint(
                        m_UICamera,
                        pending.CursorRect.position
                    );

                int foundSlot = -1;
                CursorTargetType foundType =
                    CursorTargetType.None;

                for (int i = 0;
                     i < m_PlayerSlots.Length;
                     ++i)
                {
                    StartMenuSlot slot =
                        m_PlayerSlots[i];

                    if (slot.IsOpen)
                    {
                        // カード全体（背景画像の範囲）が対象
                        if (IsPointOnTarget(
                                cursorScreenPoint,
                                slot.BackgroundImage
                                    .rectTransform))
                        {
                            foundSlot = i;
                            foundType =
                                CursorTargetType.JoinSlot;
                            break;
                        }
                    }
                    else
                    {
                        if (IsPointOnTarget(
                                cursorScreenPoint,
                                slot.m_ComputerControlButton
                                    .GetComponent<RectTransform>()))
                        {
                            foundSlot = i;
                            foundType =
                                CursorTargetType.CpuButton;
                            break;
                        }

                        if (IsPointOnTarget(
                                cursorScreenPoint,
                                slot.m_OffControlButton
                                    .GetComponent<RectTransform>()))
                        {
                            foundSlot = i;
                            foundType =
                                CursorTargetType.OffButton;
                            break;
                        }
                    }
                }

                pending.HoverSlotIndex  = foundSlot;
                pending.HoverTargetType = foundType;
            }

            // カーソルのスクリーン座標が、指定した対象の実際の
            // 範囲（＋少しの余白）に入っているかを判定する
            private bool IsPointOnTarget(
                Vector2 cursorScreenPoint,
                RectTransform targetRect)
            {
                Vector2 localPoint;

                RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        targetRect,
                        cursorScreenPoint,
                        m_UICamera,
                        out localPoint
                    );

                Rect r = targetRect.rect;

                r.xMin -= m_CursorCatchMargin;
                r.xMax += m_CursorCatchMargin;
                r.yMin -= m_CursorCatchMargin;
                r.yMax += m_CursorCatchMargin;

                return r.Contains(localPoint);
            }

            // ゲームパッドカーソルの可動範囲を計算する
            // （スタートメニュー全体のパネル範囲＋余白。
            // 　初回のみ計算）
            private void EnsureCursorBounds()
            {
                if (m_CursorBoundsReady)
                {
                    return;
                }

                Rect menuRect =
                    m_StartMenuRoot.rect;

                float halfWidth  = menuRect.width  / 2f;
                float halfHeight = menuRect.height / 2f;

                // カーソル半径ぶん内側に余白を取り、
                // パネルの端で見切れないようにする
                const float edgeMargin = 24f;

                m_CursorMinX = -halfWidth  + edgeMargin;
                m_CursorMaxX =  halfWidth  - edgeMargin;
                m_CursorMinY = -halfHeight + edgeMargin;
                m_CursorMaxY =  halfHeight - edgeMargin;

                m_CursorBoundsReady = true;
            }

            // ワールド座標をm_StartMenuRootのローカル座標に変換する
            private Vector2 WorldToStartMenuLocal(
                Vector3 worldPos)
            {
                Vector2 screenPoint =
                    RectTransformUtility.WorldToScreenPoint(
                        m_UICamera,
                        worldPos
                    );

                RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        m_StartMenuRoot,
                        screenPoint,
                        m_UICamera,
                        out Vector2 localPoint
                    );

                return localPoint;
            }

            // カーソル用の丸テクスチャをコードで生成する
            // （一度作ったら使い回す）
            private static Sprite GetCursorSprite()
            {
                if (s_CursorSprite != null)
                {
                    return s_CursorSprite;
                }

                int size = 64;

                Texture2D tex =
                    new Texture2D(
                        size,
                        size,
                        TextureFormat.RGBA32,
                        false
                    );

                tex.filterMode = FilterMode.Bilinear;

                Vector2 center =
                    new Vector2(size / 2f, size / 2f);

                float radius = size / 2f - 2f;
                float edge   = 2.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist =
                            Vector2.Distance(
                                new Vector2(
                                    x + 0.5f,
                                    y + 0.5f),
                                center
                            );

                        float alpha;

                        if (dist <= radius - edge)
                        {
                            alpha = 1f;
                        }
                        else if (dist <= radius)
                        {
                            alpha =
                                1f -
                                (dist - (radius - edge)) /
                                edge;
                        }
                        else
                        {
                            alpha = 0f;
                        }

                        tex.SetPixel(
                            x,
                            y,
                            new Color(1f, 1f, 1f, alpha)
                        );
                    }
                }

                tex.Apply();

                s_CursorSprite =
                    Sprite.Create(
                        tex,
                        new Rect(0, 0, size, size),
                        new Vector2(0.5f, 0.5f)
                    );

                return s_CursorSprite;
            }


            //=====================================================
            // ■ 補助関数
            //=====================================================

            // 指定したControlIndexが既にどこかの枠で確定済みか
            private bool IsControlIndexConfirmed(
                int controlIndex)
            {
                for (int i = 0;
                     i < m_PlayerSlots.Length;
                     ++i)
                {
                    var slot =
                        m_PlayerSlots[i];

                    if (!slot.IsOpen &&
                        !slot.IsComputer &&
                        slot.PlayerControlling ==
                            controlIndex)
                    {
                        return true;
                    }
                }

                return false;
            }

            // 指定したControlIndexの選択中情報を探す
            private PendingJoin FindPending(
                int controlIndex)
            {
                foreach (var p in m_PendingJoins)
                {
                    if (p.ControlIndex == controlIndex)
                    {
                        return p;
                    }
                }

                return null;
            }

            // 空いている最初の枠のインデックスを返す
            // （無ければ-1）
            private int FindFirstOpenSlot()
            {
                for (int i = 0;
                     i < m_PlayerSlots.Length;
                     ++i)
                {
                    if (m_PlayerSlots[i].IsOpen)
                    {
                        return i;
                    }
                }

                return -1;
            }

            // fromIndexからdirection方向へ探して
            // 次に空いている枠のインデックスを返す
            // （他に空きが無ければfromIndexのまま）
            private int FindNextOpenSlot(
                int fromIndex,
                int direction)
            {
                int count =
                    m_PlayerSlots.Length;

                for (int step = 1;
                     step <= count;
                     ++step)
                {
                    int idx =
                        ((fromIndex +
                          direction * step) % count +
                         count) % count;

                    if (m_PlayerSlots[idx].IsOpen)
                    {
                        return idx;
                    }
                }

                return fromIndex;
            }
        }
    }
}