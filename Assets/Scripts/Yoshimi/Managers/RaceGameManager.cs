using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レース全体の進行管理（4人まで）
    // =====================================================================
    //
    // 流れ
    //
    //   車種選択 ─→ スタート演出（カメラ・信号・カウントダウン）─→ レース ─→ 結果発表
    //      ↑                                                              │
    //      └──────── 「車を選びなおす」／「もう一度」（同じ顔ぶれで）─────┘
    //                                                        「タイトルへ」でタイトル画面へ
    //
    // このスクリプトが行うこと
    //   ・車の生成：選んだ人数ぶん、1つの車プレハブから作る。
    //       色をプレイヤーごとに変える（P1 青／P2 赤／P3 緑／P4 黄）
    //       車種の点数に合わせて、性能（最高速度・加速・重さ・曲がりやすさ・滑りにくさ）と、
    //       車体の大きさ（重いほど大きい）を変える
    //   ・スタート位置の並べ方（左右2列）
    //   ・画面分割（RaceSplitScreenCameraManager）と、区画ごとの表示（RaceHud）
    //   ・ミニマップの位置と、プレイヤーのアイコン
    //   ・周回・順位の管理。「人数 − 1」人がゴールしたら、残りの1人は最下位に決まるので、
    //     その人に「最下位確定」を出して、少し待ってからレースを終えて結果を発表する
    //   ・結果発表（RaceResultsUI）からの「もう一度」「車を選びなおす」「タイトルへ」
    //
    // 準備（シーンに置くもの）
    //   ・ヒエラルキーで右クリック → Race → Race Game Manager で、このスクリプトを置く
    //   ・車のプレハブを「Car Prefab」に設定する（空なら、シーンにある車をひな形にして、
    //     その車を最初の位置の基準にする）
    //   ・ほかは、シーンにあるものを自動で見つけて使う
    //     （RaceStartSequence・RaceMinimap・RaceSplitScreenCameraManager・カメラ）
    //
    // =====================================================================

    /// <summary>
    /// レースの進行の段階。
    /// </summary>
    public enum RacePhase
    {
        /// <summary>始まる前</summary>
        Init,

        /// <summary>車種選択中</summary>
        Select,

        /// <summary>車を置いて、スタート演出（カウントダウン）をしている</summary>
        Countdown,

        /// <summary>レース中</summary>
        Racing,

        /// <summary>レースの終わりが決まった。結果を出すまで少し待っている</summary>
        Ending,

        /// <summary>結果発表</summary>
        Results
    }

    /// <summary>
    /// レース全体の進行管理。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RaceGameManager : MonoBehaviour
    {
        // =================================================================
        // ■ 設定：車
        // =================================================================

        [Header("車")]

        [Tooltip("レースに出す車のプレハブ。空なら、シーンにある車（CarMovement が付いたもの）を、" +
            "ひな形として使います（ひな形の車は、レース中は使わず、非表示にします）")]
        public GameObject m_CarPrefab;

        [Tooltip("スタート位置を並べる時の基準（この位置と向きが、先頭の列の真ん中になります）。\n" +
            "空なら、ひな形の車がある位置と向きを基準にします")]
        public Transform m_GridOrigin;

        [Tooltip("スタート位置を1台ずつ自分で決めたい時に設定します（P1〜P4 の順）。\n" +
            "空の項目は、自動で並べた位置を使います")]
        public Transform[] m_SpawnPoints = new Transform[4];

        [Tooltip("スタート位置：左右の車の、真ん中からの距離（メートル）。コースの幅の半分より小さくします")]
        public float m_GridHalfWidth = 2.2f;

        [Tooltip("スタート位置：前後の列の間隔（メートル）")]
        public float m_GridRowSpacing = 6f;

        [Tooltip("スタート位置：右側の車を、後ろへずらす量（メートル。互い違いに並べる）")]
        public float m_GridStagger = 1.5f;


        // =================================================================
        // ■ 設定：車の色
        // =================================================================

        [Header("車の色")]

        [Tooltip("車体の色を、P1〜P4 の色に変えるマテリアルの名前（一部でよい。複数可）。\n" +
            "名前に「CarColor」を含むマテリアルは、ここに書かなくても必ず対象になります。空欄でも構いません")]
        public string[] m_PaintMaterialNames = new string[0];

        [Tooltip("ON：名前で対象が見つからない時に、車体のいちばん目立つ色のマテリアル（と、同じ色合いのもの）を、" +
            "自動で選んで塗る。うまく塗れない時は、OFFにして、上の欄にマテリアルの名前を書いてください")]
        public bool m_AutoDetectPaint = true;


        // =================================================================
        // ■ 設定：車種ごとの性能差
        // =================================================================

        [Header("車種ごとの性能差")]

        [Tooltip("選んだ車種に合わせて、車の性能を変える。OFFなら、全員プレハブのままの性能（色だけ変わる）")]
        public bool m_ApplyCarTypes = true;

        [Tooltip("車種による差の大きさ。0＝全員同じ／1＝標準／2＝差が2倍。\n" +
            "いつも同じ車種が勝つ時は小さく、差がわかりにくい時は大きくします")]
        [Range(0f, 2f)]
        public float m_TuningInfluence = 1f;

        [Tooltip("「おもさ」に合わせて、車体（見た目）の大きさを変える（重いほど大きい）。" +
            "当たり判定は変わりません")]
        public bool m_ScaleModelByWeight = true;


        // =================================================================
        // ■ 設定：周回
        // =================================================================

        [Header("周回")]

        [Tooltip("ONなら、下の周回数を使う。OFFなら、車のプレハブの LapCounter の Max Laps を使う")]
        public bool m_OverrideLaps = false;

        [Tooltip("何周でゴールか")]
        public int m_Laps = 3;

        [Tooltip("ONなら、コースにある CheckPoint の番号の最大値を、チェックポイントの数にする。" +
            "OFFなら、車のプレハブの LapCounter の Total Check Points を使う")]
        public bool m_AutoCheckPointCount = true;


        // =================================================================
        // ■ 設定：レースの終わり
        // =================================================================

        [Header("レースの終わり")]

        [Tooltip("ON：「人数 − 1」人がゴールした時点で、残りの1人は最下位に決まるので、レースを終える" +
            "（4人なら3人がゴールした時）。OFF：全員がゴールするまで続ける")]
        public bool m_EndWhenRankDecided = true;

        [Tooltip("最初の人がゴールしてから、この秒数がたったら、まだゴールしていない人がいてもレースを終える" +
            "（放置された人がいても終わるための安全装置）。0なら使わない")]
        public float m_TimeoutAfterFirstFinish = 60f;

        [Tooltip("レースの終わりが決まってから、結果発表を出すまでの時間（秒）。" +
            "この間に、最後にゴールした人の「FINISH!」と、最下位の人の「最下位確定」が見える")]
        public float m_ResultsDelay = 3.5f;

        [Tooltip("ゴールした車は、ほかの車と当たらないようにする（ゴール後に止まった車が、" +
            "まだ走っている人の邪魔にならないように）")]
        public bool m_GhostFinishedCars = true;


        // =================================================================
        // ■ 設定：画面
        // =================================================================

        [Header("画面")]

        [Tooltip("数字や英語の見出しに使うフォント（空ならTextMeshProの標準）。\n" +
            "日本語（最下位確定など）を出したい時は、日本語の文字が入ったフォントを設定します。" +
            "空の時は、パソコンに入っている日本語フォントを自動で探します。見つからなければ英語で表示します")]
        public TMP_FontAsset m_Font;

        [Tooltip("ONなら、シーンにある古い表示（LapUI・RankUI）を隠す。レース中の表示は、新しい表示（RaceHud）が出す")]
        public bool m_HideLegacyHud = true;

        [Tooltip("1人プレイの時の、ミニマップの置き場所。Keep ＝ シーンで置いた位置のまま")]
        public MinimapSpot m_MinimapSpot1P = MinimapSpot.Keep;

        [Tooltip("2人プレイの時の、ミニマップの置き場所")]
        public MinimapSpot m_MinimapSpot2P = MinimapSpot.Center;

        [Tooltip("3人プレイの時の、ミニマップの置き場所。Empty Slot ＝ 画面分割で空いた枠（右下）")]
        public MinimapSpot m_MinimapSpot3P = MinimapSpot.EmptySlot;

        [Tooltip("4人プレイの時の、ミニマップの置き場所")]
        public MinimapSpot m_MinimapSpot4P = MinimapSpot.Center;

        [Tooltip("ミニマップを画面の真ん中に置く時（2人・4人プレイ）の高さ。画面の高さに対する割合で、" +
            "0.45 ＝ 画面の高さの45%。大きくしたい時は増やし、車が隠れる時は減らします。" +
            "幅は、画面の上で正方形になるように自動で決まります")]
        [Range(0.2f, 0.8f)]
        public float m_MinimapCenterHeightRate = RaceMinimapLayout.CenterHeightRate;

        [Tooltip("空き枠（3人の時の右下）に置く時、枠のうちミニマップが使う割合。" +
            "1 ＝ 枠いっぱい／0.92 ＝ 少し余白。画面の大きさが変わっても、枠の中に収まります")]
        [Range(0.5f, 1f)]
        public float m_MinimapEmptySlotFill = RaceMinimapLayout.EmptySlotFill;

        [Tooltip("2人以上の時、ミニマップのアイコンの中にプレイヤー番号（1〜4）を出す")]
        public bool m_ShowPlayerNumbersOnMinimap = true;


        // =================================================================
        // ■ 設定：シーンのつなぎ先
        // =================================================================

        [Header("シーンのつなぎ先")]

        [Tooltip("「タイトルへ」で戻るシーンの名前（空なら、下の番号を使う）。" +
            "シーンは Build Settings に入れておいてください")]
        public string m_TitleSceneName = "";

        [Tooltip("「タイトルへ」で戻るシーンの Build Settings の番号（タンクモードと同じく、0番がタイトル）")]
        public int m_TitleSceneBuildIndex = 0;


        // =================================================================
        // ■ 設定：開発用
        // =================================================================

        [Header("開発用")]

        [Tooltip("1〜4にすると、車種選択を飛ばして、その人数ですぐにレースを始める" +
            "（P1＝WASD／P2＝矢印キー／P3・P4＝ゲームパッド。1人の時はどの入力でも動く）。0なら通常の流れ")]
        [Range(0, 4)]
        public int m_DebugPlayerCount = 0;

        [Tooltip("進行の主な出来事を、Console に出す")]
        public bool m_LogDetails = true;


        // =================================================================
        // ■ 内部：走者1人ぶん
        // =================================================================

        private sealed class Racer
        {
            public int Index;                  // 走者の番号（0から。画面の区画の順）
            public RacePlayerEntry Entry;      // 選んだ内容
            public GameObject Car;
            public Transform Transform;
            public CarMovement Movement;
            public InputReader Input;
            public LapCounter Laps;
            public Collider[] Colliders;
            public bool Ghost;                 // ほかの車と当たらない状態か

            // 購読した処理（片付ける時に外す）
            public System.Action<int> LapHandler;
            public System.Action<LapCounter> FinishHandler;
        }

        // ミニマップの、もともとの置き場所の記録（元に戻すため）
        private sealed class MinimapPlacement
        {
            public Transform Parent;
            public int SiblingIndex;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector3 Scale;
        }


        // =================================================================
        // ■ 内部変数
        // =================================================================

        private RacePhase m_Phase = RacePhase.Init;

        private readonly List<Racer> m_Racers = new List<Racer>();
        private RaceFlow m_Flow;
        private RacePlayerEntry[] m_Entries = new RacePlayerEntry[0];

        private float m_RaceStartTime;   // GO! の時刻（Time.time）
        private float m_EndAt;           // 結果を出す時刻（Time.unscaledTime）
        private int m_MaxLaps = 3;

        private RaceFonts m_Fonts;
        private RaceHud m_Hud;
        private RaceResultsUI m_Results;
        private CarSelectMenu m_Select;

        private RaceSplitScreenCameraManager m_CameraManager;
        private RaceStartSequence m_Sequence;
        private RaceMinimap m_Minimap;
        private MinimapPlacement m_MinimapOriginal;

        // ミニマップを置いた時の画面の大きさ（変わったら、置き直す）
        private int m_PlacedScreenWidth;
        private int m_PlacedScreenHeight;

        // 車のひな形
        private GameObject m_Template;

        // スタート位置の基準
        private bool m_HasOrigin;
        private Vector3 m_OriginPosition;
        private Quaternion m_OriginRotation = Quaternion.identity;

        // 車を作る時に使う入れ物
        private GameObject m_Holder;       // 非アクティブ。ここで設定を済ませてから、アクティブにする
        private Transform m_CarsRoot;      // 作った車を置く場所

        // コースの情報
        private readonly Dictionary<int, Transform> m_CheckPoints = new Dictionary<int, Transform>();
        private GoalTrigger[] m_Goals = new GoalTrigger[0];
        private int m_CheckPointCount;

        // 順位の計算用（毎フレーム作り直さない）
        private RacerProgress[] m_Progress = new RacerProgress[0];
        private int[] m_Ranks = new int[0];

        private Coroutine m_StartRoutine;

        // 当たり判定を探す時の入れ物
        private readonly RaycastHit[] m_GroundHits = new RaycastHit[16];


        // =================================================================
        // ■ 外から見える状態
        // =================================================================

        /// <summary>今の進行の段階</summary>
        public RacePhase Phase
        {
            get { return m_Phase; }
        }

        /// <summary>走っている人数</summary>
        public int RacerCount
        {
            get { return m_Racers.Count; }
        }

        /// <summary>レース開始（GO!）からの経過秒。GO! の前は 0</summary>
        public float RaceTime
        {
            get
            {
                if (m_Phase != RacePhase.Racing && m_Phase != RacePhase.Ending &&
                    m_Phase != RacePhase.Results)
                {
                    return 0f;
                }

                return Mathf.Max(0f, Time.time - m_RaceStartTime);
            }
        }

        /// <summary>
        /// 走者の車（番号は 0 から。範囲外は null）。
        /// </summary>
        public GameObject GetCar(int index)
        {
            return (index >= 0 && index < m_Racers.Count) ? m_Racers[index].Car : null;
        }

        /// <summary>今の順位（1から。まだ計算していない時は 0）</summary>
        public int GetRank(int index)
        {
            return (index >= 0 && index < m_Ranks.Length) ? m_Ranks[index] : 0;
        }

        /// <summary>ゴール・終了の記録（レース前は null）</summary>
        public RaceFlow Flow
        {
            get { return m_Flow; }
        }

        /// <summary>車のひな形（プレハブ、またはシーンにあった車）</summary>
        public GameObject CarTemplate
        {
            get { return m_Template; }
        }

        /// <summary>レース中の表示</summary>
        public RaceHud Hud
        {
            get { return m_Hud; }
        }

        /// <summary>結果発表</summary>
        public RaceResultsUI Results
        {
            get { return m_Results; }
        }

        /// <summary>車種選択</summary>
        public CarSelectMenu Select
        {
            get { return m_Select; }
        }


        // =================================================================
        // ■ Unity のイベント
        // =================================================================

        private void Awake()
        {
            m_Fonts = RaceFonts.Resolve(m_Font);

            EnsureComponents();
            FindSceneObjects();
            FindTemplate();
            ReadCourse();

            // スタート演出は、このスクリプトが始めるので、自動では始めない
            if (m_Sequence != null)
            {
                m_Sequence.m_PlayOnStart = false;

                m_Sequence.m_OnRaceStarted.AddListener(OnRaceStarted);
            }
        }

        private void Start()
        {
            if (m_Template == null)
            {
                Debug.LogError(
                    "RaceGameManager : 車のひな形がありません。" +
                    "Car Prefab を設定するか、シーンに車（CarMovement が付いたもの）を置いてください。");

                enabled = false;

                return;
            }

            if (m_DebugPlayerCount > 0)
            {
                RacePlayerEntry[] debugEntries = CreateDebugEntries(m_DebugPlayerCount);

                RaceSession.Set(debugEntries);

                BeginRace(debugEntries);
            }
            else if (RaceSession.HasPlayers)
            {
                BeginRace(RaceSession.GetPlayers());
            }
            else
            {
                OpenCarSelect();
            }
        }

        private void Update()
        {
            // 画面の大きさ（縦横比）が変わったら、ミニマップを置き直す
            if (m_MinimapOriginal != null &&
                (Screen.width != m_PlacedScreenWidth || Screen.height != m_PlacedScreenHeight))
            {
                ApplyMinimapPlacement();
            }

            switch (m_Phase)
            {
                case RacePhase.Countdown:
                    UpdateRanking();
                    break;

                case RacePhase.Racing:
                    UpdateRacing();
                    break;

                case RacePhase.Ending:
                    UpdateRanking();

                    if (Time.unscaledTime >= m_EndAt)
                    {
                        ShowResults();
                    }

                    break;
            }
        }

        private void OnDestroy()
        {
            if (m_Sequence != null)
            {
                m_Sequence.m_OnRaceStarted.RemoveListener(OnRaceStarted);
            }

            CleanupRace();

            RestoreMinimapPlacement();

            if (m_Holder != null)
            {
                Destroy(m_Holder);
            }

            if (m_CarsRoot != null)
            {
                Destroy(m_CarsRoot.gameObject);
            }
        }


        // =================================================================
        // ■ 準備：シーンにあるものを探す
        // =================================================================

        /// <summary>
        /// 表示のスクリプト（RaceHud・RaceResultsUI・CarSelectMenu）が無ければ、
        /// このオブジェクトに付ける。
        /// </summary>
        private void EnsureComponents()
        {
            m_Hud = GetOrAdd<RaceHud>();
            m_Results = GetOrAdd<RaceResultsUI>();
            m_Select = GetOrAdd<CarSelectMenu>();
        }

        private T GetOrAdd<T>() where T : Component
        {
            T found = GetComponent<T>();

            if (found == null)
            {
                found = gameObject.AddComponent<T>();
            }

            return found;
        }

        /// <summary>
        /// シーンにある、スタート演出・ミニマップ・画面分割のカメラ管理を探す
        /// （無ければ、カメラ管理だけ作る）。
        /// </summary>
        private void FindSceneObjects()
        {
            m_Sequence =
                FindFirstObjectByType<RaceStartSequence>(FindObjectsInactive.Include);

            m_Minimap =
                FindFirstObjectByType<RaceMinimap>(FindObjectsInactive.Include);

            m_CameraManager =
                FindFirstObjectByType<RaceSplitScreenCameraManager>(FindObjectsInactive.Include);

            if (m_CameraManager == null)
            {
                m_CameraManager = gameObject.AddComponent<RaceSplitScreenCameraManager>();

                // 元になるカメラ：追従カメラ（RaceCameraFollow）が付いたカメラ、無ければメインカメラ
                RaceCameraFollow follow =
                    FindFirstObjectByType<RaceCameraFollow>(FindObjectsInactive.Include);

                if (follow != null)
                {
                    m_CameraManager.m_SourceCamera = follow.GetComponent<Camera>();
                }

                if (m_CameraManager.m_SourceCamera == null)
                {
                    m_CameraManager.m_SourceCamera = Camera.main;
                }
            }
            else if (m_CameraManager.m_SourceCamera == null && m_CameraManager.m_CameraPrefab == null)
            {
                RaceCameraFollow follow =
                    FindFirstObjectByType<RaceCameraFollow>(FindObjectsInactive.Include);

                if (follow != null)
                {
                    m_CameraManager.m_SourceCamera = follow.GetComponent<Camera>();
                }
            }

            if (m_Sequence == null && m_LogDetails)
            {
                Debug.Log(
                    "RaceGameManager : RaceStartSequence が見つからないため、" +
                    "スタート演出なしですぐに始めます。");
            }
        }

        /// <summary>
        /// 車のひな形と、スタート位置の基準を決める。
        /// Car Prefab が空なら、シーンにある車をひな形にして、その位置を基準にし、
        /// ひな形の車は非表示にする（レースには使わない）。
        /// </summary>
        private void FindTemplate()
        {
            // シーンにある車（Player タグのものを優先）
            CarMovement[] sceneCars =
                FindObjectsByType<CarMovement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            CarMovement sceneCar = null;

            for (int i = 0; i < sceneCars.Length; i++)
            {
                if (sceneCars[i] == null)
                {
                    continue;
                }

                if (sceneCar == null || sceneCars[i].CompareTag("Player"))
                {
                    sceneCar = sceneCars[i];

                    if (sceneCar.CompareTag("Player"))
                    {
                        break;
                    }
                }
            }

            // スタート位置の基準：Grid Origin ＞ シーンにある車 ＞ ゴール
            if (m_GridOrigin != null)
            {
                SetOrigin(m_GridOrigin.position, m_GridOrigin.rotation);
            }
            else if (sceneCar != null)
            {
                SetOrigin(sceneCar.transform.position, sceneCar.transform.rotation);
            }

            if (m_CarPrefab != null)
            {
                m_Template = m_CarPrefab;
            }
            else if (sceneCar != null)
            {
                m_Template = sceneCar.gameObject;
            }

            // シーンにある車は、レースには使わない。選択中に動かせてしまわないよう、非表示にする
            if (sceneCar != null)
            {
                sceneCar.gameObject.SetActive(false);
            }

            if (!m_HasOrigin)
            {
                GoalTrigger goal =
                    FindFirstObjectByType<GoalTrigger>(FindObjectsInactive.Include);

                if (goal != null)
                {
                    SetOrigin(goal.transform.position, goal.transform.rotation);

                    Debug.LogWarning(
                        "RaceGameManager : スタート位置の基準が無いため、ゴールの位置を基準にしました。" +
                        "向きが合わない時は、Grid Origin を設定してください。");
                }
                else
                {
                    Debug.LogWarning(
                        "RaceGameManager : スタート位置の基準がありません。" +
                        "Grid Origin（スタート地点の Transform）を設定してください。");
                }
            }
        }

        private void SetOrigin(Vector3 position, Quaternion rotation)
        {
            m_HasOrigin = true;
            m_OriginPosition = position;

            // 向きは、水平方向（Y軸まわり）だけを使う
            m_OriginRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        }

        /// <summary>
        /// コースの情報（チェックポイントとゴールの位置）を集める。
        /// </summary>
        private void ReadCourse()
        {
            m_CheckPoints.Clear();

            CheckPoint[] points =
                FindObjectsByType<CheckPoint>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            int max = 0;

            for (int i = 0; i < points.Length; i++)
            {
                int number = points[i].checkPointNumber;

                max = Mathf.Max(max, number);

                if (!m_CheckPoints.ContainsKey(number))
                {
                    m_CheckPoints.Add(number, points[i].transform);
                }
                else
                {
                    Debug.LogWarning(
                        "RaceGameManager : チェックポイント番号 " + number +
                        " が重なっています（" + points[i].name + "）。");
                }
            }

            // 番号が 1〜最大値 まで揃っているか（抜けていると、周回できなくなる）
            for (int n = 1; n <= max; n++)
            {
                if (!m_CheckPoints.ContainsKey(n))
                {
                    Debug.LogWarning(
                        "RaceGameManager : チェックポイント番号 " + n +
                        " がありません。番号は 1 から順に、抜けなく付けてください" +
                        "（抜けていると、その先へ進めず、周回が増えません）。");
                }
            }

            m_CheckPointCount = max;

            m_Goals =
                FindObjectsByType<GoalTrigger>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
        }


        // =================================================================
        // ■ 車種選択
        // =================================================================

        /// <summary>
        /// 車種選択を開く。前回の顔ぶれがあれば、最初から入れておく。
        /// </summary>
        public void OpenCarSelect()
        {
            CleanupRace();

            m_Phase = RacePhase.Select;

            m_Select.OnStart = OnSelectFinished;
            m_Select.OnBack = BackToTitle;

            // プレビューも、レース中の車と同じ決め方で、色と大きさを変える
            m_Select.SetPaintOptions(m_PaintMaterialNames, m_AutoDetectPaint);

            m_Select.m_SizeInfluence =
                (m_ApplyCarTypes && m_ScaleModelByWeight) ? m_TuningInfluence : 0f;

            m_Select.Open(m_Fonts, m_Template, RaceSession.GetLastPlayers());
        }

        private void OnSelectFinished(RacePlayerEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return;
            }

            RaceSession.Set(entries);

            m_Select.Close();

            BeginRace(entries);
        }


        // =================================================================
        // ■ レースを始める
        // =================================================================

        /// <summary>
        /// 車を置いて、画面を用意し、スタート演出を始める。
        /// </summary>
        /// <param name="entries">出る人（1〜4人。プレイヤー番号の順）</param>
        public void BeginRace(RacePlayerEntry[] entries)
        {
            CleanupRace();

            if (entries == null || entries.Length == 0)
            {
                Debug.LogError("RaceGameManager : 出る人がいないため、レースを始められません。");

                return;
            }

            // 最大4人まで
            int count = Mathf.Min(entries.Length, RaceSession.MaxPlayers);

            m_Entries = new RacePlayerEntry[count];

            for (int i = 0; i < count; i++)
            {
                m_Entries[i] = entries[i].Clone();
            }

            SpawnRacers();

            m_Flow =
                new RaceFlow(
                    count,
                    m_EndWhenRankDecided,
                    m_TimeoutAfterFirstFinish);

            m_Progress = new RacerProgress[count];
            m_Ranks = new int[count];

            RaceSession.Set(m_Entries);

            Rect[] viewports = SetupCameras();

            SetupHud(viewports);
            SetupEmptyArea();
            PlaceMinimap();

            if (m_HideLegacyHud)
            {
                HideLegacyHud();
            }

            m_Phase = RacePhase.Countdown;

            UpdateRanking();

            if (m_LogDetails)
            {
                Debug.Log(
                    "RaceGameManager : レース開始の準備ができました（" + count + "人／" +
                    m_MaxLaps + "周／チェックポイント " + m_CheckPointCount + " 個）");
            }

            m_StartRoutine = StartCoroutine(StartRoutine());
        }

        /// <summary>
        /// 1フレーム待って（新しい車の Start が終わってから）、スタート演出を始める。
        /// </summary>
        private IEnumerator StartRoutine()
        {
            yield return null;

            m_StartRoutine = null;

            if (m_Phase != RacePhase.Countdown)
            {
                yield break;
            }

            if (m_Sequence != null)
            {
                AddHudToSequence();

                m_Sequence.Begin();
            }
            else
            {
                // スタート演出が無い時は、すぐに始める
                OnRaceStarted();
            }
        }

        /// <summary>
        /// GO! の瞬間（スタート演出から呼ばれる）。タイムを数え始める。
        /// </summary>
        private void OnRaceStarted()
        {
            if (m_Phase != RacePhase.Countdown)
            {
                return;
            }

            m_Phase = RacePhase.Racing;
            m_RaceStartTime = Time.time;

            // 全員の操作を受け付け始める
            for (int i = 0; i < m_Racers.Count; i++)
            {
                if (m_Racers[i].Input != null)
                {
                    m_Racers[i].Input.InputLocked = false;
                }
            }

            m_Hud.SetTime(0f);

            if (m_LogDetails)
            {
                Debug.Log("RaceGameManager : レース開始");
            }
        }

        /// <summary>
        /// レース中の毎フレームの処理（順位・タイム・時間切れの確認）。
        /// </summary>
        private void UpdateRacing()
        {
            UpdateRanking();

            float time = RaceTime;

            m_Hud.SetTime(time);

            // 最初のゴールから決めた時間がたって、レースを終える
            if (m_Flow != null && m_Flow.Tick(time))
            {
                BeginEnding();
            }
        }


        // =================================================================
        // ■ 車を作る
        // =================================================================

        /// <summary>
        /// 選んだ人数ぶんの車を、ひな形から作って、色・性能・大きさ・入力を設定する。
        /// 設定は、車を有効にする前に済ませる
        /// （CarMovement は Start で車体の位置を記憶するので、先に設定しておく必要がある）。
        /// </summary>
        private void SpawnRacers()
        {
            // 設定を済ませるための、非アクティブな入れ物
            if (m_Holder == null)
            {
                m_Holder = new GameObject("Race Car Holder (inactive)");
                m_Holder.SetActive(false);
            }

            if (m_CarsRoot == null)
            {
                m_CarsRoot = new GameObject("Race Cars").transform;
            }

            int count = m_Entries.Length;

            // 地面からの高さ（基準の位置での、車の高さ）
            float groundAtOrigin;
            bool originHasGround = FindGround(m_OriginPosition, out groundAtOrigin);

            float clearance = originHasGround ? m_OriginPosition.y - groundAtOrigin : 0f;

            for (int i = 0; i < count; i++)
            {
                RacePlayerEntry entry = m_Entries[i];

                Vector3 position;
                Quaternion rotation;

                GetSpawnPose(i, count, originHasGround, clearance, out position, out rotation);

                GameObject car =
                    Instantiate(m_Template, position, rotation, m_Holder.transform);

                car.name = "Car P" + entry.PlayerNumber;

                Racer racer = ConfigureCar(i, entry, car);

                // 設定が終わったので、シーンへ出す（ここで初めて有効になる）
                car.transform.SetParent(m_CarsRoot, true);
                car.SetActive(true);

                m_Racers.Add(racer);
            }

            // 周回数は、1人目の車の設定を、全員に共通で使う
            m_MaxLaps = m_Racers.Count > 0 ? m_Racers[0].Laps.maxLaps : m_Laps;
        }

        /// <summary>
        /// 車1台を設定する（色・入力・性能・大きさ・周回）。
        /// </summary>
        private Racer ConfigureCar(int index, RacePlayerEntry entry, GameObject car)
        {
            Racer racer = new Racer();

            racer.Index = index;
            racer.Entry = entry;
            racer.Car = car;
            racer.Transform = car.transform;

            // 念のため、タグを Player にする（CheckPoint・GoalTrigger が見分けるのに使う）
            car.tag = "Player";

            racer.Movement = car.GetComponent<CarMovement>();
            racer.Input = car.GetComponent<InputReader>();
            racer.Laps = car.GetComponent<LapCounter>();

            if (racer.Laps == null)
            {
                racer.Laps = car.AddComponent<LapCounter>();
            }

            Rigidbody body = car.GetComponent<Rigidbody>();

            // プレイヤー番号・入力
            if (racer.Movement != null)
            {
                racer.Movement.m_PlayerNumber = entry.PlayerNumber;
            }
            else
            {
                Debug.LogError("RaceGameManager : " + car.name + " に CarMovement がありません。");
            }

            if (racer.Input != null)
            {
                racer.Input.ControlIndex = entry.ControlIndex;

                // スタートの合図（GO!）が出るまでは、操作を受け付けない。
                // スタート演出が車を止めるのは、1フレーム後なので、それまでの間も押さえておく
                racer.Input.InputLocked = true;
            }
            else
            {
                Debug.LogError("RaceGameManager : " + car.name + " に InputReader がありません。");
            }

            // 色（どのマテリアルを塗ったかは、1人目の時だけ Console に出す）
            PaintCar(car, RaceColors.Player(entry.PlayerNumber), index == 0);

            // 性能と大きさ
            if (m_ApplyCarTypes && racer.Movement != null)
            {
                CarSpec spec = CarTypeCatalog.Get(entry.CarTypeIndex).Spec;

                ApplyTuning(racer.Movement, body, spec);

                // 重い車ほど、車体を大きくする（3点が等倍）。当たり判定は変えない
                if (m_ScaleModelByWeight)
                {
                    ScaleModel(
                        racer.Movement.kartModel,
                        CarTuning.Scale(CarTuning.ModelScale(spec.Weight), m_TuningInfluence));
                }
            }

            // 周回
            if (m_OverrideLaps)
            {
                racer.Laps.maxLaps = Mathf.Max(1, m_Laps);
            }

            if (m_AutoCheckPointCount && m_CheckPointCount > 0)
            {
                racer.Laps.totalCheckPoints = m_CheckPointCount;
            }

            racer.Laps.ResetLap();

            // 周回・ゴールの通知を受け取る（index はこの走者の番号）
            int number = index;

            racer.LapHandler = delegate (int lap) { OnLapChanged(number, lap); };
            racer.FinishHandler = delegate (LapCounter counter) { OnCarFinished(number); };

            racer.Laps.OnLapUpdated += racer.LapHandler;
            racer.Laps.OnFinished += racer.FinishHandler;

            racer.Colliders = car.GetComponentsInChildren<Collider>(true);

            return racer;
        }

        /// <summary>
        /// 車体の色を、プレイヤーの色に変える。どのマテリアルを塗るかは CarPaint を参照。
        /// </summary>
        /// <param name="car">色を変える車</param>
        /// <param name="color">プレイヤーの色</param>
        /// <param name="logReport">どのマテリアルを塗ったかを Console に出すか（全員同じ結果なので、1人目の時だけ出す）</param>
        private void PaintCar(GameObject car, Color color, bool logReport)
        {
            string report;

            HashSet<Material> targets =
                CarPaint.FindTargets(car, m_PaintMaterialNames, m_AutoDetectPaint, out report);

            int painted = CarPaint.Apply(car, targets, color);

            if (!logReport)
            {
                return;
            }

            if (painted == 0)
            {
                // 色を変えられなかった：原因と直し方を知らせる
                Debug.LogWarning(
                    "RaceGameManager : 車の色を変えられるマテリアルが見つかりませんでした（車の色は変わりません）。" +
                    report +
                    "。Race Game Manager の「Paint Material Names」に、車体のマテリアルの名前（の一部）を書いてください。");
            }
            else if (m_LogDetails)
            {
                Debug.Log("RaceGameManager : 車の色を変えました。" + report);
            }
        }

        /// <summary>
        /// 車種の点数から、車の性能を変える。倍率は CarTuning にまとめてある。
        /// </summary>
        private void ApplyTuning(CarMovement movement, Rigidbody body, CarSpec spec)
        {
            float k = m_TuningInfluence;

            movement.maxSpeed *=
                CarTuning.Scale(CarTuning.SpeedMultiplier(spec.Speed), k);

            movement.accelResponse *=
                CarTuning.Scale(CarTuning.AccelMultiplier(spec.Accel), k);

            movement.steerStrength *=
                CarTuning.Scale(CarTuning.HandlingMultiplier(spec.Handling), k);

            movement.driftSidewaysFriction *=
                CarTuning.Scale(CarTuning.SlideMultiplier(spec.Grip), k);

            // ダートでの速度は、舗装路の速度（1.0）を超えないようにする
            movement.groundSpeedMultiplier =
                Mathf.Min(
                    1f,
                    movement.groundSpeedMultiplier *
                    CarTuning.Scale(CarTuning.DirtMultiplier(spec.Grip), k));

            if (body != null)
            {
                body.mass *= CarTuning.Scale(CarTuning.MassMultiplier(spec.Weight), k);
            }
        }

        /// <summary>
        /// 車体モデルの大きさを変える。一番下の位置は変えない（大きくしても、地面に埋まったり浮いたりしない）。
        /// </summary>
        private static void ScaleModel(Transform model, float scale)
        {
            if (model == null || Mathf.Approximately(scale, 1f))
            {
                return;
            }

            float before = LowestPoint(model);

            model.localScale = model.localScale * scale;

            float after = LowestPoint(model);

            // 大きく動く時は、計算の間違いかもしれないので、動かさない
            if (!float.IsNaN(before) && !float.IsNaN(after))
            {
                float shift = before - after;

                if (Mathf.Abs(shift) <= 0.5f)
                {
                    model.position += Vector3.up * shift;
                }
            }
        }

        /// <summary>
        /// 車体モデルの、一番低い点の高さ（ワールド座標）。描画するメッシュが無ければ NaN。
        /// メッシュの箱の8つの角を、いまの位置・大きさに直して調べる。
        /// （非アクティブな時でも正しく求めるため、Renderer.bounds は使わない）
        /// </summary>
        private static float LowestPoint(Transform root)
        {
            float lowest = float.PositiveInfinity;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;

                if (mesh == null || filters[i].GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }

                Bounds box = mesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local =
                        box.center +
                        new Vector3(
                            (corner & 1) == 0 ? -box.extents.x : box.extents.x,
                            (corner & 2) == 0 ? -box.extents.y : box.extents.y,
                            (corner & 4) == 0 ? -box.extents.z : box.extents.z);

                    float y = filters[i].transform.TransformPoint(local).y;

                    if (y < lowest)
                    {
                        lowest = y;
                    }
                }
            }

            return float.IsInfinity(lowest) ? float.NaN : lowest;
        }

        /// <summary>
        /// 1台ぶんのスタート位置と向きを決める。
        /// Spawn Points があればそれを使い、無ければ基準の位置から自動で並べる。
        /// </summary>
        private void GetSpawnPose(
            int index,
            int count,
            bool originHasGround,
            float clearance,
            out Vector3 position,
            out Quaternion rotation)
        {
            // 自分で決めた位置
            if (m_SpawnPoints != null &&
                index < m_SpawnPoints.Length &&
                m_SpawnPoints[index] != null)
            {
                position = m_SpawnPoints[index].position;
                rotation = Quaternion.Euler(0f, m_SpawnPoints[index].eulerAngles.y, 0f);

                return;
            }

            // 自動で並べた位置
            float right;
            float back;

            RaceGrid.Slot(index, count, m_GridHalfWidth, m_GridRowSpacing, m_GridStagger, out right, out back);

            rotation = m_OriginRotation;

            position =
                m_OriginPosition +
                rotation * Vector3.right * right -
                rotation * Vector3.forward * back;

            // 地面の高さに合わせる（坂道でも、地面に埋まったり浮いたりしないように）
            float ground;

            if (originHasGround && FindGround(position, out ground))
            {
                position.y = ground + clearance;
            }
        }

        /// <summary>
        /// この位置の真下にある地面の高さを調べる。車やトリガーは地面として数えない。
        /// </summary>
        /// <returns>地面が見つかったら true</returns>
        private bool FindGround(Vector3 position, out float groundY)
        {
            groundY = 0f;

            // 位置より少し上から、真下へ向けて調べる
            Vector3 origin = position + Vector3.up * 3f;

            int hits =
                Physics.RaycastNonAlloc(
                    origin,
                    Vector3.down,
                    m_GroundHits,
                    20f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hits; i++)
            {
                Collider hit = m_GroundHits[i].collider;

                // 車は地面ではない
                if (hit == null || hit.GetComponentInParent<CarMovement>() != null)
                {
                    continue;
                }

                if (m_GroundHits[i].distance < nearest)
                {
                    nearest = m_GroundHits[i].distance;
                    groundY = m_GroundHits[i].point.y;
                    found = true;
                }
            }

            return found;
        }


        // =================================================================
        // ■ カメラ・表示・ミニマップ
        // =================================================================

        /// <summary>
        /// 人数に合わせて画面を分割し、各カメラに車を追わせる。
        /// </summary>
        /// <returns>各プレイヤーの区画（画面の割合 0〜1）</returns>
        private Rect[] SetupCameras()
        {
            int count = m_Racers.Count;

            Transform[] targets = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                targets[i] = m_Racers[i].Transform;
            }

            m_CameraManager.Setup(targets, false);

            Rect[] viewports = new Rect[count];

            for (int i = 0; i < count; i++)
            {
                RaceCameraFollow follow =
                    (m_CameraManager.m_Cameras != null && i < m_CameraManager.m_Cameras.Length)
                        ? m_CameraManager.m_Cameras[i]
                        : null;

                viewports[i] =
                    (follow != null && follow.Camera != null)
                        ? follow.Camera.rect
                        : new Rect(0f, 0f, 1f, 1f);
            }

            return viewports;
        }

        /// <summary>
        /// 区画ごとの表示（周回・順位・タイム）を作る。
        /// </summary>
        private void SetupHud(Rect[] viewports)
        {
            int count = m_Racers.Count;

            int[] numbers = new int[count];

            for (int i = 0; i < count; i++)
            {
                numbers[i] = m_Racers[i].Entry.PlayerNumber;
            }

            m_Hud.Build(m_Fonts, viewports, numbers, m_MaxLaps);

            for (int i = 0; i < count; i++)
            {
                m_Hud.SetLap(i, 1);
            }
        }

        /// <summary>
        /// どのカメラにも映らない空き枠（3人の時の右下）を、表示側で塗りつぶす。
        /// 塗らない設定の時や、空き枠が無い時は、塗りつぶしを消す。
        ///
        /// 塗りつぶしは、カメラではなく UI の画像で行う（Unity 6.2 の URP には、スクリプトで作った
        /// 画面分割のカメラが UI の上に重なってしまう不具合があるため）。
        /// RaceHud が持っているので、レースが終わって表示を片付けると、一緒に消える。
        /// </summary>
        private void SetupEmptyArea()
        {
            Rect area;
            Color color;

            if (m_CameraManager != null &&
                m_CameraManager.TryGetEmptyAreaFill(out area, out color))
            {
                m_Hud.SetEmptyArea(area, color);
            }
            else
            {
                m_Hud.SetEmptyArea(new Rect(0f, 0f, 0f, 0f), Color.clear);
            }
        }

        /// <summary>
        /// スタート演出が、表示を「3」のタイミングで現れさせられるように、
        /// 表示全体を HUD として教える。
        /// </summary>
        private void AddHudToSequence()
        {
            GameObject hud = m_Hud.HudObject;

            if (m_Sequence == null || hud == null)
            {
                return;
            }

            GameObject[] current = m_Sequence.m_ExtraHudObjects;

            int length = current == null ? 0 : current.Length;

            // 前回のレースの表示（破棄済み）を除いて、新しい表示を足す
            List<GameObject> list = new List<GameObject>();

            for (int i = 0; i < length; i++)
            {
                if (current[i] != null && current[i] != hud)
                {
                    list.Add(current[i]);
                }
            }

            list.Add(hud);

            m_Sequence.m_ExtraHudObjects = list.ToArray();
        }

        /// <summary>
        /// 人数に合わせて、ミニマップの位置を変える。
        /// Keep の時は、もともとの位置に戻す。
        ///
        /// 位置と大きさは anchor（画面全体に対する割合）で決める。ピクセルの大きさを計算しないので、
        /// Canvas の大きさが決まる前に呼んでも、同じ場所に収まる。
        /// </summary>
        private void PlaceMinimap()
        {
            if (m_Minimap == null)
            {
                return;
            }

            int count = m_Racers.Count;

            // アイコンの出し方：1人なら自分の車を強調／2人以上ならプレイヤー番号を出す
            m_Minimap.m_HighlightFocus = count == 1;
            m_Minimap.m_ShowPlayerNumber = count > 1 && m_ShowPlayerNumbersOnMinimap;
            m_Minimap.SetFocus(count == 1 ? m_Racers[0].Transform : null);

            ApplyMinimapPlacement();

            // 車の色は、アイコンを作る時にしか読まないので、作り直す
            m_Minimap.RefreshIcons();
        }

        /// <summary>
        /// ミニマップの位置と大きさを、今の人数・画面の縦横比に合わせて決める。
        /// 画面の大きさが変わった時にも、これだけを呼び直す。
        /// </summary>
        private void ApplyMinimapPlacement()
        {
            if (m_Minimap == null)
            {
                return;
            }

            int count = m_Racers.Count;

            MinimapSpot spot =
                RaceMinimapLayout.ForPlayerCount(
                    count,
                    m_MinimapSpot1P,
                    m_MinimapSpot2P,
                    m_MinimapSpot3P,
                    m_MinimapSpot4P);

            // 置いた時の画面の大きさを覚える（変わったら、Update で置き直す）
            m_PlacedScreenWidth = Screen.width;
            m_PlacedScreenHeight = Screen.height;

            RectTransform rect = m_Minimap.transform as RectTransform;

            if (rect == null || m_Hud.HudObject == null)
            {
                return;
            }

            RectTransform hudRoot = m_Hud.HudObject.transform as RectTransform;

            Rect empty = m_CameraManager.EmptyArea;

            bool hasEmpty = m_CameraManager.HasEmptyArea;

            // 画面の縦横比（幅 ÷ 高さ）。分からない時は 0 を渡す（16:9 とみなされる）
            float aspect =
                m_PlacedScreenHeight > 0
                    ? (float)m_PlacedScreenWidth / m_PlacedScreenHeight
                    : 0f;

            MinimapRect box;

            bool move =
                RaceMinimapLayout.Compute(
                    spot,
                    empty.x,
                    empty.y,
                    hasEmpty ? empty.width : 0f,
                    hasEmpty ? empty.height : 0f,
                    m_MinimapCenterHeightRate,
                    aspect,
                    m_MinimapEmptySlotFill,
                    out box);

            if (!move)
            {
                RestoreMinimapPlacement();

                return;
            }

            SaveMinimapPlacement(rect);

            // 表示の親の下へ移す（画面全体を基準に、位置を割合で決められる）
            if (rect.parent != hudRoot)
            {
                rect.SetParent(hudRoot, false);
            }

            // 置く範囲を anchor で決める（大きさは 0 ＝ anchor の範囲がそのまま大きさ）
            rect.anchorMin = new Vector2(box.AnchorMinX, box.AnchorMinY);
            rect.anchorMax = new Vector2(box.AnchorMaxX, box.AnchorMaxY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            // 拡大・縮小が掛かっていると、枠からはみ出したり小さくなったりするので、等倍にする
            rect.localScale = Vector3.one;
        }

        private void SaveMinimapPlacement(RectTransform rect)
        {
            if (m_MinimapOriginal != null)
            {
                return;
            }

            m_MinimapOriginal = new MinimapPlacement();

            m_MinimapOriginal.Parent = rect.parent;
            m_MinimapOriginal.SiblingIndex = rect.GetSiblingIndex();
            m_MinimapOriginal.AnchorMin = rect.anchorMin;
            m_MinimapOriginal.AnchorMax = rect.anchorMax;
            m_MinimapOriginal.Pivot = rect.pivot;
            m_MinimapOriginal.AnchoredPosition = rect.anchoredPosition;
            m_MinimapOriginal.SizeDelta = rect.sizeDelta;
            m_MinimapOriginal.Scale = rect.localScale;
        }

        /// <summary>
        /// ミニマップを、もともとの位置（シーンで置いた位置）に戻す。
        /// </summary>
        private void RestoreMinimapPlacement()
        {
            if (m_MinimapOriginal == null || m_Minimap == null)
            {
                return;
            }

            RectTransform rect = m_Minimap.transform as RectTransform;

            if (rect != null && m_MinimapOriginal.Parent != null)
            {
                rect.SetParent(m_MinimapOriginal.Parent, false);
                rect.SetSiblingIndex(m_MinimapOriginal.SiblingIndex);

                rect.anchorMin = m_MinimapOriginal.AnchorMin;
                rect.anchorMax = m_MinimapOriginal.AnchorMax;
                rect.pivot = m_MinimapOriginal.Pivot;
                rect.anchoredPosition = m_MinimapOriginal.AnchoredPosition;
                rect.sizeDelta = m_MinimapOriginal.SizeDelta;
                rect.localScale = m_MinimapOriginal.Scale;
            }

            m_MinimapOriginal = null;
        }

        /// <summary>
        /// 古い表示（LapUI・RankUI）を隠す。スクリプトは止めず、画像・文字だけを消す。
        /// </summary>
        private void HideLegacyHud()
        {
            LapUI[] laps =
                FindObjectsByType<LapUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < laps.Length; i++)
            {
                if (laps[i].lapText != null)
                {
                    laps[i].lapText.enabled = false;
                }

                HideGraphics(laps[i].gameObject);
            }

            RankUI[] ranks =
                FindObjectsByType<RankUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < ranks.Length; i++)
            {
                if (ranks[i].rankNumberText != null)
                {
                    ranks[i].rankNumberText.enabled = false;
                }

                if (ranks[i].rankSuffixText != null)
                {
                    ranks[i].rankSuffixText.enabled = false;
                }

                HideGraphics(ranks[i].gameObject);
            }
        }

        // そのオブジェクト自身の、画像・文字だけを消す（子や、ミニマップなど他のものは消さない）
        private static void HideGraphics(GameObject target)
        {
            Graphic[] graphics = target.GetComponents<Graphic>();

            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].enabled = false;
            }
        }


        // =================================================================
        // ■ 周回・順位・ゴール
        // =================================================================

        /// <summary>
        /// 新しい周に入った（LapCounter から呼ばれる）。
        /// </summary>
        private void OnLapChanged(int index, int lap)
        {
            if (m_Hud == null)
            {
                return;
            }

            if (lap >= 2)
            {
                m_Hud.AnnounceLap(index, lap);
            }
            else
            {
                m_Hud.SetLap(index, lap);
            }
        }

        /// <summary>
        /// 規定の周回を走り切った（LapCounter から呼ばれる）。
        /// ゴールを記録して、「FINISH!」を出す。
        /// 「人数 − 1」人がゴールしたら、残りの1人に「最下位確定」を出して、レースの終わりに入る。
        /// </summary>
        private void OnCarFinished(int index)
        {
            if (m_Flow == null ||
                (m_Phase != RacePhase.Racing && m_Phase != RacePhase.Ending))
            {
                return;
            }

            float time = RaceTime;

            FinishInfo info = m_Flow.RegisterFinish(index, time);

            // 二重のゴールなどは、何もしない
            if (!info.Accepted)
            {
                return;
            }

            Racer racer = m_Racers[index];

            m_Hud.ShowFinish(index, info.Order, time);

            // ゴールした車は、操作を受け付けず、惰性で止まる
            LockInput(racer);

            if (m_GhostFinishedCars)
            {
                MakeGhost(racer);
            }

            if (m_LogDetails)
            {
                Debug.Log(
                    "RaceGameManager : P" + racer.Entry.PlayerNumber + " が " +
                    info.Order + " 位でゴール（" + RaceTimeFormat.Format(time) + "）");
            }

            // 最下位が確定した人に、「最下位確定」を出す
            if (info.LastPlaceConfirmed && info.LastPlaceIndex >= 0)
            {
                m_Hud.ShowLastPlace(info.LastPlaceIndex);

                if (m_LogDetails)
                {
                    Debug.Log(
                        "RaceGameManager : P" + m_Racers[info.LastPlaceIndex].Entry.PlayerNumber +
                        " は最下位確定");
                }
            }

            if (info.RaceDecided && m_Phase == RacePhase.Racing)
            {
                BeginEnding();
            }
        }

        /// <summary>
        /// レースの終わりが決まった。ゴールしていない車の操作を止めて、
        /// 少し待ってから結果を出す。
        /// </summary>
        private void BeginEnding()
        {
            m_Phase = RacePhase.Ending;
            m_EndAt = Time.unscaledTime + Mathf.Max(0f, m_ResultsDelay);

            for (int i = 0; i < m_Racers.Count; i++)
            {
                if (!m_Flow.HasFinished(i))
                {
                    LockInput(m_Racers[i]);
                }
            }

            if (m_LogDetails)
            {
                Debug.Log("RaceGameManager : レース終了（" + m_Flow.Reason + "）");
            }
        }

        /// <summary>
        /// 全員の順位を計算して、表示に反映する。
        /// 順位は、ゴール済みなら「ゴールした順」、まだなら「進んだ区間の数」と
        /// 「次の目標までの近さ」で決まる（RaceRanking）。
        /// </summary>
        private void UpdateRanking()
        {
            if (m_Flow == null || m_Racers.Count == 0)
            {
                return;
            }

            int count = m_Racers.Count;

            for (int i = 0; i < count; i++)
            {
                Racer racer = m_Racers[i];

                RacerProgress progress = new RacerProgress();

                progress.Finished = m_Flow.HasFinished(i);
                progress.FinishOrder = m_Flow.FinishOrder(i);
                progress.Units = racer.Laps.ProgressUnits;
                progress.DistanceToNext = DistanceToNext(racer);

                m_Progress[i] = progress;
            }

            m_Ranks = RaceRanking.Rank(m_Progress);

            // スタート前（GO! の前）は、並んでいる順（P1 から順に 1位・2位…）にする。
            // 進み具合で決めると、スタート位置の左右のずれだけで、先頭の車が2位に見えることがある
            if (m_Phase == RacePhase.Countdown)
            {
                for (int i = 0; i < count; i++)
                {
                    m_Ranks[i] = i + 1;
                }
            }

            for (int i = 0; i < count; i++)
            {
                m_Hud.SetRank(i, m_Ranks[i], count);
            }
        }

        /// <summary>
        /// 次に通る目標（チェックポイント。全部通り終えていたらゴール）までの、水平方向の距離。
        /// </summary>
        private float DistanceToNext(Racer racer)
        {
            Vector3 position = racer.Transform.position;

            Transform target = null;

            int next = racer.Laps.NextCheckPointNumber;

            if (next > 0)
            {
                m_CheckPoints.TryGetValue(next, out target);
            }

            // 次がゴールの時（または、チェックポイントが見つからない時）：いちばん近いゴール
            if (target == null)
            {
                float best = float.PositiveInfinity;

                for (int i = 0; i < m_Goals.Length; i++)
                {
                    if (m_Goals[i] == null)
                    {
                        continue;
                    }

                    float distance = Flat(m_Goals[i].transform.position - position);

                    if (distance < best)
                    {
                        best = distance;
                        target = m_Goals[i].transform;
                    }
                }
            }

            if (target == null)
            {
                return 0f;
            }

            return Flat(target.position - position);
        }

        // 高さ（Y）を無視した、水平方向の長さ
        private static float Flat(Vector3 vector)
        {
            vector.y = 0f;

            return vector.magnitude;
        }

        /// <summary>
        /// 車の操作を止める。止めた車は、惰性で止まる。
        /// </summary>
        private static void LockInput(Racer racer)
        {
            if (racer.Input != null)
            {
                racer.Input.InputLocked = true;
            }
        }

        /// <summary>
        /// この車を、ほかの全ての車と当たらないようにする
        /// （ゴール後に止まった車が、まだ走っている人の邪魔にならないように）。
        /// </summary>
        private void MakeGhost(Racer racer)
        {
            if (racer.Ghost)
            {
                return;
            }

            racer.Ghost = true;

            for (int i = 0; i < m_Racers.Count; i++)
            {
                Racer other = m_Racers[i];

                if (other == racer)
                {
                    continue;
                }

                for (int a = 0; a < racer.Colliders.Length; a++)
                {
                    for (int b = 0; b < other.Colliders.Length; b++)
                    {
                        if (racer.Colliders[a] != null && other.Colliders[b] != null)
                        {
                            Physics.IgnoreCollision(racer.Colliders[a], other.Colliders[b], true);
                        }
                    }
                }
            }
        }


        // =================================================================
        // ■ 結果発表
        // =================================================================

        /// <summary>
        /// 結果発表を出す。
        /// </summary>
        private void ShowResults()
        {
            if (m_Phase == RacePhase.Results)
            {
                return;
            }

            UpdateRanking();

            m_Phase = RacePhase.Results;

            int count = m_Racers.Count;

            RaceResultRow[] rows = new RaceResultRow[count];

            for (int i = 0; i < count; i++)
            {
                RaceResultRow row = new RaceResultRow();

                row.PlayerNumber = m_Racers[i].Entry.PlayerNumber;
                row.Rank = m_Ranks[i];
                row.Finished = m_Flow.HasFinished(i);
                row.Time = m_Flow.FinishTime(i);
                row.CarTypeIndex = m_Racers[i].Entry.CarTypeIndex;
                row.DeviceLabel = m_Racers[i].Entry.DeviceLabel;

                // ゴールしていない人のうち、いちばん下の順位の人だけが「最下位」
                row.IsLastPlace = !row.Finished && row.Rank == count;

                rows[i] = row;
            }

            m_Results.OnRetry = Retry;
            m_Results.OnCarSelect = BackToCarSelect;
            m_Results.OnTitle = BackToTitle;

            m_Results.Show(rows, m_Fonts);

            // 結果発表の間は、レース中の表示（周回・順位・ミニマップなど）を隠す
            // （結果の画面の後ろに、うっすら透けて見えないように。もう一度走る時は、作り直されて出る）
            if (m_Hud != null)
            {
                m_Hud.SetVisible(false);
            }

            if (m_LogDetails)
            {
                Debug.Log("RaceGameManager : 結果発表");
            }
        }

        /// <summary>
        /// 同じ顔ぶれ・同じ車種で、もう一度走る。
        /// </summary>
        public void Retry()
        {
            RacePlayerEntry[] entries = m_Entries;

            BeginRace(entries);
        }

        /// <summary>
        /// 車種選択に戻る（前回の顔ぶれと車種が、最初から入っている）。
        /// </summary>
        public void BackToCarSelect()
        {
            RaceSession.Clear();

            OpenCarSelect();
        }

        /// <summary>
        /// タイトルへ戻る。
        /// </summary>
        public void BackToTitle()
        {
            CleanupRace();

            m_Select.Close();

            RaceSession.ClearAll();

            RaceSceneLoader.LoadScene(m_TitleSceneName, m_TitleSceneBuildIndex);
        }


        // =================================================================
        // ■ 後片付け
        // =================================================================

        /// <summary>
        /// 今のレースの車・表示・結果を片付ける（次のレースや車種選択の前に呼ぶ）。
        /// </summary>
        private void CleanupRace()
        {
            if (m_StartRoutine != null)
            {
                StopCoroutine(m_StartRoutine);

                m_StartRoutine = null;
            }

            // スタート演出の最中なら、止めて、カメラ・表示を元に戻す
            if (m_Sequence != null)
            {
                m_Sequence.Abort();
            }

            for (int i = 0; i < m_Racers.Count; i++)
            {
                Racer racer = m_Racers[i];

                if (racer.Laps != null)
                {
                    racer.Laps.OnLapUpdated -= racer.LapHandler;
                    racer.Laps.OnFinished -= racer.FinishHandler;
                }

                if (racer.Car != null)
                {
                    // Destroy は、そのフレームの終わりに実行される。
                    // それまで他の処理に見つからないよう、先に非アクティブにする
                    racer.Car.SetActive(false);

                    Destroy(racer.Car);
                }
            }

            m_Racers.Clear();

            m_Flow = null;
            m_Progress = new RacerProgress[0];
            m_Ranks = new int[0];

            // ミニマップは、人数に合わせて表示の親の下へ移していることがある。
            // 表示を片付けると、子のミニマップも一緒に消えてしまうので、先に元の場所へ戻す
            RestoreMinimapPlacement();

            if (m_Hud != null)
            {
                m_Hud.Clear();
            }

            if (m_Results != null)
            {
                m_Results.Hide();
            }

            if (m_Phase != RacePhase.Select)
            {
                m_Phase = RacePhase.Init;
            }
        }


        // =================================================================
        // ■ 開発用
        // =================================================================

        /// <summary>
        /// 開発用の、出る人の一覧を作る。
        /// P1＝WASD／P2＝矢印キー／P3・P4＝ゲームパッド（1人だけの時は、どの入力でも動かせる）。
        /// 車種は、違いが見えやすいように、バランス・スピード・ヘビー・ドリフトの順にする。
        /// </summary>
        private static RacePlayerEntry[] CreateDebugEntries(int count)
        {
            int[] controls = { 1, 2, 10, 11 };
            int[] types = { 0, 1, 3, 4 };

            count = Mathf.Clamp(count, 1, RaceSession.MaxPlayers);

            RacePlayerEntry[] entries = new RacePlayerEntry[count];

            for (int i = 0; i < count; i++)
            {
                int control = count == 1 ? 0 : controls[i];

                entries[i] =
                    new RacePlayerEntry(
                        i + 1,
                        control,
                        CarTypeCatalog.Wrap(types[i]),
                        control == 0 ? "AUTO" : RaceMenuInput.DeviceLabel(control));
            }

            return entries;
        }


        // =================================================================
        // ■ エディタ用（メニュー・スタート位置の表示）
        // =================================================================

#if UNITY_EDITOR

        /// <summary>
        /// ヒエラルキーの右クリック → Race → Race Game Manager で、このスクリプトを置く。
        /// </summary>
        [MenuItem("GameObject/Race/Race Game Manager", false, 10)]
        private static void CreateInScene(MenuCommand command)
        {
            if (FindFirstObjectByType<RaceGameManager>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog(
                    "Race Game Manager",
                    "このシーンには、すでに Race Game Manager があります。",
                    "OK");

                return;
            }

            GameObject go = new GameObject("Race Game Manager");

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);

            go.AddComponent<RaceGameManager>();
            go.AddComponent<RaceHud>();
            go.AddComponent<RaceResultsUI>();
            go.AddComponent<CarSelectMenu>();

            Undo.RegisterCreatedObjectUndo(go, "Create Race Game Manager");

            Selection.activeObject = go;
        }

        /// <summary>
        /// 選んでいる時に、スタート位置（自動で並べた時の位置）をシーンビューに出す。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 origin;
            Quaternion rotation;

            if (!GetGizmoOrigin(out origin, out rotation))
            {
                return;
            }

            for (int i = 0; i < RaceSession.MaxPlayers; i++)
            {
                float right;
                float back;

                // 4人の時の並べ方を表示する
                RaceGrid.Slot(i, RaceSession.MaxPlayers, m_GridHalfWidth, m_GridRowSpacing, m_GridStagger, out right, out back);

                Vector3 position =
                    origin + rotation * Vector3.right * right - rotation * Vector3.forward * back;

                Gizmos.color = RaceColors.Player(i + 1);

                Gizmos.DrawWireSphere(position + Vector3.up * 0.8f, 0.8f);
                Gizmos.DrawLine(position + Vector3.up * 0.8f, position + Vector3.up * 0.8f + rotation * Vector3.forward * 1.5f);

                Handles.Label(position + Vector3.up * 2f, "P" + (i + 1));
            }
        }

        private bool GetGizmoOrigin(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Transform source = m_GridOrigin;

            if (source == null)
            {
                CarMovement car = FindFirstObjectByType<CarMovement>(FindObjectsInactive.Include);

                if (car != null)
                {
                    source = car.transform;
                }
            }

            if (source == null)
            {
                return false;
            }

            position = source.position;
            rotation = Quaternion.Euler(0f, source.eulerAngles.y, 0f);

            return true;
        }

#endif
    }

    // =====================================================================
    // ■ 車体の色を塗る
    // =====================================================================
    //
    // レース中の車（RaceGameManager）と、車種選択の3Dプレビュー（CarSelectMenu）が、
    // 同じ決め方で色を塗るために、ここにまとめてある。
    //
    // 車のプレハブは1種類だけなので、色は「マテリアルの色を変える」ことで変える。
    // どのマテリアルを塗るかは、次の順で決める。
    //
    //   ① 名前に「CarColor」を含むマテリアル（CarManager と同じ決まり）と、
    //      Race Game Manager の Paint Material Names に書いた名前を含むマテリアル
    //   ② ①が1つも無ければ（Auto Detect Paint がONの時だけ）、
    //      車体でいちばん面が多い「色のついた」マテリアルと、同じ色合いのマテリアル
    //      （灰色・白・黒のタイヤやガラスは、色がついていないので対象外）
    //
    // 塗ったマテリアルは、名前を「CarColor …」に付け替える。
    // ミニマップのアイコンが、この名前を目印に車の色を読み取る（RaceMinimap の決まり）ため。
    //
    // 塗るのは「この車だけのマテリアルのコピー」なので、ほかの車や、元のプレハブの色は変わらない。
    //
    // =====================================================================

    /// <summary>
    /// 車体の色を塗る。
    /// </summary>
    public static class CarPaint
    {
        /// <summary>目印の名前。この文字を含むマテリアルは、必ず塗る対象になる</summary>
        public const string MarkName = "CarColor";

        // 自動で選ぶ時の基準
        private const float MinSaturation = 0.30f;  // 彩度：これより低いものは、灰色・白・黒とみなす
        private const float MinValue = 0.15f;       // 明るさ：これより暗いものは、ほぼ黒とみなす
        private const float HueRange = 0.06f;       // 色相の差：これ以内なら同じ色合い（0〜1。0.06 は約22度）

        /// <summary>
        /// 塗る対象のマテリアルを決める。
        /// </summary>
        /// <param name="car">車（プレハブから作った車でも、ひな形でもよい）</param>
        /// <param name="extraNames">名前に含まれていたら対象にする文字（大文字小文字は区別しない）。null・空でもよい</param>
        /// <param name="autoDetect">名前で見つからない時に、色から自動で選ぶか</param>
        /// <param name="report">どう決めたかの説明（Console に出す用）</param>
        /// <returns>塗る対象の、共有マテリアル（アセットそのもの）。1つも無ければ空</returns>
        public static HashSet<Material> FindTargets(
            GameObject car,
            string[] extraNames,
            bool autoDetect,
            out string report)
        {
            HashSet<Material> targets = new HashSet<Material>();

            // 車に使われているマテリアル（重複なし。出てきた順）
            List<Material> all = new List<Material>();

            Renderer[] renderers =
                car != null
                    ? car.GetComponentsInChildren<Renderer>(true)
                    : new Renderer[0];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];

                    if (material == null || all.Contains(material))
                    {
                        continue;
                    }

                    all.Add(material);

                    // ① 名前で選ぶ
                    if (MatchesName(material.name, extraNames))
                    {
                        targets.Add(material);
                    }
                }
            }

            if (targets.Count > 0)
            {
                report = "塗るマテリアル：" + NamesOf(all, targets, true) + "（名前で選択）";

                return targets;
            }

            // ② 色から自動で選ぶ
            if (!autoDetect)
            {
                report = "車のマテリアル：" + NamesOf(all, null, true) + "（Auto Detect Paint がOFFのため、自動では選びません）";

                return targets;
            }

            AutoDetect(renderers, targets);

            if (targets.Count > 0)
            {
                report =
                    "塗るマテリアル：" + NamesOf(all, targets, true) +
                    "（色から自動で選択。塗らないもの：" + NamesOf(all, targets, false) + "）";

                return targets;
            }

            report = "車のマテリアル：" + NamesOf(all, null, true) + "（色のついたものが見つかりませんでした）";

            return targets;
        }

        /// <summary>
        /// 車のマテリアルのうち、対象のものを、色を付けたコピーに差し替える。
        /// コピーは、この車だけのものなので、ほかの車の色は変わらない。
        /// </summary>
        /// <param name="car">色を変える車</param>
        /// <param name="targets">塗る対象（FindTargets で決めたもの）</param>
        /// <param name="color">塗る色</param>
        /// <returns>塗ったマテリアルの数（0 ＝ 色は変わっていない）</returns>
        public static int Apply(GameObject car, HashSet<Material> targets, Color color)
        {
            if (car == null || targets == null || targets.Count == 0)
            {
                return 0;
            }

            int painted = 0;

            Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                // 差し替える前の、元のマテリアル（対象かどうかは、元のもので調べる）
                Material[] original = renderers[i].sharedMaterials;

                bool any = false;

                for (int j = 0; j < original.Length; j++)
                {
                    if (original[j] != null && targets.Contains(original[j]))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    continue;
                }

                // materials ＝ この描画部品だけの、マテリアルのコピー（他の車の色に影響しない）
                Material[] copies = renderers[i].materials;

                for (int j = 0; j < copies.Length && j < original.Length; j++)
                {
                    if (original[j] == null || !targets.Contains(original[j]) || copies[j] == null)
                    {
                        continue;
                    }

                    if (SetColor(copies[j], color))
                    {
                        Mark(copies[j]);

                        painted++;
                    }
                }

                renderers[i].materials = copies;
            }

            return painted;
        }

        /// <summary>
        /// マテリアルの並びのうち、対象のものだけを、色を付けたコピーに置き換えた並びを返す
        /// （3Dプレビュー用。元のマテリアルは変えない）。
        /// 作ったコピーは created に足す（使い終わったら、呼んだ側が Destroy する）。
        /// </summary>
        /// <param name="materials">元のマテリアルの並び</param>
        /// <param name="targets">塗る対象（FindTargets で決めたもの）</param>
        /// <param name="color">塗る色</param>
        /// <param name="created">作ったコピーを入れるリスト</param>
        public static Material[] Tint(
            Material[] materials,
            HashSet<Material> targets,
            Color color,
            List<Material> created)
        {
            if (materials == null || targets == null || targets.Count == 0)
            {
                return materials;
            }

            Material[] result = new Material[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];

                result[i] = source;

                if (source == null || !targets.Contains(source))
                {
                    continue;
                }

                Material copy = new Material(source);

                if (SetColor(copy, color))
                {
                    Mark(copy);

                    created.Add(copy);

                    result[i] = copy;
                }
                else
                {
                    UnityEngine.Object.Destroy(copy);
                }
            }

            return result;
        }

        // 名前に目印（CarColor）か、指定された名前が含まれているか
        private static bool MatchesName(string materialName, string[] extraNames)
        {
            if (materialName.Contains(MarkName))
            {
                return true;
            }

            if (extraNames == null)
            {
                return false;
            }

            for (int i = 0; i < extraNames.Length; i++)
            {
                string key = extraNames[i];

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (materialName.IndexOf(key.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 色から、塗る対象を選ぶ。
        /// 車体の形（MeshRenderer・SkinnedMeshRenderer）のうち、色のついたマテリアルで、
        /// いちばん面が多いものを選び、同じ色合いのマテリアルも一緒に選ぶ。
        /// </summary>
        private static void AutoDetect(Renderer[] renderers, HashSet<Material> targets)
        {
            // マテリアルごとの「面の多さ」（そのマテリアルを使っている部分の、頂点の並びの数の合計）
            Dictionary<Material, int> weights = new Dictionary<Material, int>();
            List<Material> order = new List<Material>();

            for (int i = 0; i < renderers.Length; i++)
            {
                // 煙・軌跡などの描画は、車体ではないので除く
                if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
                {
                    continue;
                }

                Mesh mesh = MeshOf(renderers[i]);

                Material[] materials = renderers[i].sharedMaterials;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];

                    if (material == null)
                    {
                        continue;
                    }

                    int weight = SubMeshSize(mesh, j);

                    int total;

                    if (weights.TryGetValue(material, out total))
                    {
                        weights[material] = total + weight;
                    }
                    else
                    {
                        weights.Add(material, weight);
                        order.Add(material);
                    }
                }
            }

            // 色のついたマテリアルのうち、いちばん面が多いもの
            Material best = null;
            int bestWeight = -1;
            float bestHue = 0f;

            for (int i = 0; i < order.Count; i++)
            {
                float hue;

                if (!IsColorful(order[i], out hue))
                {
                    continue;
                }

                if (weights[order[i]] > bestWeight)
                {
                    best = order[i];
                    bestWeight = weights[order[i]];
                    bestHue = hue;
                }
            }

            if (best == null)
            {
                return;
            }

            // 同じ色合いのマテリアルも、一緒に選ぶ（赤い車体と、赤いホイールなど）
            for (int i = 0; i < order.Count; i++)
            {
                float hue;

                if (IsColorful(order[i], out hue) && HueDistance(hue, bestHue) <= HueRange)
                {
                    targets.Add(order[i]);
                }
            }
        }

        // 色のついたマテリアルか（灰色・白・黒ではない）。色合い（0〜1）も返す
        private static bool IsColorful(Material material, out float hue)
        {
            hue = 0f;

            Color color;

            if (!TryGetBaseColor(material, out color))
            {
                return false;
            }

            float saturation;
            float value;

            Color.RGBToHSV(color, out hue, out saturation, out value);

            return saturation >= MinSaturation && value >= MinValue;
        }

        // 色合いの差（0〜0.5。色相環はつながっているので、赤の両側は近い）
        private static float HueDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);

            return Mathf.Min(d, 1f - d);
        }

        // マテリアルの本体の色。URP のシェーダーは _BaseColor、従来のものは _Color
        private static bool TryGetBaseColor(Material material, out Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");

                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");

                return true;
            }

            color = Color.white;

            return false;
        }

        // マテリアルに色を付ける。色の名前はシェーダーで違うので、あるものすべてに付ける。付けられたら true
        private static bool SetColor(Material material, Color color)
        {
            bool set = false;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);

                set = true;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);

                set = true;
            }

            return set;
        }

        // 名前を「CarColor …」にする（すでに含んでいれば、そのまま）
        private static void Mark(Material material)
        {
            if (!material.name.Contains(MarkName))
            {
                material.name = MarkName + " " + material.name;
            }
        }

        // 描画部品の、メッシュ
        private static Mesh MeshOf(Renderer renderer)
        {
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;

            if (skinned != null)
            {
                return skinned.sharedMesh;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            return filter != null ? filter.sharedMesh : null;
        }

        // メッシュのうち、index 番目のマテリアルの部分の大きさ（頂点の並びの数）。調べられなければ 1
        private static int SubMeshSize(Mesh mesh, int index)
        {
            if (mesh == null || index < 0 || index >= mesh.subMeshCount)
            {
                return 1;
            }

            try
            {
                return Mathf.Max(1, (int)mesh.GetSubMesh(index).indexCount);
            }
            catch (System.Exception)
            {
                return 1;
            }
        }

        // 名前の一覧を「A、B」の形にする。selected が null なら全部。
        // include が true なら selected に入っているもの、false なら入っていないもの
        private static string NamesOf(List<Material> all, HashSet<Material> selected, bool include)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();

            for (int i = 0; i < all.Count; i++)
            {
                if (selected != null && selected.Contains(all[i]) != include)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append("、");
                }

                text.Append(all[i].name);
            }

            return text.Length > 0 ? text.ToString() : "（なし）";
        }
    }
}