using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Tanks.Complete
{
    public class GameManager : MonoBehaviour
    {
        // =====================================================
        // ■ ゲーム状態
        // =====================================================
        // 現在ゲームがどの状態にあるかを管理する
        public enum GameState
        {
            MainMenu,  // メインメニュー
            Game       // ゲーム中
        }

        // =====================================================
        // ■ プレイモード
        // =====================================================
        //
        // TankBattle
        //     従来の戦車バトルモード
        //
        // Race
        //     レースモード
        //     専用カメラシステムを使用
        //
        public enum PlayMode
        {
            TankBattle,
            Race
        }

        public class PlayerData
        {
            // CPU操作かどうか
            public bool IsComputer;

            // プレイヤーカラー
            public Color TankColor;

            // 使用するタンクPrefab
            public GameObject UsedPrefab;

            // 入力設定番号
            public int ControlIndex;
        }

        // =====================================================
        // ■ ゲーム設定
        // =====================================================
        // ゲーム勝利に必要なラウンド勝利数
        public int m_NumRoundsToWin = 5;

        // ラウンド開始前待機時間
        public float m_StartDelay = 3f;

        // ラウンド終了後待機時間
        public float m_EndDelay = 3f;

        // カメラ制御
        public CameraControl m_CameraControl;

        // =====================================================
        // ■ モード設定
        // =====================================================
        [Header("Mode")]

        // 現在のゲームモード
        //
        // TankBattle
        //     通常の戦車バトル
        //
        // Race
        //     レースモード
        //
        public PlayMode m_PlayMode = PlayMode.TankBattle;

        // オンラインモードか
        //
        // false = ローカル対戦
        // true  = オンライン対戦
        //
        public bool m_IsOnlineMode = false;

        // レースモード専用
        // 分割画面カメラ管理
        public RaceSplitScreenCameraManager m_RaceCameraManager;

        // =====================================================
        // ■ タンクPrefab
        // =====================================================
        [Header("Tanks Prefabs")]
        public GameObject m_Tank1Prefab;
        public GameObject m_Tank2Prefab;
        public GameObject m_Tank3Prefab;
        public GameObject m_Tank4Prefab;

        // =====================================================
        // ■ 出現ポイント
        // =====================================================
        [FormerlySerializedAs("m_Tanks")]
        public TankManager[] m_SpawnPoints;

        // =====================================================
        // ■ 内部変数
        // =====================================================
        // 現在のゲーム状態
        private GameState m_CurrentState;

        // 現在のラウンド番号
        private int m_RoundNumber;

        // ラウンド開始待機
        private WaitForSeconds m_StartWait;

        // ラウンド終了待機
        private WaitForSeconds m_EndWait;

        // ラウンド勝者
        private TankManager m_RoundWinner;

        // ゲーム勝者
        private TankManager m_GameWinner;

        // メニューから渡されたプレイヤーデータ
        private PlayerData[] m_TankData;

        // プレイヤー人数
        private int m_PlayerCount = 0;

        // メッセージ表示用テキスト
        private TextMeshProUGUI m_TitleText;

        // =====================================================
        // ■ Start
        // =====================================================
        private void Start()
        {
            // 最初はメニュー状態
            m_CurrentState = GameState.MainMenu;

            // メッセージ表示用テキスト取得
            var textRef =
                FindAnyObjectByType<MessageTextReference>(
                    FindObjectsInactive.Include);

            // 見つからなかった場合はエラー
            if (textRef == null)
            {
                Debug.LogError(
                    "You need to add the Menus prefab in the scene to use the GameManager!"
                );
                return;
            }

            m_TitleText = textRef.Text;
            m_TitleText.text = "";

            // タンクPrefab設定確認
            if (m_Tank1Prefab == null ||
                m_Tank2Prefab == null ||
                m_Tank3Prefab == null ||
                m_Tank4Prefab == null)
            {
                Debug.LogError(
                    "You need to assign 4 tank prefab in the GameManager!"
                );
            }
        }

        // =====================================================
        // ■ ゲーム開始
        // =====================================================
        void GameStart()
        {
            // 待機時間生成
            m_StartWait =
                new WaitForSeconds(m_StartDelay);

            m_EndWait =
                new WaitForSeconds(m_EndDelay);

            // 全プレイヤー生成
            SpawnAllTanks();

            // カメラ対象設定
            SetCameraTargets();

            // メインゲームループ開始
            StartCoroutine(GameLoop());
        }

        // =====================================================
        // ■ 状態変更
        // =====================================================
        void ChangeGameState(GameState newState)
        {
            m_CurrentState = newState;

            switch (m_CurrentState)
            {
                case GameState.Game:
                    GameStart();
                    break;
            }
        }

        // =====================================================
        // ■ メニューから呼ばれる
        // =====================================================
        // 選択されたプレイヤーデータを受け取りゲーム開始
        public void StartGame(PlayerData[] playerData)
        {
            m_TankData = playerData;

            m_PlayerCount =
                m_TankData.Length;

            ChangeGameState(GameState.Game);
        }

        // =====================================================
        // ■ 全タンク生成
        // =====================================================
        private void SpawnAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                PlayerData playerData =
                    m_TankData[i];

                // タンク生成
                m_SpawnPoints[i].m_Instance =
                    Instantiate(
                        playerData.UsedPrefab,
                        m_SpawnPoints[i].m_SpawnPoint.position,
                        m_SpawnPoints[i].m_SpawnPoint.rotation
                    );

                    // =====================================================
                    // ■ デバッグ表示
                    // =====================================================
                    //
                    // 正しくシーン内へ生成されたか確認する
                    //
                    // Name
                    //     生成されたオブジェクト名
                    //
                    // Scene
                    //     所属シーン名
                    //
                    // Valid
                    //     シーン参照が有効か
                    //
                    Debug.Log(
                        "Spawned : " +
                        m_SpawnPoints[i].m_Instance.name +
                        "\nScene = " +
                        m_SpawnPoints[i].m_Instance.scene.name +
                        "\nValid = " +
                        m_SpawnPoints[i].m_Instance.scene.IsValid()
                    );

                // 初期状態では必ずプレイヤー操作
                TankMovement mov =
                    m_SpawnPoints[i]
                    .m_Instance
                    .GetComponent<TankMovement>();

                mov.m_IsComputerControlled =
                    false;

                // プレイヤー設定
                m_SpawnPoints[i].m_PlayerNumber =
                    i + 1;

                m_SpawnPoints[i].ControlIndex =
                    playerData.ControlIndex;

                m_SpawnPoints[i].m_PlayerColor =
                    playerData.TankColor;

                m_SpawnPoints[i].m_ComputerControlled =
                    playerData.IsComputer;
            }

            // 全生成後にSetup
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Instance == null)
                    continue;

                m_SpawnPoints[i].Setup(this);
            }
        }

        // =====================================================
        // ■ カメラ追従対象設定
        // =====================================================
        //
        // Battleモード
        //     CameraControl が全プレイヤーを追従
        //
        // Raceモード
        //     RaceSplitScreenCameraManager が
        //     各プレイヤー専用カメラを生成
        //
        // =====================================================
        private void SetCameraTargets()
        {
            Transform[] targets =
                new Transform[m_PlayerCount];

            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] =
                    m_SpawnPoints[i]
                    .m_Instance
                    .transform;
            }

            // =====================================================
            // ■ レースモード
            // =====================================================
            //
            // 通常のCameraControlは使用しない
            //
            // RaceSplitScreenCameraManagerへ
            // プレイヤー情報を渡し、
            // 分割画面カメラを生成する
            //
            if (m_PlayMode == PlayMode.Race)
            {
                // 通常の追従カメラ停止
                if (m_CameraControl != null)
                {
                    m_CameraControl.enabled = false;
                }

                // レース用カメラ生成
                if (m_RaceCameraManager != null)
                {
                    m_RaceCameraManager.Setup(
                        targets,
                        m_IsOnlineMode
                    );
                }

                return;
            }

            // =====================================================
            // ■ バトルモード
            // =====================================================
            //
            // 従来のCameraControlを使用
            //
            // 全プレイヤーを対象に
            // 自動ズーム・追従を行う
            //
            if (m_CameraControl != null)
            {
                m_CameraControl.enabled = true;

                m_CameraControl.m_Targets =
                    targets;
            }
        }

        // =====================================================
        // ■ メインゲームループ
        // =====================================================
        //
        // RoundStarting
        //      ↓
        // RoundPlaying
        //      ↓
        // RoundEnding
        //      ↓
        // 次ラウンド
        //
        // =====================================================
        private IEnumerator GameLoop()
        {
            yield return StartCoroutine(
                RoundStarting());

            yield return StartCoroutine(
                RoundPlaying());

            yield return StartCoroutine(
                RoundEnding());

            // ゲーム勝者がいる
            if (m_GameWinner != null)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                StartCoroutine(GameLoop());
            }
        }

        // =====================================================
        // ■ ラウンド開始処理
        // =====================================================
        private IEnumerator RoundStarting()
        {
            // タンク初期化
            ResetAllTanks();

            // 操作禁止
            DisableTankControl();

            // =====================================================
            // ■ バトルモード専用
            // =====================================================
            //
            // ラウンド開始時に
            // カメラ位置とズームをリセット
            //
            // レースモードでは
            // RaceCameraFollow が担当するため
            // 実行しない
            //
            if (m_PlayMode == PlayMode.TankBattle &&
                m_CameraControl != null)
            {
                m_CameraControl.SetStartPositionAndSize();
            }

            // ラウンド番号更新
            m_RoundNumber++;

            // 表示
            m_TitleText.text =
                "ROUND " + m_RoundNumber;

            yield return m_StartWait;
        }

        // =====================================================
        // ■ ラウンド進行中
        // =====================================================
        private IEnumerator RoundPlaying()
        {
            EnableTankControl();

            m_TitleText.text =
                string.Empty;

            // 生存者が1人になるまで継続
            while (!OneTankLeft())
            {
                yield return null;
            }
        }

        // =====================================================
        // ■ ラウンド終了
        // =====================================================
        private IEnumerator RoundEnding()
        {
            DisableTankControl();

            m_RoundWinner = null;

            // ラウンド勝者取得
            m_RoundWinner =
                GetRoundWinner();

            // 勝利数加算
            if (m_RoundWinner != null)
            {
                m_RoundWinner.m_Wins++;
            }

            // ゲーム勝者判定
            m_GameWinner =
                GetGameWinner();

            // 結果表示
            m_TitleText.text =
                EndMessage();

            yield return m_EndWait;
        }

        // =====================================================
        // ■ 生存者が1人以下か
        // =====================================================
        private bool OneTankLeft()
        {
            int numTanksLeft = 0;

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i]
                    .m_Instance.activeSelf)
                {
                    numTanksLeft++;
                }
            }

            return numTanksLeft <= 1;
        }

        // =====================================================
        // ■ ラウンド勝者取得
        // =====================================================
        private TankManager GetRoundWinner()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i]
                    .m_Instance.activeSelf)
                {
                    return m_SpawnPoints[i];
                }
            }

            // 全滅なら引き分け
            return null;
        }

        // =====================================================
        // ■ ゲーム勝者取得
        // =====================================================
        private TankManager GetGameWinner()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Wins ==
                    m_NumRoundsToWin)
                {
                    return m_SpawnPoints[i];
                }
            }

            return null;
        }

        // =====================================================
        // ■ ラウンド終了メッセージ作成
        // =====================================================
        private string EndMessage()
        {
            string message = "DRAW!";

            if (m_RoundWinner != null)
            {
                message =
                    m_RoundWinner
                    .m_ColoredPlayerText +
                    " WINS THE ROUND!";
            }

            message += "\n\n\n\n";

            for (int i = 0; i < m_PlayerCount; i++)
            {
                message +=
                    m_SpawnPoints[i]
                    .m_ColoredPlayerText +
                    ": " +
                    m_SpawnPoints[i].m_Wins +
                    " WINS\n";
            }

            // ゲーム勝利
            if (m_GameWinner != null)
            {
                message =
                    m_GameWinner
                    .m_ColoredPlayerText +
                    " WINS THE GAME!";
            }

            return message;
        }

        // =====================================================
        // ■ 全タンク初期化
        // =====================================================
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].Reset();
            }
        }

        // =====================================================
        // ■ 全プレイヤー操作許可
        // =====================================================
        private void EnableTankControl()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].EnableControl();
            }
        }

        // =====================================================
        // ■ 全プレイヤー操作禁止
        // =====================================================
        private void DisableTankControl()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].DisableControl();
            }
        }
    }
}
