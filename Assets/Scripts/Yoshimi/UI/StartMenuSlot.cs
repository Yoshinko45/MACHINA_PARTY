using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Tanks.Complete
{
    //=====================================================
    // ■ スタートメニュー用タンクスロット
    //=====================================================
    //
    // タイトル画面やキャラクター選択画面で使用するクラス。
    //
    // ・タンクのプレビュー表示
    // ・タンク性能の表示
    // ・プレイヤー1 / プレイヤー2 / CPU の割り当て
    // ・ゲームに参加させるかどうか
    //
    // を管理する。
    //
    //=====================================================

    public class StartMenuSlot : MonoBehaviour
    {
        //=================================================
        // ■ スロットカラー
        //=================================================

        // このスロット専用のタンクカラー
        public Color m_SlotColor;


        //=================================================
        // ■ UI参照
        //=================================================

        [Header("References")]

        // タンクプレビューを表示する位置
        public RectTransform m_TankPreviewPosition;

        // タンク性能表示用テキスト
        public TextMeshProUGUI m_TankStats;

        // 「追加(Add)」ボタン
        public Button m_AddControlButton;


        //=================================================
        // ■ 操作キャラクター選択UI
        //=================================================

        // P1 / P2 / P3 / P4 / CPU / Off の選択肢をまとめたUI
        public RectTransform m_ControlChoiceRoot;

        // プレイヤー1操作
        public Button m_P1ControlButton;

        // プレイヤー2操作
        public Button m_P2ControlButton;

        // プレイヤー3操作
        public Button m_P3ControlButton;

        // プレイヤー4操作
        public Button m_P4ControlButton;

        // CPU操作
        public Button m_ComputerControlButton;

        // 使用しない
        public Button m_OffControlButton;

        //=================================================
        // ■ 背景画像
        //=================================================

        // スロット背景
        public Image BackgroundImage;

        // 未使用状態の背景
        public Sprite OpenSlotBackground;

        // 使用中状態の背景
        public Sprite UsedSlotBackground;


        //=================================================
        // ■ プロパティ
        //=================================================

        // 現在表示しているプレビュータンク
        public GameObject TankPreview { get; set; }

        // このスロットの元になるPrefab
        public GameObject TankPrefab { get; private set; }

        // 操作プレイヤー
        //
        // 1 = Player1
        // 2 = Player2
        // -1 = CPU
        public int PlayerControlling { get; set; }

        // 空きスロットか
        public bool IsOpen { get; set; }

        // CPU操作か
        public bool IsComputer { get; set; }

        //=================================================
        // ■ タンク選択
        //=================================================

        // 選択可能タンク一覧
        public GameObject[] m_TankList;

        // 現在選択中
        private int m_CurrentTankIndex = 0;

        // このスロットに参加したデバイスの表示名
        // （例："WASD" "Gamepad 1"）未参加ならnull
        private string m_DeviceLabel;

        // ←→ボタン
        public Button m_LeftTankButton;
        public Button m_RightTankButton;

        //=================================================
        // ■ 内部変数
        //=================================================
        // メニュー表示用カメラ
        private Camera m_MenuCamera;


        //=================================================
        // ■ Awake
        //=================================================

        private void Awake()
        {
            // 親オブジェクトからカメラ取得
            m_MenuCamera =
                GetComponentInParent<Camera>();

            // 初期状態は空きスロット
            IsOpen = true;


            //=============================================
            // モバイル版はP2を使用しない
            //=============================================

            if (Application.isMobilePlatform)
            {
                m_P2ControlButton
                    .gameObject
                    .SetActive(false);
            }


            //=============================================
            // P1〜P4ボタンは使用しない
            //=============================================
            //
            // 参加はデバイスの参加ボタン（発射ボタン相当）
            // 押下で自動的に行われるようになったため、
            // 手動選択用のこれらのボタンは非表示にする。
            //
            //=============================================

            m_P1ControlButton.gameObject.SetActive(false);
            m_P2ControlButton.gameObject.SetActive(false);
            m_P3ControlButton.gameObject.SetActive(false);
            m_P4ControlButton.gameObject.SetActive(false);

            // 初期背景
            BackgroundImage.sprite =
                OpenSlotBackground;

                // ボタン登録
                m_LeftTankButton.onClick.AddListener(
                    PreviousTank);

                m_RightTankButton.onClick.AddListener(
                    NextTank);

                // タンクリストが存在するなら最初のタンクを表示
                if (m_TankList != null &&
                    m_TankList.Length > 0)
                {
                    m_CurrentTankIndex = 0;
                    SetTankPreview(
                        m_TankList[m_CurrentTankIndex]);
                }
        }


        //=================================================
        // ■ Update
        //=================================================

        private void Update()
        {
            // プレビューが存在するなら
            // ゆっくり回転させる
            if (TankPreview != null)
            {
                TankPreview.transform.Rotate(
                    Vector3.up,
                    45.0f * Time.deltaTime
                );
            }

            // 参加済みデバイスでのタンク切り替え
            if (!IsOpen && !IsComputer)
            {
                PollTankCycleInput();
            }
        }


        //=================================================
        // ■ 参加デバイスでのタンク切り替え入力
        //=================================================

        private void PollTankCycleInput()
        {
            bool goPrevious = false;
            bool goNext = false;

            // WASD参加（A/D）
            if (PlayerControlling == 1 &&
                Keyboard.current != null)
            {
                goPrevious =
                    Keyboard.current.aKey
                        .wasPressedThisFrame;

                goNext =
                    Keyboard.current.dKey
                        .wasPressedThisFrame;
            }

            // 矢印キー参加（←/→）
            else if (PlayerControlling == 2 &&
                     Keyboard.current != null)
            {
                goPrevious =
                    Keyboard.current.leftArrowKey
                        .wasPressedThisFrame;

                goNext =
                    Keyboard.current.rightArrowKey
                        .wasPressedThisFrame;
            }

            // ゲームパッド参加（D-Pad左右）
            else if (PlayerControlling >= 10)
            {
                int gamepadIndex =
                    PlayerControlling - 10;

                if (Gamepad.all.Count > gamepadIndex)
                {
                    Gamepad pad =
                        Gamepad.all[gamepadIndex];

                    goPrevious =
                        pad.dpad.left
                            .wasPressedThisFrame;

                    goNext =
                        pad.dpad.right
                            .wasPressedThisFrame;
                }
            }

            if (goPrevious)
            {
                PreviousTank();
            }
            else if (goNext)
            {
                NextTank();
            }
        }


        //=================================================
        // ■ タンク追加
        //=================================================

        public void AddTank()
        {
            // Addボタン非表示
            m_AddControlButton
                .gameObject
                .SetActive(false);

            // 操作選択UI表示
            m_ControlChoiceRoot
                .gameObject
                .SetActive(true);

            // 使用中状態へ
            IsOpen = false;

            // 背景変更
            BackgroundImage.sprite =
                UsedSlotBackground;

            // 選択中カーソルの色/表示が残っていればクリア
            BackgroundImage.color = Color.white;
            m_DeviceLabel = null;
            UpdateStatsText();
        }


        //=================================================
        // ■ デバイス参加による割り当て
        //=================================================
        //
        // ジョイン画面で、デバイスの参加ボタン
        // （発射ボタン相当）が押された時に呼ばれる。
        //
        //=================================================

        public void ClaimByDevice(
            int controlIndex,
            string deviceLabel)
        {
            // Addボタン非表示
            m_AddControlButton
                .gameObject
                .SetActive(false);

            // 操作選択UI（CPU/Offボタン）を表示
            m_ControlChoiceRoot
                .gameObject
                .SetActive(true);

            // 使用中状態へ
            IsOpen = false;

            // 背景変更（選択中カーソルの色もリセット）
            BackgroundImage.sprite =
                UsedSlotBackground;

            BackgroundImage.color =
                Color.white;

            // 操作割り当て
            SetPlayerControlling(controlIndex);

            // デバイス表示名を保存して反映
            m_DeviceLabel = deviceLabel;
            UpdateStatsText();
        }


        //=================================================
        // ■ タンク削除
        //=================================================

        public void RemoveTank()
        {
            // Addボタン表示
            m_AddControlButton
                .gameObject
                .SetActive(true);

            // 操作選択UI非表示
            m_ControlChoiceRoot
                .gameObject
                .SetActive(false);

            // 操作設定初期化
            SetPlayerControlling(-1);

            // デバイス表示名クリア
            m_DeviceLabel = null;
            UpdateStatsText();

            // 空き状態へ
            IsOpen = true;

            // 背景変更（選択中カーソルの色もリセット）
            BackgroundImage.sprite =
                OpenSlotBackground;

            BackgroundImage.color =
                Color.white;
        }


        //=================================================
        // ■ 操作プレイヤー設定
        //=================================================
        public void SetPlayerControlling(
            int playerNumber)
        {
            //---------------------------------------------
            // 以前の選択ボタンを再び押せるようにする
            //---------------------------------------------

            switch (PlayerControlling)
            {
                // Player1
                case 1:

                    m_P1ControlButton.interactable =
                        true;

                    break;

                // Player2
                case 2:

                    m_P2ControlButton.interactable =
                        true;

                    break;

                // Player3
                case 3:

                    m_P3ControlButton.interactable =
                        true;

                    break;

                // Player4
                case 4:

                    m_P4ControlButton.interactable =
                        true;

                    break;

                // CPU
                case -1:

                    m_ComputerControlButton
                        .interactable = true;

                    break;
            }

            //---------------------------------------------
            // 新しい操作プレイヤー設定
            //---------------------------------------------
            PlayerControlling =
                playerNumber;


            //---------------------------------------------
            // 選択されたボタンを押せなくする
            //---------------------------------------------
            switch (playerNumber)
            {
                // Player1（WASD）
                case 1:

                    m_P1ControlButton.interactable =
                        false;

                    IsComputer = false;

                    break;

                // Player2（Arrow）
                case 2:

                    m_P2ControlButton.interactable =
                        false;

                    IsComputer = false;

                    break;

                // Player3（Gamepad1）
                case 3:

                    m_P3ControlButton.interactable =
                        false;

                    IsComputer = false;

                    break;

                // Player4（Gamepad2）
                case 4:

                    m_P4ControlButton.interactable =
                        false;

                    IsComputer = false;

                    break;

                // CPU
                case -1:

                    m_ComputerControlButton
                        .interactable = false;

                    IsComputer = true;

                    break;

                // ゲームパッド（10以上）
                default:

                    if (playerNumber >= 10)
                    {
                        IsComputer = false;
                    }

                    break;
            }
        }

        //=================================================
        // ■ タンクプレビュー生成
        //=================================================

        public void SetTankPreview(
            GameObject prefab)
        {
            //---------------------------------------------
            // 既存プレビュー削除
            //---------------------------------------------

            if (TankPreview != null)
            {
                Destroy(TankPreview);
            }


            //---------------------------------------------
            // Prefab保存
            //---------------------------------------------

            TankPrefab = prefab;


            //---------------------------------------------
            // プレビュー生成
            //---------------------------------------------

            TankPreview =
                Instantiate(prefab);


            //---------------------------------------------
            // 必要コンポーネント取得
            //---------------------------------------------

            TankMovement move =
                TankPreview.GetComponent<TankMovement>();

            TankShooting shoot =
                TankPreview.GetComponent<TankShooting>();

            TankHealth health =
                TankPreview.GetComponent<TankHealth>();

            InputReader input =
                TankPreview.GetComponent<InputReader>();


            //---------------------------------------------
            // ゲーム処理停止
            //---------------------------------------------

            // プレビューなので入力や発射は不要
            move.enabled = false;
            shoot.enabled = false;

            // プレビューは操作を受け付けないため、
            // InputReaderも無効化しておく。
            // （無効化しないと、Prefabに残ったままの
            // 　ControlIndexの値でUpdate()が毎フレーム走り、
            // 　「未対応の ControlIndex」警告が出続けてしまう）
            if (input != null)
            {
                input.enabled = false;
            }


            //---------------------------------------------
            // ステータス表示更新
            //---------------------------------------------

            UpdateStatsText();


            //---------------------------------------------
            // プレビュー位置設定
            //---------------------------------------------

            Vector3 position =
                m_MenuCamera.WorldToScreenPoint(
                    m_TankPreviewPosition.position
                );

            TankPreview.transform.position =
                m_MenuCamera.ScreenToWorldPoint(
                    position
                )
                + Vector3.back * 3.0f;


            //---------------------------------------------
            // タンクカラー変更
            //---------------------------------------------

            MeshRenderer[] renderers =
                TankPreview
                .GetComponentsInChildren<MeshRenderer>();

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                MeshRenderer renderer =
                    renderers[i];

                for (int j = 0;
                     j < renderer.materials.Length;
                     ++j)
                {
                    // TankColorマテリアルを探す
                    if (renderer.materials[j]
                        .name
                        .Contains("TankColor"))
                    {
                        // スロットカラー適用
                        renderer.materials[j]
                            .color =
                            m_SlotColor;
                    }
                }
            }

            //---------------------------------------------
            // 音を鳴らさないようにする
            //---------------------------------------------

            AudioSource[] audioSource =
                TankPreview
                .GetComponentsInChildren<AudioSource>();

            foreach (AudioSource source
                     in audioSource)
            {
                Destroy(source);
            }
        }

        //=================================================
        // ■ ステータステキスト更新
        //=================================================
        //
        // 現在のプレビュータンクの性能に加え、
        // 参加済みデバイスがあればその表示名を先頭に表示する。
        //
        //=================================================

        private void UpdateStatsText()
        {
            if (TankPreview == null)
            {
                return;
            }

            TankMovement move =
                TankPreview.GetComponent<TankMovement>();

            TankShooting shoot =
                TankPreview.GetComponent<TankShooting>();

            TankHealth health =
                TankPreview.GetComponent<TankHealth>();

            string prefix =
                string.IsNullOrEmpty(m_DeviceLabel)
                    ? ""
                    : $"<b>{m_DeviceLabel}</b>\n";

            m_TankStats.text =
                prefix +
                $"Speed {move.m_Speed}\n" +
                $"Damage {shoot.m_MaxDamage}\n" +
                $"Health: {health.m_StartingHealth}";
        }

        //=================================================
        // ■ 次のタンク
        //=================================================
        public void NextTank()
        {
            m_CurrentTankIndex++;

            if (m_CurrentTankIndex >= m_TankList.Length)
            {
                m_CurrentTankIndex = 0;
            }

            SetTankPreview(
                m_TankList[m_CurrentTankIndex]
            );
        }

        //=================================================
        // ■ 前のタンク
        //=================================================
        public void PreviousTank()
        {
            m_CurrentTankIndex--;

            if (m_CurrentTankIndex < 0)
            {
                m_CurrentTankIndex =
                    m_TankList.Length - 1;
            }

            SetTankPreview(
                m_TankList[m_CurrentTankIndex]
            );
        }
    }
}