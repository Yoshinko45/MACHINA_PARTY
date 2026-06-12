using TMPro;
using UnityEngine;
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

            // 空き状態へ
            IsOpen = true;

            // 背景変更
            BackgroundImage.sprite =
                OpenSlotBackground;
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


            //---------------------------------------------
            // ゲーム処理停止
            //---------------------------------------------

            // プレビューなので入力や発射は不要
            move.enabled = false;
            shoot.enabled = false;


            //---------------------------------------------
            // ステータス表示更新
            //---------------------------------------------

            m_TankStats.text =
                $"Speed {move.m_Speed}\n" +
                $"Damage {shoot.m_MaxDamage}\n" +
                $"Health: {health.m_StartingHealth}";


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