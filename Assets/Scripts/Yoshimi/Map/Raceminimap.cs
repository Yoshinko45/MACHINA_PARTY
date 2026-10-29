using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================
    // ■ レースモード用ミニマップ
    // =====================================================
    //
    // ・コースの形を自動で検出して、ミニマップ画像を作る
    // 　（道路のColliderへ真上からRayを撃ち、当たった場所を道路とみなす）
    // ・柵・障害物など余計なものは自動で消す
    // 　（名前に「Fence」を含むものを除外／細いものを消す／チェックポイント・
    // 　 ゴール・車につながっている道路だけを残す）。消しきれないものは
    // 　 Excluded Names（名前）や Excluded Roots（ドラッグ）で指定できる
    // ・シーン内の車（CarMovement）を探して、アイコンを表示する
    // ・カメラが追っている車（自分の車）は大きく強調する
    // 　（画面分割の複数人プレイでは強調をOFFにして、全員を同じ大きさにし、
    // 　 代わりにアイコンの中にプレイヤー番号を出せる）
    // ・GoalTriggerの位置にスタート／ゴールの目印を出す
    // 　（トリガーの中心がコースの端や外側にズレていても、道路の幅の
    // 　 真ん中に寄せるので、目印は必ずコースの上に出る）
    //
    // 【使い方】
    // Canvasの子にRectTransformを持つ空のオブジェクトを作り、
    // このコンポーネントを付ける（Hierarchyの右クリック →
    // UI → Race Minimap でも作れる）。
    // そのオブジェクトの位置とサイズが、そのままミニマップの
    // 表示位置とサイズになる。
    //
    // 画像・Sprite・追加のCameraは一切不要。
    //
    // 【全体の流れ】
    //   1. Awake      … 表示用のUIの骨組み（コース画像を入れる場所）を作る
    //   2. OnEnable   … コース形状の検出を始める（BuildCourseRoutine）
    //   3. 検出       … 真上からRayを撃って道路を調べ、余計なものを消して、
    //                   コース画像（Texture2D）にする。ゴールの目印もここで置く
    //   4. LateUpdate … 毎フレーム、車の位置に合わせてアイコンを動かす
    //                   （車の増減は、一定の間隔で確認する）
    //
    // 【いっしょに使うファイル】
    //   MinimapCourseBuilder … 道路の画像を加工する（隙間埋め・細いものの除去など）
    //   MinimapSprites       … アイコンの画像（丸・縁・三角・市松模様）をコードで作る
    //   RaceMinimapMenu      … 右クリックメニューから、このミニマップを作る（Editor専用）
    //
    // =====================================================

    /// <summary>
    /// レースモード用のミニマップ本体（UIのコンポーネント）。
    /// コースの形を自動で検出して画像にし、その上に、車のアイコンとゴールの目印を表示する。
    /// UIの位置・大きさを決める RectTransform が必要（付いていなければ、自動で付く）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RaceMinimap : MonoBehaviour
    {
        // =====================================================
        // ■ コース形状の取得
        // =====================================================

        [Header("コース形状の取得（Rayで自動検出）")]

        [Tooltip("道路のColliderが入っているルートオブジェクト（例：TrackCourse）。" +
            "空のままなら、名前が「TrackCourse」のオブジェクトを自動で探す")]
        public Transform m_CourseRoot;

        [Tooltip("道路として扱わないタグ。ダート（Groundタグ）などを除外する")]
        public string[] m_ExcludedTags = new string[] { "Ground" };

        [Tooltip("名前にこの文字を含むオブジェクト（その子孫も）を、道路として扱わない。" +
            "大文字小文字は区別しない。ゲーム内の見た目や当たり判定は変わらず、ミニマップから消えるだけ。" +
            "例：Fence と入れると、Fence や Fence (1) の中身がミニマップに出なくなる")]
        public string[] m_ExcludedNames = new string[] { "Fence" };

        [Tooltip("道路として扱わないオブジェクト（子孫も含めて除外）。壁・柵・装飾など")]
        public Transform[] m_ExcludedRoots;

        [Tooltip("ここに入れたオブジェクトの子孫だけを道路として扱う（空ならCourse Rootの全部が対象）。" +
            "例：Straights / Curves / ShortCut / Goal だけを入れると、Fenceなどが丸ごと除外される")]
        public Transform[] m_IncludedRoots;

        [Tooltip("道路として扱うレイヤー")]
        public LayerMask m_IncludedLayers = ~0;  // ~0 ＝ すべてのレイヤーが対象

        [Tooltip("水平に近い面だけを道路とみなす下限（法線のY成分）。" +
            "壁の側面を除外するための設定。1に近いほど水平な面だけになる")]
        [Range(0f, 1f)]
        public float m_MinNormalY = 0.5f;

        [Tooltip("これより背の高いColliderは道路として扱わない（メートル。0なら無効）。" +
            "道路は薄く、障害物・建物・木は背が高い、という場合に有効。" +
            "坂道やジャンプ台など高低差のある道路も背が高く見えるので、使う時は大きめの値にする")]
        public float m_MaxColliderHeight = 0f;

        [Tooltip("コース画像の解像度（長辺のピクセル数）。大きいほど細かいが、検出に時間がかかる。" +
            "遠くに障害物があると調べる範囲が広がって道路が粗くなるので、" +
            "その場合は Excluded Roots / Included Roots で外すと細かくなる")]
        [Range(64, 1024)]
        public int m_Resolution = 384;

        [Tooltip("コースの外側に取る余白（メートル）。縁取りが欠けないようにする")]
        public float m_WorldPadding = 8f;

        [Tooltip("1フレームに撃つRayの数。大きいほど早く終わるが、その間だけ処理が重くなる")]
        public int m_RaysPerFrame = 8000;

        [Tooltip("コースの最高点よりどれだけ高い位置からRayを撃つか（メートル）")]
        public float m_RayHeightMargin = 30f;


        // =====================================================
        // ■ 余計なものを消す
        // =====================================================

        [Header("余計なものを消す（障害物・柵など）")]

        [Tooltip("チェックポイント・ゴール・車の位置からつながっている道路だけを残す。" +
            "コースから離れて浮いている障害物・装飾が消える")]
        public bool m_KeepConnectedToCheckpoints = true;

        [Tooltip("道路がこの幅（ピクセル）以下の隙間で途切れていても、つながっているとみなす。" +
            "ジャンプ台の切れ目などで道路の一部が消えてしまう時は大きくする")]
        [Range(0, 12)]
        public int m_ConnectGapPixels = 2;

        [Tooltip("細いもの（柵・ポール・看板など）を消す強さ。0で無効。" +
            "1＝幅2ピクセル以下、2＝幅4ピクセル以下、3＝幅6ピクセル以下を消す" +
            "（1ピクセルが何mかは、検出完了のログに出る）。" +
            "細い道（ショートカットなど）まで消えてしまう時は0にする")]
        [Range(0, 3)]
        public int m_RemoveThinPixels = 1;

        [Tooltip("余計なものを消した後の道路の形に合わせて、コース画像を切り詰める。" +
            "遠くにあった障害物が消えた時に、コースを枠いっぱいに大きく表示できる")]
        public bool m_FitToCourse = true;

        [Tooltip("検出が終わった時に、どのオブジェクトが何ピクセル分の道路になったかをConsoleに出す。" +
            "余計なものがどこから来ているかを調べる時に使う")]
        public bool m_LogBreakdown = true;

        [Tooltip("内訳を、Course Root から何階層下のオブジェクト単位でまとめるか。" +
            "1＝直下の子（Straights など）、2＝その子（Straights/Straight_03 など）。" +
            "余計なものが大きなグループの中に混ざっている時に増やす")]
        [Range(1, 4)]
        public int m_BreakdownDepth = 1;


        // =====================================================
        // ■ 手描きのマップ画像
        // =====================================================

        [Header("手描きのマップ画像を使う場合（設定すると自動検出はしない）")]

        [Tooltip("真上から見たコースの画像（北＝ワールドの+Zが画像の上）")]
        public Texture2D m_CustomMapTexture;

        [Tooltip("画像の左下に対応するワールド座標（X, Z）")]
        public Vector2 m_CustomWorldMin;

        [Tooltip("画像の右上に対応するワールド座標（X, Z）")]
        public Vector2 m_CustomWorldMax;


        // =====================================================
        // ■ 見た目
        // =====================================================

        [Header("見た目")]

        [Tooltip("道路の中身の色")]
        public Color m_FillColor = new Color(0.08f, 0.10f, 0.14f, 0.60f);

        [Tooltip("道路の縁取りの色")]
        public Color m_OutlineColor = new Color(1f, 1f, 1f, 0.95f);

        [Tooltip("縁取りの太さ（コース画像上のピクセル）")]
        [Range(0, 8)]
        public int m_OutlinePixels = 3;

        [Tooltip("道路パーツの継ぎ目などの隙間を埋める強さ（ピクセル）")]
        [Range(0, 4)]
        public int m_GapClosePixels = 1;

        [Tooltip("ギザギザ・点ノイズを消す回数")]
        [Range(0, 3)]
        public int m_SmoothPasses = 1;

        [Tooltip("マップ全体を回転させる角度（度）。スタート地点が下に来るように合わせたい時などに使う")]
        public float m_MapRotation = 0f;

        [Tooltip("このオブジェクトの枠の内側に取る余白（ピクセル）")]
        public Vector2 m_Padding = new Vector2(16f, 16f);

        [Tooltip("背景パネルの色。アルファ0なら背景は出さない")]
        public Color m_PanelColor = new Color(0f, 0f, 0f, 0f);


        // =====================================================
        // ■ アイコン
        // =====================================================

        [Header("アイコン")]

        [Tooltip("通常の車のアイコンの大きさ（ピクセル）")]
        public float m_IconSize = 22f;

        [Tooltip("自分の車のアイコンの大きさ（ピクセル）")]
        public float m_FocusIconSize = 30f;

        [Tooltip("通常の車のアイコンの縁の色")]
        public Color m_IconRingColor = Color.white;

        [Tooltip("自分の車のアイコンの縁の色")]
        public Color m_FocusRingColor = new Color(1f, 0.9f, 0.2f, 1f);

        [Tooltip("アイコンの外側に進行方向の三角を出す")]
        public bool m_ShowHeadingArrow = true;

        [Tooltip("車体からプレイヤーカラー（CarColorマテリアル）が取れなかった時に使う色。出現順に割り当てる")]
        public Color[] m_FallbackColors = new Color[]
        {
            new Color(1f,    0.25f, 0.25f),  // 赤
            new Color(0.3f,  0.55f, 1f),     // 青
            new Color(0.35f, 0.9f,  0.4f),   // 緑
            new Color(1f,    0.85f, 0.25f),  // 黄
        };

        [Tooltip("自分の車（強調表示する車）。空ならRaceCameraFollowが追っている車を自動で使う")]
        public Transform m_FocusTarget;

        [Tooltip("自分の車だけを、大きく・目立つ縁で強調する。" +
                 "OFFにすると、全員のアイコンを同じ大きさ・同じ縁にする" +
                 "（画面分割の複数人プレイでは、自分の車が人によって違うのでOFFにする）")]
        public bool m_HighlightFocus = true;

        [Tooltip("アイコンの中に、プレイヤー番号（1〜4）を出す。" +
                 "車が2台以上ある時だけ出る。番号は CarMovement の Player Number を使う")]
        public bool m_ShowPlayerNumber = false;

        [Tooltip("GoalTriggerの位置にスタート／ゴールの目印を出す")]
        public bool m_ShowStartMarker = true;

        [Tooltip("スタート／ゴールの目印の大きさ（ピクセル）")]
        public float m_StartMarkerSize = 18f;

        [Tooltip("ゴールの目印を、道路の幅の真ん中に寄せる。" +
                 "GoalTriggerの位置がコースの端や外側にズレていても、目印がコースの上に出る。" +
                 "（自動で作ったコース画像の時だけ働く）")]
        public bool m_SnapStartToRoad = true;

        [Tooltip("車の増減や自分の車の切り替わりを確認する間隔（秒）")]
        public float m_RefreshInterval = 0.5f;


        // =====================================================
        // ■ 内部：アイコン1つ分の情報
        // =====================================================

        /// <summary>
        /// ミニマップに出すアイコン1つ分（車1台ぶん）の情報。
        /// Root（位置を動かす親）の下に、Arrow（進行方向の三角）・Body（車の色の丸）・
        /// Ring（縁）の3つの画像が重なっている。
        /// </summary>
        private class Marker
        {
            public Transform Target;     // 追いかける対象
            public RectTransform Root;   // アイコンの親（位置を動かす）
            public Image Body;           // 車の色の丸
            public Image Ring;           // 縁取り
            public Image Arrow;          // 進行方向の三角
            public TextMeshProUGUI Label; // プレイヤー番号（番号が無い対象は null）
            public bool IsFocus;         // 自分の車として強調中か
        }


        // =====================================================
        // ■ 内部変数
        // =====================================================

        // いま表示しているアイコンの一覧（車1台につき1つ）
        private readonly List<Marker> m_Markers =
            new List<Marker>();

        // Colliderごとの「道路かどうか」の判定結果（毎回調べ直さないため）
        private readonly Dictionary<Collider, bool> m_ColliderVerdict =
            new Dictionary<Collider, bool>();

        // 除外したColliderの数（ログ用）。名前ごと／Excluded Roots
        private readonly Dictionary<string, int> m_ExcludedNameCounts =
            new Dictionary<string, int>();

        private int m_ExcludedRootCount;

        // 道路の内訳ログ用：Colliderが属するグループ（Course Root直下の子）
        private readonly Dictionary<Collider, int> m_ColliderGroup =
            new Dictionary<Collider, int>();

        // グループ名の一覧（添字がグループ番号。上の m_ColliderGroup が、この番号を持つ）
        private readonly List<string> m_GroupNames =
            new List<string>();

        // Ray結果の使い回し用バッファ
        // （RaycastNonAlloc に渡す。毎回新しい配列を作らないので、処理が軽い）
        private readonly RaycastHit[] m_HitBuffer =
            new RaycastHit[32];

        private RectTransform m_Content;       // コース画像とアイコンの親（回転する）
        private RawImage m_CourseImage;        // コース画像
        private RectTransform m_StartMarker;   // スタート／ゴールの目印
        private Vector3 m_StartWorldPos;       // ゴールの目印のワールド座標（道路の真ん中に寄せた後）

        // ゴールの目印を道路の真ん中に寄せるための、最終的な道路データ。
        // 自動でコース画像を作った時だけ入り、目印を作り終えたら捨てる
        // （手描きの画像には道路データが無いので、寄せずにそのまま置く）
        private byte[] m_SnapMask;
        private int m_SnapWidth;
        private int m_SnapHeight;
        private Vector2 m_SnapMin;   // 道路データの左下のワールド座標（XZ）
        private float m_SnapCell;    // 1ピクセルの大きさ（m）

        private Texture2D m_GeneratedTexture;  // 自動生成したコース画像
        private Coroutine m_BuildRoutine;     // コース検出のコルーチン（二重に始めないための記録）

        private bool m_Ready;  // コース画像ができて、表示できる状態か

        // コース画像が対応するワールド範囲（XZ）
        private Vector2 m_WorldMin;
        private Vector2 m_WorldSize = Vector2.one;

        // 画面上に表示するコース画像の大きさ（ピクセル）
        private Vector2 m_MapSize = Vector2.one;

        private Vector2 m_LastRectSize;   // 前回レイアウトした時の枠の大きさ（変わったか調べる用）
        private float m_NextRefreshTime;  // 次に車の増減を確認する時刻（Time.unscaledTime）


        // =====================================================
        // ■ 外部公開
        // =====================================================

        /// <summary>
        /// コース画像の準備ができて、ミニマップが表示されているか。
        /// </summary>
        public bool IsReady => m_Ready;


        // =====================================================
        // ■ Reset（コンポーネントを追加した時の初期配置）
        // =====================================================
        //
        // 画面右下、順位表示（RankUI）の少し上に出る位置にしておく
        //
        // =====================================================

        /// <summary>
        /// コンポーネントを追加した時（や、Inspectorで Reset を選んだ時）に、Unity が呼ぶ。
        /// 初期の位置と大きさを決める。
        /// </summary>
        private void Reset()
        {
            // UIの位置・大きさを決める RectTransform を取り出す（UIでなければ null）
            RectTransform rect = transform as RectTransform;

            if (rect == null)
            {
                return;
            }

            // 基準（anchor・pivot）を右下の角にして、右端から40、下端から250ピクセルの位置に、
            // 360 × 360 ピクセルの大きさで置く
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 250f);
            rect.sizeDelta = new Vector2(360f, 360f);
        }


        // =====================================================
        // ■ Awake / OnEnable / OnDisable / OnDestroy
        // =====================================================

        /// <summary>
        /// 最初に1回だけ呼ばれる。表示用のUIの骨組みを作る。
        /// </summary>
        private void Awake()
        {
            BuildSkeleton();
        }

        /// <summary>
        /// 有効になった時に呼ばれる。
        /// まだコース画像が無く、検出も始めていなければ、検出を始める。
        /// </summary>
        private void OnEnable()
        {
            if (!m_Ready && m_BuildRoutine == null)
            {
                m_BuildRoutine = StartCoroutine(BuildCourseRoutine());
            }
        }

        /// <summary>
        /// 無効になった時に呼ばれる。
        /// </summary>
        private void OnDisable()
        {
            // コルーチンはOFFにすると止まるので、
            // 次にONになった時に最初からやり直せるようにしておく
            m_BuildRoutine = null;
        }

        /// <summary>
        /// 削除された時に呼ばれる。自動で作ったコース画像を片付ける。
        /// </summary>
        private void OnDestroy()
        {
            // 自動で作ったテクスチャは、自分で Destroy しないと、メモリに残り続ける
            if (m_GeneratedTexture != null)
            {
                Destroy(m_GeneratedTexture);
            }
        }


        // =====================================================
        // ■ LateUpdate
        // =====================================================
        //
        // 車はUpdate/FixedUpdateで動くので、その後に
        // アイコンの位置を合わせる
        //
        // =====================================================

        /// <summary>
        /// 毎フレーム、すべての Update が終わった後に呼ばれる。
        /// 枠の大きさの変化を確認し、車のアイコンを更新する。
        /// </summary>
        private void LateUpdate()
        {
            // コース画像がまだできていなければ、何もしない
            if (!m_Ready)
            {
                return;
            }

            // 枠の大きさが変わったらレイアウトを組み直す
            RectTransform self = (RectTransform)transform;

            if ((self.rect.size - m_LastRectSize).sqrMagnitude > 0.01f)
            {
                RefreshLayout();
            }

            // 車の増減・自分の車の切り替わりを定期的に確認
            if (Time.unscaledTime >= m_NextRefreshTime)
            {
                // 次の確認時刻を決める（確認の間隔は、最低でも0.1秒）。
                // Time.unscaledTime ＝ ポーズ中（timeScale が 0 の時）も進む時間
                m_NextRefreshTime =
                    Time.unscaledTime +
                    Mathf.Max(0.1f, m_RefreshInterval);

                SyncMarkers();
            }

            // アイコンの位置と向きは、毎フレーム車に合わせる
            UpdateMarkers();
        }


        // =====================================================
        // ■ 外部から使えるAPI
        // =====================================================

        /// <summary>
        /// コース形状を最初から検出し直す（コースを差し替えた時など）。
        /// </summary>
        public void Rebuild()
        {
            // 検出の途中なら、止める
            if (m_BuildRoutine != null)
            {
                StopCoroutine(m_BuildRoutine);
                m_BuildRoutine = null;
            }

            // 「準備完了」の印を戻し、前回の判定結果（キャッシュ）も捨てる
            m_Ready = false;
            m_ColliderVerdict.Clear();

            // まだAwakeが呼ばれていない（骨組みが無い）場合は、
            // OnEnableでの最初の検出に任せる
            if (m_Content == null)
            {
                return;
            }

            m_Content.gameObject.SetActive(false);

            // 非アクティブな間はコルーチンを始められないので、
            // その場合も次のOnEnableで検出する
            if (isActiveAndEnabled)
            {
                m_BuildRoutine = StartCoroutine(BuildCourseRoutine());
            }
        }

        /// <summary>
        /// 自分の車（強調表示する車）を指定する。
        /// </summary>
        /// <param name="target">自分の車の Transform</param>
        public void SetFocus(Transform target)
        {
            m_FocusTarget = target;

            // 次のLateUpdateですぐ反映させる
            m_NextRefreshTime = 0f;
        }

        /// <summary>
        /// 車のアイコンを全部作り直す。
        /// 車の色を変えた直後や、アイコンの設定（強調する／プレイヤー番号を出す）を
        /// 切り替えた直後に呼ぶ。色は、アイコンを作る時にしか読まないため。
        /// </summary>
        public void RefreshIcons()
        {
            // 今あるアイコンを全部消す
            for (int i = m_Markers.Count - 1; i >= 0; i--)
            {
                if (m_Markers[i].Root != null)
                {
                    Destroy(m_Markers[i].Root.gameObject);
                }
            }

            m_Markers.Clear();

            // 次のLateUpdateで作り直す
            m_NextRefreshTime = 0f;

            // コース画像ができている時は、すぐ作り直す
            // （まだの時は、コース画像ができた時に作られる）
            if (m_Ready)
            {
                SyncMarkers();
                UpdateMarkers();
            }
        }

        /// <summary>
        /// CarMovement以外のもの（CPUの車など）にもアイコンを出したい時に使う。
        /// </summary>
        /// <param name="target">アイコンで追いかける対象</param>
        /// <param name="color">アイコンの色</param>
        public void RegisterTarget(Transform target, Color color)
        {
            // コース画像ができる前や、対象が無い時は、何もしない
            if (!m_Ready || target == null)
            {
                return;
            }

            // すでにアイコンがある対象には、重ねて作らない
            if (FindMarker(target) != null)
            {
                return;
            }

            AddMarker(target, color);
        }


        // =====================================================
        // ■ UIの骨組みを作る
        // =====================================================

        /// <summary>
        /// ミニマップのUIの骨組みを作る。
        /// このオブジェクトの子として、次のものを作る。
        ///   MinimapPanel   … 背景（Panel Color のアルファが0なら作らない）
        ///   MinimapContent … コース画像とアイコンの親（マップの回転はここに掛ける）
        ///     └ CourseImage … コース画像（RawImage）
        /// 車のアイコンとゴールの目印は、あとで MinimapContent の下に作られる。
        /// </summary>
        private void BuildSkeleton()
        {
            // 背景パネル（アルファが0なら作らない）
            if (m_PanelColor.a > 0.001f)
            {
                Image panel =
                    CreateImage(
                        "MinimapPanel",
                        transform,
                        null,
                        m_PanelColor
                    );

                Stretch(panel.rectTransform);
            }

            // コース画像とアイコンの親（マップ回転はここに掛ける）
            m_Content = CreateRect("MinimapContent", transform);

            // コース画像
            RectTransform courseRect =
                CreateRect("CourseImage", m_Content);

            Stretch(courseRect);

            // RawImage ＝ Texture2D をそのまま表示できるUI部品（Spriteにしなくてよい）
            m_CourseImage =
                courseRect.gameObject.AddComponent<RawImage>();

            m_CourseImage.raycastTarget = false;  // クリックを受け付けない（下のUIの操作を邪魔しない）

            // コース画像ができるまでは非表示
            m_Content.gameObject.SetActive(false);
        }


        // =====================================================
        // ■ コース形状の検出
        // =====================================================

        /// <summary>
        /// コース形状を検出して、ミニマップのコース画像を作る（コルーチン）。
        /// Rayの数が多いので、数フレームに分けて処理する。
        /// 流れ：道路の範囲を求める → 格子状にRayを撃って道路を調べる → 余計なものを消す
        /// → 画像を切り詰める → 画像にする → 表示を始める
        /// </summary>
        /// <returns>コルーチン（StartCoroutine で動かす）</returns>
        private IEnumerator BuildCourseRoutine()
        {
            // Canvasの初期化・Physicsへの登録が済むのを1フレーム待つ
            yield return null;

            // Colliderの位置の変更を、物理演算に反映させる
            // （これをしないと、動かしたばかりのColliderに、Rayが当たらないことがある）
            Physics.SyncTransforms();


            // 手描きの画像が指定されていればそれを使う
            if (m_CustomMapTexture != null)
            {
                ApplyCustomMap();

                yield break;
            }


            // 前回の判定結果を引きずらない
            m_ColliderVerdict.Clear();
            m_ColliderGroup.Clear();
            m_GroupNames.Clear();
            m_ExcludedNameCounts.Clear();
            m_ExcludedRootCount = 0;

            // Course Root が未設定なら、名前「TrackCourse」で探す
            ResolveCourseRoot();

            // 道路のColliderをすべて包む箱（範囲）を求める。
            // 道路が1つも見つからなければ、エラーを出して終わる
            Bounds bounds;

            if (!TryGetCourseBounds(out bounds))
            {
                Debug.LogError(
                    "RaceMinimap : コースの道路が見つかりませんでした。" +
                    "Course Root に道路のColliderを含むオブジェクト" +
                    "（例：TrackCourse）を設定してください。" +
                    "Excluded Tags / Included Layers / Included Roots / " +
                    "Max Collider Height で除外しすぎていないかも確認してください"
                );

                yield break;
            }


            // ---------------------------------------------
            // 調べる格子の大きさを決める
            // ---------------------------------------------

            // resolution ＝ コースの長い辺を、何ピクセルにするか（小さすぎる値は16にそろえる）
            int resolution = Mathf.Max(16, m_Resolution);

            // firstCell ＝ 余白を決めるための、仮の「1ピクセルの大きさ」（m）
            float firstCell =
                Mathf.Max(bounds.size.x, bounds.size.z) / resolution;

            // 縁取り・隙間埋めで画像の端が欠けないよう余白を取る
            float pad =
                Mathf.Max(
                    m_WorldPadding,
                    (m_OutlinePixels + m_GapClosePixels + 2) * firstCell
                );

            // 調べる範囲（コース＋余白）の左下の角（minX, minZ）と、
            // 大きさ（sizeX, sizeZ）。単位はメートル
            float minX = bounds.min.x - pad;
            float minZ = bounds.min.z - pad;
            float sizeX = bounds.size.x + pad * 2f;
            float sizeZ = bounds.size.z + pad * 2f;

            // cell ＝ 1ピクセルが表す長さ（m）。長い辺が resolution ピクセルになるように決める
            float cell = Mathf.Max(sizeX, sizeZ) / resolution;

            // 極端に小さな値にならないよう、最低でも 0.01m（1cm）にする
            if (cell < 0.01f)
            {
                cell = 0.01f;
            }

            // 画像の大きさ（ピクセル）。小さくなりすぎないよう、最低でも8
            int w = Mathf.Max(8, Mathf.CeilToInt(sizeX / cell));
            int h = Mathf.Max(8, Mathf.CeilToInt(sizeZ / cell));

            // Rayを撃ち始める高さ（コースの最高点より、余白ぶん上）と、
            // Rayの長さ（最低点より余白ぶん下まで届く長さ）
            float topY = bounds.max.y + m_RayHeightMargin;
            float rayLength = bounds.size.y + m_RayHeightMargin * 2f;


            // ---------------------------------------------
            // 真上からRayを撃って、道路の場所を調べる
            // ---------------------------------------------

            // mask ＝ 道路マスク（0 ＝ 道路でない、1 ＝ 道路。添字 ＝ y * w + x）
            byte[] mask = new byte[w * h];

            // その道路がどのグループ（Course Root直下の子）に属するか
            // （0＝なし、1以上＝グループ番号+1。内訳ログ用）
            int[] owner = new int[w * h];

            // found ＝ 道路が見つかったピクセルの数
            // raysThisFrame ＝ このフレームで撃ったRayの数
            // raysPerFrame ＝ 1フレームに撃つ数の上限（最低100）
            int found = 0;
            int raysThisFrame = 0;
            int raysPerFrame = Mathf.Max(100, m_RaysPerFrame);

            // ピクセルを1つずつ順に調べる。
            // 各ピクセルの中心（worldX, worldZ）の真上から、真下へRayを撃つ
            for (int y = 0; y < h; y++)
            {
                float worldZ = minZ + (y + 0.5f) * cell;

                for (int x = 0; x < w; x++)
                {
                    float worldX = minX + (x + 0.5f) * cell;

                    Vector3 origin = new Vector3(worldX, topY, worldZ);

                    // RaycastNonAlloc ＝ 当たったものを、用意済みの配列（m_HitBuffer）に入れる
                    // Ignore ＝ トリガー（チェックポイントなど）は無視する
                    int hitCount =
                        Physics.RaycastNonAlloc(
                            origin,
                            Vector3.down,
                            m_HitBuffer,
                            rayLength,
                            m_IncludedLayers.value,
                            QueryTriggerInteraction.Ignore
                        );

                    // 当たった中に道路が1つでもあれば道路とする
                    // （上に木や看板があっても道路を見逃さないため全部見る）。
                    // 複数あれば一番低い（地面側の）ものを、その場所の道路として記録する
                    Collider road = null;
                    float roadDistance = -1f;

                    for (int i = 0; i < hitCount; i++)
                    {
                        RaycastHit hit = m_HitBuffer[i];

                        if (hit.collider == null)
                        {
                            continue;
                        }

                        // ほぼ水平な面（上から見た道路の表面）だけを使う。壁の側面などは除く
                        if (hit.normal.y < m_MinNormalY)
                        {
                            continue;
                        }

                        // 道路として扱わないもの（トリガー・車・除外した名前など）は飛ばす
                        if (!IsRoadCollider(hit.collider))
                        {
                            continue;
                        }

                        // Rayの出発点から遠い ＝ 低い。一番低いものを、その場所の道路にする
                        if (hit.distance > roadDistance)
                        {
                            roadDistance = hit.distance;
                            road = hit.collider;
                        }
                    }

                    // 道路が見つかった場所は 1 にして、どのグループの道路かも記録する
                    if (road != null)
                    {
                        mask[y * w + x] = 1;
                        owner[y * w + x] = GetGroupId(road) + 1;
                        found++;
                    }

                    // 数フレームに分けて処理する（一瞬止まるのを防ぐ）
                    raysThisFrame++;

                    if (raysThisFrame >= raysPerFrame)
                    {
                        raysThisFrame = 0;

                        yield return null;  // 次のフレームまで待つ
                    }
                }
            }

            // 道路に1つも当たらなかったら、原因のヒントをエラーに出して終わる
            if (found == 0)
            {
                Debug.LogError(
                    "RaceMinimap : Rayが道路に1つも当たりませんでした。" +
                    "Course Root の下に道路のCollider（Is Triggerではないもの）が" +
                    "あるか、Excluded Tags に道路のタグが入っていないか確認してください"
                );

                yield break;
            }


            // ---------------------------------------------
            // 仕上げ（隙間埋め → 平滑化 → 細いものを消す →
            // 目印につながっている道路だけ残す）
            // ---------------------------------------------

            // mask を直接書き換えて仕上げる
            // （minX, minZ, cell は、目印の位置をピクセル座標に直すのに使う）
            FinishCourse(mask, owner, w, h, minX, minZ, cell);

            int roadPixels = MinimapCourseBuilder.CountPixels(mask);

            // 仕上げで全部消えてしまった場合は、設定のヒントをエラーに出して終わる
            if (roadPixels == 0)
            {
                Debug.LogError(
                    "RaceMinimap : 余計なものを消した結果、道路が1ピクセルも残りませんでした。" +
                    "Remove Thin Pixels を小さくするか、" +
                    "Keep Connected To Checkpoints をOFFにして確認してください"
                );

                yield break;
            }


            // ---------------------------------------------
            // 残った道路の形に合わせて画像を切り詰める
            // （遠くの障害物を消した後に、空白だらけにならないように）
            // ---------------------------------------------

            if (m_FitToCourse)
            {
                // 切り詰めた範囲の、左下の位置と大きさ（ピクセル）を受け取る変数
                int cropX;
                int cropY;
                int cropW;
                int cropH;

                // 余白は、縁取りの太さ＋2ピクセル（縁取りが、画像の端で欠けないようにする）
                byte[] cropped =
                    MinimapCourseBuilder.CropToRoad(
                        mask,
                        w,
                        h,
                        m_OutlinePixels + 2,
                        out cropX,
                        out cropY,
                        out cropW,
                        out cropH
                    );

                // 切り詰められたら、mask を置き換える。
                // 画像の左下のワールド座標（minX, minZ）と大きさ（w, h）も、
                // 切り詰めた分だけ更新する
                if (cropped != null)
                {
                    mask = cropped;

                    minX += cropX * cell;
                    minZ += cropY * cell;

                    w = cropW;
                    h = cropH;
                }
            }


            // ゴールの目印を道路の真ん中に寄せるため、最終的な道路データを渡しておく
            // （ApplyTexture の中で目印を作る時に使い、すぐ捨てる）
            m_SnapMask = mask;
            m_SnapWidth = w;
            m_SnapHeight = h;
            m_SnapMin = new Vector2(minX, minZ);
            m_SnapCell = cell;


            // 前回作ったコース画像があれば、先に破棄する（作り直しても、メモリが増えないように）
            if (m_GeneratedTexture != null)
            {
                Destroy(m_GeneratedTexture);
            }

            // 道路マスクから、コース画像（塗り＋縁取り）を作る
            m_GeneratedTexture =
                MinimapCourseBuilder.BuildTexture(
                    mask,
                    w,
                    h,
                    m_FillColor,
                    m_OutlineColor,
                    m_OutlinePixels
                );

            // 画像を表示に使い、対応するワールド範囲（左下の座標と、幅・高さ）も教える
            ApplyTexture(
                m_GeneratedTexture,
                new Vector2(minX, minZ),
                new Vector2(w * cell, h * cell)
            );

            // 検出の結果（画像の大きさ・1ピクセルの大きさ・
            // 道路のピクセル数・範囲）を、Consoleに出す
            Debug.Log(
                "RaceMinimap : コース検出完了 → 画像 " + w + "x" + h +
                " / 1ピクセル = " + cell.ToString("F2") + "m" +
                " / 道路 " + roadPixels + "ピクセル" +
                " / 範囲 X " + minX.ToString("F0") + "〜" + (minX + w * cell).ToString("F0") +
                ", Z " + minZ.ToString("F0") + "〜" + (minZ + h * cell).ToString("F0")
            );
        }


        /// <summary>
        /// 手描きのマップ画像（Custom Map Texture）を、コース画像として使う。
        /// 自動検出はしない。画像の左下と右上に対応するワールド座標（Custom World Min / Max）から、
        /// 画像が表すワールドの範囲を決める。
        /// </summary>
        private void ApplyCustomMap()
        {
            // 左下・右上の指定が逆になっていても動くように、
            // X・Zそれぞれ小さい方を min、大きい方を max にする
            Vector2 min = Vector2.Min(m_CustomWorldMin, m_CustomWorldMax);
            Vector2 max = Vector2.Max(m_CustomWorldMin, m_CustomWorldMax);
            Vector2 size = max - min;

            // 範囲の幅か奥行きがほぼ0なら、設定ミスなので、エラーを出して終わる
            if (size.x <= 0.001f || size.y <= 0.001f)
            {
                Debug.LogError(
                    "RaceMinimap : Custom World Min / Max が同じ値です。" +
                    "画像の左下と右上に対応するワールド座標（X, Z）を設定してください"
                );

                return;
            }

            // 画像と、画像が表すワールドの範囲（左下の座標と、幅・奥行き）を渡して、表示を始める
            ApplyTexture(m_CustomMapTexture, min, size);
        }


        /// <summary>
        /// コース画像が決まった後の共通の処理。画像を表示に使い、ゴールの目印と車のアイコンを置く。
        /// （自動検出でも手描きの画像でも、最後にここを通る）
        /// </summary>
        /// <param name="texture">表示するコース画像</param>
        /// <param name="worldMin">画像の左下に対応するワールド座標（X, Z）</param>
        /// <param name="worldSize">画像が表すワールドの大きさ（Xの幅, Zの奥行き。メートル）</param>
        private void ApplyTexture(
            Texture texture,
            Vector2 worldMin,
            Vector2 worldSize)
        {
            // 画像を表示に使い、画像が表すワールドの範囲を覚える
            // （ワールド座標をミニマップ上の座標に直す WorldToMap で使う）
            m_CourseImage.texture = texture;

            m_WorldMin = worldMin;
            m_WorldSize = worldSize;

            // ミニマップを表示して、「準備完了」の印を付ける
            m_Content.gameObject.SetActive(true);
            m_Ready = true;

            // ゴールの目印を作る
            CreateStartMarker();

            // 道路データは目印の位置決めにしか使わない
            m_SnapMask = null;

            // コース画像の配置 → 車のアイコンの作成 → アイコンの位置合わせ、の順に行う
            RefreshLayout();
            SyncMarkers();
            UpdateMarkers();
        }


        // =====================================================
        // ■ 検出した道路の仕上げ
        // =====================================================
        //
        // 1. 隙間埋め・平滑化（継ぎ目の隙間や点ノイズを消す）
        // 2. 細いもの（柵・ポール・看板など）を消す
        // 3. 目印（チェックポイント・ゴール・車）につながっている
        // 　 道路だけを残す（コースから離れた障害物を消す）
        //
        // 消えた量と道路の内訳は、Consoleにログで出す。
        //
        // =====================================================

        // 目印が道路から少しズレていても拾う範囲（ピクセル）
        private const int SeedSearchRadius = 4;

        // 内訳ログに出すグループ数の上限
        private const int MaxBreakdownLines = 20;

        /// <summary>
        /// 検出した道路マスクを仕上げる。
        /// 隙間埋め → 平滑化 → 細いものを消す → 目印につながっていない道路を消す、の順に行い、
        /// 消えた量をConsoleにログで出す。
        /// </summary>
        /// <param name="mask">道路マスク（0＝道路でない、1＝道路）。直接書き換える</param>
        /// <param name="owner">各ピクセルの道路のグループ番号＋1（0＝なし）。内訳ログ用</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="minX">画像の左端のワールドX座標</param>
        /// <param name="minZ">画像の下端のワールドZ座標</param>
        /// <param name="cell">1ピクセルが表す長さ（m）</param>
        private void FinishCourse(
            byte[] mask,
            int[] owner,
            int w,
            int h,
            float minX,
            float minZ,
            float cell)
        {
            // 仕上げる前に、グループごとの道路ピクセル数を数えておく
            int[] detected = new int[m_GroupNames.Count];

            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] > 0)
                {
                    detected[owner[i] - 1]++;
                }
            }

            // 仕上げる前の、道路ピクセルの合計（ログに出す用）
            int detectedTotal = MinimapCourseBuilder.CountPixels(mask);


            // 1. 隙間埋め・平滑化
            MinimapCourseBuilder.Close(mask, w, h, m_GapClosePixels);
            MinimapCourseBuilder.Smooth(mask, w, h, m_SmoothPasses);


            // 2. 細いもの（柵・ポールなど）を消す
            int beforeThin = MinimapCourseBuilder.CountPixels(mask);  // 消す前の数

            MinimapCourseBuilder.Open(mask, w, h, m_RemoveThinPixels);

            int thinRemoved =
                beforeThin - MinimapCourseBuilder.CountPixels(mask);


            // 3. 目印につながっていない道路を消す
            int seedCount = 0;           // 集めた目印の数
            int validSeedCount = 0;      // そのうち、道路の上にあった数
            int unconnectedRemoved = 0;  // つながっていなくて消えた道路のピクセル数

            if (m_KeepConnectedToCheckpoints)
            {
                // 目印（チェックポイント・ゴール・車）の位置を、画像のピクセル座標で集める
                List<Vector2Int> seeds = CollectSeeds(minX, minZ, cell, w, h);

                seedCount = seeds.Count;

                int beforeConnect = MinimapCourseBuilder.CountPixels(mask);  // 消す前の数

                // 目印につながっている道路だけを残す。戻り値は、道路の上にあった目印の数。
                // 4つ目の値 ＝ 隙間をつなぐために、道路を太らせる半径。
                //   Connect Gap Pixels（つなぐ隙間の幅）の半分を、切り上げた値にする
                //   （両側から太らせるので、半径の2倍の隙間まで、つながる）
                // 5つ目の値 ＝ 目印が道路から少しズレていた時に、道路を探す範囲（ピクセル）
                validSeedCount =
                    MinimapCourseBuilder.KeepConnectedToSeeds(
                        mask,
                        w,
                        h,
                        seeds,
                        (m_ConnectGapPixels + 1) / 2,
                        SeedSearchRadius
                    );

                unconnectedRemoved =
                    beforeConnect - MinimapCourseBuilder.CountPixels(mask);
            }


            // ---------------------------------------------
            // ここからはログの出力だけ
            // ---------------------------------------------

            StringBuilder log = new StringBuilder();

            log.Append(
                "RaceMinimap : 余計なものを消しました" +
                "（検出 " + detectedTotal + " → 残り " +
                MinimapCourseBuilder.CountPixels(mask) + " ピクセル）"
            );

            AppendExcludedSummary(log);

            // 1ピクセルが何mかも添えて、設定の強さをイメージしやすくする
            string thinLabel =
                m_RemoveThinPixels > 0
                    ? "幅 約" + (m_RemoveThinPixels * 2 * cell).ToString("F1") + "m 以下"
                    : "無効";

            log.Append(
                "\n  ・細いもの（Remove Thin Pixels = " + m_RemoveThinPixels +
                " → " + thinLabel + "）: " +
                thinRemoved + " ピクセル"
            );

            if (m_KeepConnectedToCheckpoints)
            {
                // gapMeters ＝ つながる隙間の最大の幅（m）。太らせる半径 × 2 × 1ピクセルの長さ
                float gapMeters = ((m_ConnectGapPixels + 1) / 2) * 2 * cell;

                log.Append(
                    "\n  ・目印につながっていない道路" +
                    "（Connect Gap Pixels = " + m_ConnectGapPixels +
                    " → 隙間 約" + gapMeters.ToString("F1") + "m 以下はつながる）: " +
                    unconnectedRemoved + " ピクセル" +
                    "（目印 " + seedCount + "個のうち " +
                    validSeedCount + "個が道路の上）"
                );

                if (validSeedCount == 0)
                {
                    log.Append(
                        "\n  ※ 道路の上に目印が1つも無かったため、" +
                        "つながりによる除去は行っていません。" +
                        "CheckPoint / GoalTrigger / 車 が道路の上にあるか確認してください"
                    );
                }
            }
            else
            {
                log.Append(
                    "\n  ・目印につながっていない道路: 消さない設定です" +
                    "（Keep Connected To Checkpoints がOFF）"
                );
            }

            if (m_LogBreakdown)
            {
                AppendBreakdown(log, mask, owner, detected);
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 検出の前に除外したColliderの数を、ログに書き足す。
        /// Excluded Names の項目ごとの数と、Excluded Roots での数を出す。
        /// </summary>
        /// <param name="log">書き足す先のログ</param>
        private void AppendExcludedSummary(StringBuilder log)
        {
            if (m_ExcludedNames != null)
            {
                for (int i = 0; i < m_ExcludedNames.Length; i++)
                {
                    string excluded = m_ExcludedNames[i];

                    if (string.IsNullOrEmpty(excluded))
                    {
                        continue;
                    }

                    // この名前で除外した数を取り出す（1つも記録が無ければ 0）
                    int count;

                    m_ExcludedNameCounts.TryGetValue(excluded, out count);

                    log.Append(
                        "\n  ・名前に「" + excluded + "」を含むものを除外: Collider " +
                        count + "個" +
                        (count == 0 ? "（該当するオブジェクトが見つかりません）" : "")
                    );
                }
            }

            if (m_ExcludedRootCount > 0)
            {
                log.Append(
                    "\n  ・Excluded Roots で除外: Collider " +
                    m_ExcludedRootCount + "個"
                );
            }
        }

        /// <summary>
        /// 道路の内訳（グループごとの、残ったピクセル数 / 検出したピクセル数）を、ログに書き足す。
        /// 余計なものが、どのオブジェクトから来ているかを調べるためのもの。
        /// </summary>
        /// <param name="log">書き足す先のログ</param>
        /// <param name="mask">仕上げ後の道路マスク</param>
        /// <param name="owner">各ピクセルの道路のグループ番号＋1（0＝なし）</param>
        /// <param name="detected">グループごとの、検出したピクセル数（添字＝グループ番号）</param>
        private void AppendBreakdown(
            StringBuilder log,
            byte[] mask,
            int[] owner,
            int[] detected)
        {
            // kept ＝ グループごとの、仕上げ後に残ったピクセル数
            // keptExtra ＝ どのグループにも属さないピクセル数
            //   （隙間埋めや平滑化で、あとから道路になった所）
            int[] kept = new int[detected.Length];
            int keptExtra = 0;

            // 残っている道路のピクセルを、グループごとに数える
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] == 0)
                {
                    continue;
                }

                if (owner[i] > 0)
                {
                    kept[owner[i] - 1]++;
                }
                else
                {
                    keptExtra++;
                }
            }

            // 検出したピクセルが多い順に並べる
            List<int> order = new List<int>();

            for (int groupIndex = 0; groupIndex < detected.Length; groupIndex++)
            {
                order.Add(groupIndex);
            }

            // b と a を逆にして比べるので、大きい順（降順）になる
            order.Sort((a, b) => detected[b].CompareTo(detected[a]));

            log.Append("\n道路の内訳（残ったピクセル / 検出したピクセル）");

            // 出す行数は、多すぎないように MaxBreakdownLines までにする
            int lines = Mathf.Min(order.Count, MaxBreakdownLines);

            for (int n = 0; n < lines; n++)
            {
                int id = order[n];

                log.Append(
                    "\n  " + m_GroupNames[id] + " : " +
                    kept[id] + " / " + detected[id]
                );

                // 検出したのに、1ピクセルも残らなかったグループには、印を付ける
                if (detected[id] > 0 && kept[id] == 0)
                {
                    log.Append("  ← 全部消えました");
                }
            }

            // 出しきれなかったグループがあれば、その数を知らせる
            if (order.Count > lines)
            {
                log.Append("\n  …ほか " + (order.Count - lines) + " グループ");
            }

            if (keptExtra > 0)
            {
                log.Append(
                    "\n  （隙間埋め・丸めで増えた分） : " + keptExtra
                );
            }
        }

        /// <summary>
        /// 道路の上にあるはずの場所（チェックポイント・ゴール・車）を、
        /// コース画像上のピクセル座標で集める。
        /// ここから道路をたどって、つながっていない道路を消すのに使う。
        /// </summary>
        /// <param name="minX">画像の左端のワールドX座標</param>
        /// <param name="minZ">画像の下端のワールドZ座標</param>
        /// <param name="cell">1ピクセルが表す長さ（m）</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <returns>目印のピクセル座標の一覧（画像の外にあるものは入らない）</returns>
        private List<Vector2Int> CollectSeeds(
            float minX,
            float minZ,
            float cell,
            int w,
            int h)
        {
            List<Vector2Int> seeds = new List<Vector2Int>();

            // シーン内の有効（Active）なチェックポイントを探して、その中心を目印にする。
            // FindObjectsSortMode.None ＝ 並び順は問わない（その分、速い）
            CheckPoint[] checkPoints =
                FindObjectsByType<CheckPoint>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < checkPoints.Length; i++)
            {
                AddSeed(seeds, CenterOf(checkPoints[i]), minX, minZ, cell, w, h);
            }

            // ゴールの中心も、目印にする
            GoalTrigger[] goals =
                FindObjectsByType<GoalTrigger>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < goals.Length; i++)
            {
                AddSeed(seeds, CenterOf(goals[i]), minX, minZ, cell, w, h);
            }

            // 車の位置も、目印にする（車は道路の上にいるはず）
            CarMovement[] cars =
                FindObjectsByType<CarMovement>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < cars.Length; i++)
            {
                AddSeed(seeds, cars[i].transform.position, minX, minZ, cell, w, h);
            }

            return seeds;
        }

        /// <summary>
        /// トリガー（ゴール・チェックポイント）の、中心のワールド座標を求める。
        /// 有効なColliderの範囲の中心を使い、Colliderが1つも無ければ、オブジェクトの位置を使う。
        /// どのColliderを優先するかは、メソッド内のコメントを参照。
        /// </summary>
        /// <param name="component">中心を知りたい GoalTrigger や CheckPoint など</param>
        /// <returns>中心のワールド座標</returns>
        private static Vector3 CenterOf(Component component)
        {
            // 自分自身と子の、Colliderをすべて集める
            // （GetComponentsInChildren は自分自身も含む。false ＝ 非アクティブなものは除く）
            Collider[] colliders = component.GetComponentsInChildren<Collider>(false);

            // 次の4段階を順に調べて、最初にColliderが見つかった段階の中心を返す。
            //   pass 0 … 自分自身に付いた、トリガーのCollider
            //   pass 1 … 自分自身に付いた、トリガーでないCollider
            //   pass 2 … 子に付いた、トリガーのCollider
            //   pass 3 … 子に付いた、トリガーでないCollider
            // Colliderが子にあるのに、オブジェクトの位置（モデルの原点など）を使ってしまうと、
            // 目印が道路から大きくズレるため、Colliderの中心を優先する
            for (int pass = 0; pass < 4; pass++)
            {
                bool wantOwn = pass < 2;             // pass 0・1 は自分自身、2・3 は子
                bool wantTrigger = (pass % 2) == 0;  // pass 0・2 はトリガー、1・3 はそれ以外

                bool found = false;           // この段階で、Colliderが見つかったか
                Bounds union = new Bounds();  // 見つかったColliderをすべて包む範囲

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider c = colliders[i];

                    // 破棄されたColliderや、無効（enabled がOFF）のColliderは使わない
                    if (c == null || !c.enabled)
                    {
                        continue;
                    }

                    // このColliderは、自分自身に付いているか（false なら、子に付いている）
                    bool own = c.gameObject == component.gameObject;

                    // 今の段階で探している種類でなければ、飛ばす
                    if (own != wantOwn || c.isTrigger != wantTrigger)
                    {
                        continue;
                    }

                    // 最初の1つは、そのまま範囲にする。2つ目からは、範囲を広げて包む（Encapsulate）
                    if (!found)
                    {
                        union = c.bounds;
                        found = true;
                    }
                    else
                    {
                        union.Encapsulate(c.bounds);
                    }
                }

                // この段階で見つかったら、その中心を返す（次の段階は調べない）
                if (found)
                {
                    return union.center;
                }
            }

            // どの段階でも見つからなければ、オブジェクト自身の位置を使う
            return component.transform.position;
        }

        /// <summary>
        /// ワールド座標を、コース画像のピクセル座標に直して、目印の一覧に足す。
        /// 画像の外にある目印は、足さない。
        /// </summary>
        /// <param name="seeds">足す先の、目印（ピクセル座標）の一覧</param>
        /// <param name="worldPos">目印のワールド座標</param>
        /// <param name="minX">画像の左端のワールドX座標</param>
        /// <param name="minZ">画像の下端のワールドZ座標</param>
        /// <param name="cell">1ピクセルが表す長さ（m）</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        private static void AddSeed(
            List<Vector2Int> seeds,
            Vector3 worldPos,
            float minX,
            float minZ,
            float cell,
            int w,
            int h)
        {
            // 左下からの距離を、1ピクセルの長さで割って、小数を切り捨てる（ピクセルの番号になる）。
            // ワールドのXが画像の横（x）、ワールドのZが画像の縦（y）に当たる
            int x = Mathf.FloorToInt((worldPos.x - minX) / cell);
            int y = Mathf.FloorToInt((worldPos.z - minZ) / cell);

            // 画像の外にある目印は使わない
            if (x < 0 || x >= w || y < 0 || y >= h)
            {
                return;
            }

            seeds.Add(new Vector2Int(x, y));
        }


        // =====================================================
        // ■ 道路Colliderの判定
        // =====================================================

        /// <summary>
        /// Course Root が未設定なら、名前「TrackCourse」で探して設定する。
        /// 見つからなければ警告を出す（その場合は、シーン内のすべてのColliderが対象になる）。
        /// </summary>
        private void ResolveCourseRoot()
        {
            if (m_CourseRoot != null)
            {
                return;
            }

            GameObject found = GameObject.Find("TrackCourse");

            if (found != null)
            {
                m_CourseRoot = found.transform;
            }
            else
            {
                Debug.LogWarning(
                    "RaceMinimap : Course Root が未設定で、「TrackCourse」という" +
                    "名前のオブジェクトも見つかりません。シーン内の全Colliderを" +
                    "対象にします（地面まで含まれる場合は Course Root を設定してください）"
                );
            }
        }

        /// <summary>
        /// 道路のColliderをすべて包む範囲（箱）を求める。
        /// </summary>
        /// <param name="bounds">道路のColliderをすべて包む範囲。見つからなければ空の範囲</param>
        /// <returns>道路のColliderが1つでも見つかったら true</returns>
        private bool TryGetCourseBounds(out Bounds bounds)
        {
            bounds = new Bounds();

            bool hasBounds = false;

            // 調べるColliderを集める。Course Root があればその子孫だけ、
            // 無ければシーン全体から探す（非アクティブなものは除く）
            Collider[] colliders =
                m_CourseRoot != null
                    ? m_CourseRoot.GetComponentsInChildren<Collider>(false)
                    : FindObjectsByType<Collider>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];

                if (c == null || !c.enabled)
                {
                    continue;
                }

                if (!IsRoadCollider(c))
                {
                    continue;
                }

                // 最初の1つは、そのまま範囲にする。2つ目からは、範囲を広げて包む（Encapsulate）
                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        /// <summary>
        /// このColliderを道路として扱うか。
        /// 一度調べた結果は覚えておき（m_ColliderVerdict）、2回目からは、覚えた結果を返す。
        /// </summary>
        /// <param name="c">調べるCollider</param>
        /// <returns>道路なら true</returns>
        private bool IsRoadCollider(Collider c)
        {
            bool verdict;

            if (m_ColliderVerdict.TryGetValue(c, out verdict))
            {
                return verdict;
            }

            verdict = EvaluateCollider(c);

            m_ColliderVerdict[c] = verdict;

            return verdict;
        }

        /// <summary>
        /// このColliderを道路として扱うかを、条件を順に確認して決める。
        /// 1つでも当てはまれば、道路ではない（false）。除外した数は、ログ用に数えておく。
        /// </summary>
        /// <param name="c">調べるCollider</param>
        /// <returns>道路なら true</returns>
        private bool EvaluateCollider(Collider c)
        {
            // トリガー（チェックポイント・ゴール）は道路ではない
            if (c.isTrigger)
            {
                return false;
            }

            // Course Root の外は対象外
            if (m_CourseRoot != null &&
                !c.transform.IsChildOf(m_CourseRoot))
            {
                return false;
            }

            // Included Layers に入っていないレイヤーは、道路ではない
            // （1 << レイヤー番号 ＝ そのレイヤーを表すビット。値と重ならなければ、入っていない）
            if (((1 << c.gameObject.layer) & m_IncludedLayers.value) == 0)
            {
                return false;
            }

            // 車自身は道路ではない
            if (c.GetComponentInParent<CarMovement>() != null)
            {
                return false;
            }

            // タグが Excluded Tags のどれかと同じなら、道路ではない
            if (m_ExcludedTags != null)
            {
                string colliderTag = c.gameObject.tag;

                for (int i = 0; i < m_ExcludedTags.Length; i++)
                {
                    if (!string.IsNullOrEmpty(m_ExcludedTags[i]) &&
                        m_ExcludedTags[i] == colliderTag)
                    {
                        return false;
                    }
                }
            }

            // Excluded Roots のどれかの子孫なら、道路ではない（除外した数を数える）
            if (m_ExcludedRoots != null)
            {
                for (int i = 0; i < m_ExcludedRoots.Length; i++)
                {
                    if (m_ExcludedRoots[i] != null &&
                        c.transform.IsChildOf(m_ExcludedRoots[i]))
                    {
                        m_ExcludedRootCount++;

                        return false;
                    }
                }
            }

            // 除外する名前（Fenceなど）を含むオブジェクトの中身
            string excludedName = FindExcludedName(c.transform);

            if (excludedName != null)
            {
                int count;

                m_ExcludedNameCounts.TryGetValue(excludedName, out count);
                m_ExcludedNameCounts[excludedName] = count + 1;

                return false;
            }

            // 道路として扱うオブジェクトの指定があれば、その子孫だけが対象
            if (HasIncludedRoots() &&
                !IsUnderIncludedRoot(c.transform))
            {
                return false;
            }

            // 背の高いもの（障害物・建物・木など）は道路ではない
            if (m_MaxColliderHeight > 0f &&
                c.bounds.size.y > m_MaxColliderHeight)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Colliderから Course Root の手前まで親をたどり、除外する名前を含むオブジェクトがあれば、
        /// その名前（Excluded Names の項目）を返す。
        /// </summary>
        /// <param name="target">調べるColliderの Transform</param>
        /// <returns>見つかった除外名（Excluded Names の項目）。無ければ null</returns>
        private string FindExcludedName(Transform target)
        {
            if (m_ExcludedNames == null || m_ExcludedNames.Length == 0)
            {
                return null;
            }

            // Colliderから親へ、Course Root の手前までたどる（Course Root 自身は調べない）
            for (Transform t = target; t != null && t != m_CourseRoot; t = t.parent)
            {
                for (int i = 0; i < m_ExcludedNames.Length; i++)
                {
                    string excluded = m_ExcludedNames[i];

                    if (string.IsNullOrEmpty(excluded))
                    {
                        continue;
                    }

                    // 名前に除外名が含まれるか。OrdinalIgnoreCase ＝ 大文字・小文字は区別しない
                    if (t.name.IndexOf(
                            excluded,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return excluded;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Included Roots に、オブジェクトが1つでも入っているか。
        /// </summary>
        /// <returns>入っていれば true</returns>
        private bool HasIncludedRoots()
        {
            if (m_IncludedRoots == null)
            {
                return false;
            }

            for (int i = 0; i < m_IncludedRoots.Length; i++)
            {
                if (m_IncludedRoots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// target が、Included Roots のどれかの子孫（本人も含む）か。
        /// </summary>
        /// <param name="target">調べる Transform</param>
        /// <returns>どれかの子孫なら true</returns>
        private bool IsUnderIncludedRoot(Transform target)
        {
            if (m_IncludedRoots == null)
            {
                return false;
            }

            for (int i = 0; i < m_IncludedRoots.Length; i++)
            {
                if (m_IncludedRoots[i] != null &&
                    target.IsChildOf(m_IncludedRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }


        // =====================================================
        // ■ 道路の内訳（どのオブジェクトが道路になったか）
        // =====================================================
        //
        // Course Root 直下の子（例：Straights / Curves / Fence）
        // ごとにまとめて、Consoleのログに出す（Breakdown Depth を
        // 増やすと、その下の階層まで細かく分ける）。
        // 余計なものがどこから来ているかを調べるための仕組み。
        //
        // =====================================================

        /// <summary>
        /// Colliderが属するグループ（Course Root 直下の子など）の番号を返す。
        /// 初めて見る名前なら、新しく登録する。一度調べたColliderは、結果を覚えておく。
        /// </summary>
        /// <param name="c">調べるCollider</param>
        /// <returns>グループ番号（m_GroupNames の添字）</returns>
        private int GetGroupId(Collider c)
        {
            int id;

            // 調べ済みのColliderなら、覚えている番号を返す
            if (m_ColliderGroup.TryGetValue(c, out id))
            {
                return id;
            }

            // グループの名前から、番号を探す。無ければ、末尾に足して、新しい番号にする
            string groupName = GroupNameOf(c);

            id = m_GroupNames.IndexOf(groupName);

            if (id < 0)
            {
                id = m_GroupNames.Count;

                m_GroupNames.Add(groupName);
            }

            m_ColliderGroup[c] = id;

            return id;
        }

        /// <summary>
        /// Colliderが属するグループの名前を返す。
        /// Course Root 直下の子の名前を使う（Breakdown Depth が2以上なら、「親/子」のように
        /// 下の階層もつなげる）。Course Root が無い時は、一番上の親の名前。
        /// </summary>
        /// <param name="c">調べるCollider</param>
        /// <returns>グループの名前</returns>
        private string GroupNameOf(Collider c)
        {
            Transform t = c.transform;

            if (m_CourseRoot == null)
            {
                return t.root.name;
            }

            if (t == m_CourseRoot)
            {
                return m_CourseRoot.name + "（直接付いているCollider）";
            }

            // Collider側から Course Root の手前まで、名前を集める（子 → 親の順）
            List<string> names = new List<string>();

            while (t != null && t != m_CourseRoot)
            {
                names.Add(t.name);

                t = t.parent;
            }

            // Course Root 直下の子（末尾）から、指定の階層ぶんだけつなげる
            int depth = Mathf.Max(1, m_BreakdownDepth);

            StringBuilder path = new StringBuilder();

            for (int i = 0; i < depth && names.Count - 1 - i >= 0; i++)
            {
                if (i > 0)
                {
                    path.Append("/");
                }

                path.Append(names[names.Count - 1 - i]);
            }

            return path.ToString();
        }


        // =====================================================
        // ■ レイアウト
        // =====================================================
        //
        // コース画像の縦横比を保ったまま、このオブジェクトの
        // 枠（余白を引いた内側）にちょうど収まる大きさにする。
        // マップを回転させる場合は、回転後の外接矩形で収める。
        //
        // =====================================================

        /// <summary>
        /// 枠の大きさに合わせて、コース画像の大きさ・位置・向きを決め直す（詳しくは上の説明）。
        /// </summary>
        private void RefreshLayout()
        {
            RectTransform self = (RectTransform)transform;

            // いまの枠の大きさを覚える（枠の大きさが変わったかを、LateUpdate で調べるため）
            m_LastRectSize = self.rect.size;

            // box ＝ 枠から余白（左右・上下）を引いた、コース画像を置ける範囲
            Vector2 box = self.rect.size - m_Padding * 2f;

            // 枠が小さすぎても、0以下にならないように、最低でも1にする
            box.x = Mathf.Max(box.x, 1f);
            box.y = Mathf.Max(box.y, 1f);

            // aspect ＝ コース画像の縦横比（横 ÷ 縦）
            float aspect = m_WorldSize.x / Mathf.Max(m_WorldSize.y, 0.0001f);

            // マップの回転角（度 → ラジアン）。
            // 回転後の画像がぴったり入る四角形（外接矩形）の大きさは、
            //   横 ＝ 幅 × cos ＋ 高さ × sin
            //   縦 ＝ 幅 × sin ＋ 高さ × cos
            // になる（cos・sin は絶対値）
            float rad = m_MapRotation * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(rad));
            float sin = Mathf.Abs(Mathf.Sin(rad));

            // 幅 ＝ aspect × 高さ なので、外接矩形が box の横・縦にそれぞれ収まる、
            // 最大の高さを求める
            float heightByWidth = box.x / (aspect * cos + sin + 0.00001f);
            float heightByHeight = box.y / (aspect * sin + cos + 0.00001f);

            // 小さい方を使えば、横にも縦にも収まる
            float height = Mathf.Min(heightByWidth, heightByHeight);

            m_MapSize = new Vector2(aspect * height, height);

            // コース画像とアイコンの親（Content）の大きさを決めて、中央に置き、回転させる
            m_Content.sizeDelta = m_MapSize;
            m_Content.anchoredPosition = Vector2.zero;
            m_Content.localRotation = Quaternion.Euler(0f, 0f, m_MapRotation);

            // アイコンは、マップが回転しても、常に正立（上が上）にする。
            // マップの回転と逆向きに回して、打ち消す
            Quaternion upright = Quaternion.Euler(0f, 0f, -m_MapRotation);

            for (int i = 0; i < m_Markers.Count; i++)
            {
                if (m_Markers[i].Root != null)
                {
                    m_Markers[i].Root.localRotation = upright;
                }
            }

            // ゴールの目印も、向きを正立にして、位置を合わせ直す
            if (m_StartMarker != null)
            {
                m_StartMarker.localRotation = upright;
                m_StartMarker.anchoredPosition = WorldToMap(m_StartWorldPos);
            }
        }

        /// <summary>
        /// ワールド座標を、ミニマップ上の座標（Content の中心が原点。単位はピクセル）に直す。
        /// </summary>
        /// <param name="worldPos">ワールド座標（XとZを使う。Yは使わない）</param>
        /// <returns>Content の中での座標（アイコンの anchoredPosition に使う）</returns>
        private Vector2 WorldToMap(Vector3 worldPos)
        {
            // u ＝ 画像の左端を0、右端を1とした、横の位置
            // v ＝ 画像の下端を0、上端を1とした、縦の位置（ワールドのXが横、Zが縦）
            float u = (worldPos.x - m_WorldMin.x) / m_WorldSize.x;
            float v = (worldPos.z - m_WorldMin.y) / m_WorldSize.y;

            // 範囲外に出ても、アイコンが枠の端に残るようにする
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            // 0〜1 を、中心が原点になるよう -0.5〜0.5 にずらして、
            // マップの大きさ（ピクセル）を掛ける
            return new Vector2(
                (u - 0.5f) * m_MapSize.x,
                (v - 0.5f) * m_MapSize.y
            );
        }


        // =====================================================
        // ■ スタート／ゴールの目印
        // =====================================================

        /// <summary>
        /// ゴールの目印（市松模様）を作る。すでにあれば、作り直す。
        /// 目印を出さない設定の時、ゴールが無い時、道路から離れすぎて寄せられない時は、作らない。
        /// </summary>
        private void CreateStartMarker()
        {
            // 前回作った目印があれば、先に消す（作り直しても、目印が増えないように）
            if (m_StartMarker != null)
            {
                Destroy(m_StartMarker.gameObject);
                m_StartMarker = null;
            }

            // 目印を出さない設定（Show Start Marker がOFF）なら、ここで終わり
            if (!m_ShowStartMarker)
            {
                return;
            }

            // 目印にするゴールを選ぶ。シーンにゴールが無ければ、目印は作らない
            GoalTrigger goal = FindGoal();

            if (goal == null)
            {
                return;
            }

            // トリガーの中心（Colliderがあればその中心）を目印の位置にする
            m_StartWorldPos = CenterOf(goal);

            // トリガーがコースの端や外側にあっても、目印は道路の真ん中に出す。
            // コースから大きく離れていて寄せられない時は、目印を出さない
            if (m_SnapStartToRoad && !SnapStartToRoad(goal))
            {
                return;
            }

            // 市松模様の画像を作る（色は白のまま。画像自体が、白と黒っぽいマス目になっている）
            Image image =
                CreateImage(
                    "StartMarker",
                    m_Content,
                    MinimapSprites.Checker,
                    Color.white
                );

            m_StartMarker = image.rectTransform;

            m_StartMarker.sizeDelta =
                new Vector2(m_StartMarkerSize, m_StartMarkerSize);

            // コース画像のすぐ上、車のアイコンの下に描く
            // （子の並び順 ＝ 描画順。0番目はコース画像なので、その次の1番目にする）
            m_StartMarker.SetSiblingIndex(1);
        }


        /// <summary>
        /// シーン内のGoalTriggerから、目印にするものを1つ選ぶ。
        /// 複数ある時は、Course Root の下にあるものを優先する（無ければ先頭）。
        /// </summary>
        /// <returns>選んだGoalTrigger。1つも無ければ null</returns>
        private GoalTrigger FindGoal()
        {
            // 有効なGoalTriggerをすべて探す。
            // InstanceID順 ＝ 毎回同じ並びになるので、複数あっても、選ばれるものが変わらない
            GoalTrigger[] goals =
                FindObjectsByType<GoalTrigger>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.InstanceID
                );

            if (goals == null || goals.Length == 0)
            {
                return null;
            }

            // 先頭を仮の選択にして、Course Root の下にあるものがあれば、それに替える
            GoalTrigger chosen = goals[0];

            if (m_CourseRoot != null)
            {
                for (int i = 0; i < goals.Length; i++)
                {
                    if (IsUnder(goals[i].transform, m_CourseRoot))
                    {
                        chosen = goals[i];

                        break;
                    }
                }
            }

            // 複数あった時は、どれを選んだかを警告で知らせる
            // （意図しないゴールが使われていないか、確認できるように）
            if (goals.Length > 1)
            {
                Debug.LogWarning(
                    "RaceMinimap : GoalTriggerが " + goals.Length + " 個あります。" +
                    "「" + PathOf(chosen.transform) + "」をゴールの目印に使います"
                );
            }

            return chosen;
        }

        /// <summary>
        /// target が、root の子孫（または root 自身）か。
        /// </summary>
        /// <param name="target">調べる Transform</param>
        /// <param name="root">親の候補</param>
        /// <returns>子孫（または本人）なら true</returns>
        private static bool IsUnder(Transform target, Transform root)
        {
            Transform current = target;

            // 親を順にたどる（階層が深すぎても止まるように、最大64段まで）
            for (int i = 0; i < 64 && current != null; i++)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// ヒエラルキー上の名前を、「親/子/孫」の形で返す。
        /// 例：TrackCourse/Goal/Trigger（ログに出す用）
        /// </summary>
        /// <param name="target">名前を知りたい Transform</param>
        /// <returns>「/」でつないだ名前。target が null なら空の文字列</returns>
        private static string PathOf(Transform target)
        {
            if (target == null)
            {
                return "";
            }

            string path = target.name;
            Transform current = target.parent;

            // 親を順にたどって、名前を前に足していく（最大32段）
            for (int i = 0; i < 32 && current != null; i++)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }


        // 目印を探す範囲（ピクセル）。ここに道路が無い時は、画像全体から探し直す
        private const int SnapSearchRadius = 48;

        /// <summary>
        /// ゴールの目印の位置（m_StartWorldPos）を、一番近い道路の、幅の真ん中へ寄せる。
        /// 道路に沿った位置はほとんど変わらず、道路の外や端から、中心へ動くだけ。
        /// </summary>
        /// <param name="goal">目印にする GoalTrigger（ログに名前を出すために使う）</param>
        /// <returns>目印を出してよいか（コースから離れすぎて、寄せられない時だけ false）</returns>
        private bool SnapStartToRoad(GoalTrigger goal)
        {
            // 自動で作ったコース画像の時だけ（手描きの画像には道路データが無い）
            if (m_SnapMask == null || m_SnapCell <= 0f)
            {
                return true;
            }

            // raw ＝ 寄せる前の位置
            Vector3 raw = m_StartWorldPos;

            // ワールド座標 → 道路データのピクセル座標
            float pixelX = (raw.x - m_SnapMin.x) / m_SnapCell;
            float pixelY = (raw.z - m_SnapMin.y) / m_SnapCell;

            // snappedX / snappedY ＝ 寄せた後の位置（ピクセル座標）
            float snappedX;
            float snappedY;

            // まず、近く（SnapSearchRadius ピクセル以内）の道路に寄せてみる
            bool snapped =
                MinimapCourseBuilder.SnapToRoadCenter(
                    m_SnapMask,
                    m_SnapWidth,
                    m_SnapHeight,
                    pixelX,
                    pixelY,
                    SnapSearchRadius,
                    out snappedX,
                    out snappedY
                );

            // farAway ＝ 近くに道路が無くて、遠くの道路に寄せたか
            bool farAway = false;

            if (!snapped)
            {
                // 近くに道路が無い。ゴールの位置が大きくズレている可能性があるので、
                // 画像全体から一番近い道路を探し直す
                snapped =
                    MinimapCourseBuilder.SnapToRoadCenter(
                        m_SnapMask,
                        m_SnapWidth,
                        m_SnapHeight,
                        pixelX,
                        pixelY,
                        Mathf.Max(m_SnapWidth, m_SnapHeight),
                        out snappedX,
                        out snappedY
                    );

                farAway = snapped;
            }

            // ログに出す、ゴールの名前（ヒエラルキー上のパス）
            string goalName = PathOf(goal.transform);

            // 画像全体を探しても寄せられなければ（コースから大きく離れている）、
            // 警告を出して、目印は出さない
            if (!snapped)
            {
                Debug.LogWarning(
                    "RaceMinimap : GoalTrigger「" + goalName + "」" +
                    "（X " + raw.x.ToString("F1") + ", Z " + raw.z.ToString("F1") + "）が、" +
                    "コースから大きく離れているため、ゴールの目印は出しません。" +
                    "GoalTriggerの位置を確認してください"
                );

                return false;
            }

            // 道路データのピクセル座標 → ワールド座標
            Vector3 result =
                new Vector3(
                    m_SnapMin.x + snappedX * m_SnapCell,
                    raw.y,
                    m_SnapMin.y + snappedY * m_SnapCell
                );

            // moved ＝ 寄せたことで動いた距離（m）。ログに出す用
            float moveX = result.x - raw.x;
            float moveZ = result.z - raw.z;
            float moved = Mathf.Sqrt(moveX * moveX + moveZ * moveZ);

            // 寄せた後の位置を、目印の位置にする
            m_StartWorldPos = result;

            // detail ＝ ログに出す、寄せる前 → 寄せた後の座標
            string detail =
                "X " + raw.x.ToString("F1") + ", Z " + raw.z.ToString("F1") +
                " → X " + result.x.ToString("F1") + ", Z " + result.z.ToString("F1");

            // 遠くの道路に寄せた時は、GoalTrigger の位置が
            // 間違っているかもしれないので、警告で知らせる
            if (farAway)
            {
                Debug.LogWarning(
                    "RaceMinimap : GoalTrigger「" + goalName + "」は道路から離れていたため、" +
                    "一番近い道路の上にゴールの目印を出しました（" + moved.ToString("F1") + "m 移動 / " +
                    detail + "）。GoalTriggerの位置が正しいか確認してください"
                );
            }
            else if (moved > m_SnapCell)
            {
                // 1ピクセル分以上動かした時だけログに出す（どれだけズレていたかの確認用）
                Debug.Log(
                    "RaceMinimap : ゴールの目印を道路の中心に寄せました" +
                    "（GoalTrigger「" + goalName + "」の位置から " + moved.ToString("F1") + "m 移動 / " +
                    detail + "）"
                );
            }

            return true;
        }

        // =====================================================
        // ■ 車のアイコン
        // =====================================================

        /// <summary>
        /// 対象に対応するアイコンを探す。
        /// </summary>
        /// <param name="target">探したい対象（車）の Transform</param>
        /// <returns>見つかったアイコン。無ければ null</returns>
        private Marker FindMarker(Transform target)
        {
            for (int i = 0; i < m_Markers.Count; i++)
            {
                if (m_Markers[i].Target == target)
                {
                    return m_Markers[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 車1台ぶんのアイコンを作って、一覧に足す。
        /// 「Marker_車の名前」の下に、Arrow・Body・Ring を、この順に作る。
        /// </summary>
        /// <param name="target">アイコンで追いかける対象（車）の Transform</param>
        /// <param name="color">アイコンの色（車の色）</param>
        /// <param name="playerNumber">プレイヤー番号。1以上ならアイコンの中に番号を出せる（0は番号なし）</param>
        /// <returns>作ったアイコン</returns>
        private Marker AddMarker(Transform target, Color color, int playerNumber = 0)
        {
            // 元の色が半透明でも、アイコンは不透明にして、はっきり見せる
            color.a = 1f;

            Marker marker = new Marker();

            marker.Target = target;
            marker.Root = CreateRect("Marker_" + target.name, m_Content);

            // 子の並び順 = 描画順（手前に来るものほど後ろに作る）
            marker.Arrow =
                CreateImage("Arrow", marker.Root, MinimapSprites.Arrow, Color.white);

            marker.Body =
                CreateImage("Body", marker.Root, MinimapSprites.Circle, color);

            marker.Ring =
                CreateImage("Ring", marker.Root, MinimapSprites.Ring, Color.white);

            // プレイヤー番号の文字（一番手前）。表示するかどうかは SyncMarkers が決める
            if (playerNumber > 0)
            {
                marker.Label = CreateNumberLabel(marker.Root, playerNumber);
            }

            // 丸（Body）と縁（Ring）は、Root いっぱいに広げる（大きさは Root で決まる）。
            // 三角（Arrow）の大きさは、ApplyMarkerStyle で決める
            Stretch(marker.Body.rectTransform);
            Stretch(marker.Ring.rectTransform);

            // マップが回転していても、アイコンは正立にする（RefreshLayout と同じ）
            marker.Root.localRotation =
                Quaternion.Euler(0f, 0f, -m_MapRotation);

            // 最初は、通常の車（自分の車ではない）の見た目にする
            ApplyMarkerStyle(marker, false);

            m_Markers.Add(marker);

            return marker;
        }

        /// <summary>
        /// アイコンの大きさと縁の色を、通常の車／自分の車のどちらかの見た目に切り替える。
        /// </summary>
        /// <param name="marker">切り替えるアイコン</param>
        /// <param name="isFocus">自分の車（強調表示する車）なら true</param>
        private void ApplyMarkerStyle(Marker marker, bool isFocus)
        {
            marker.IsFocus = isFocus;

            // 自分の車は、大きさと縁の色を変えて、目立たせる
            float size = isFocus ? m_FocusIconSize : m_IconSize;

            Color ringColor = isFocus ? m_FocusRingColor : m_IconRingColor;

            marker.Root.sizeDelta = new Vector2(size, size);

            marker.Ring.color = ringColor;

            // 三角は、縁と同じ色にする（表示する設定の時だけ出す）。
            // 丸より一回り大きい正方形の中で回転させる
            marker.Arrow.color = ringColor;
            marker.Arrow.enabled = m_ShowHeadingArrow;

            marker.Arrow.rectTransform.sizeDelta =
                new Vector2(size * 1.8f, size * 1.8f);

            // プレイヤー番号の文字は、アイコンの大きさに合わせる
            if (marker.Label != null)
            {
                marker.Label.fontSize = size * 0.72f;
            }

            // 自分の車は一番手前に描く
            if (isFocus)
            {
                marker.Root.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 車の増減と、自分の車の切り替わりを、アイコンに反映する。
        /// いなくなった車のアイコンを消し、新しく現れた車のアイコンを作り、
        /// 自分の車だけを強調表示する。
        /// </summary>
        private void SyncMarkers()
        {
            // 破棄された（いなくなった）対象のアイコンを片付ける。
            // RemoveAt で消すと、後ろの添字がずれるので、後ろから順に調べる
            for (int i = m_Markers.Count - 1; i >= 0; i--)
            {
                if (m_Markers[i].Target == null)
                {
                    if (m_Markers[i].Root != null)
                    {
                        Destroy(m_Markers[i].Root.gameObject);
                    }

                    m_Markers.RemoveAt(i);
                }
            }

            // 新しく現れた車のアイコンを作る
            CarMovement[] cars =
                FindObjectsByType<CarMovement>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < cars.Length; i++)
            {
                if (FindMarker(cars[i].transform) != null)
                {
                    continue;
                }

                // 車の色でアイコンを作る（色は、車体の「CarColor」マテリアルから取る）。
                // m_Markers.Count ＝ 何番目に現れた車か（色が取れなかった時の、
                // 予備の色を選ぶのに使う）
                AddMarker(
                    cars[i].transform,
                    ResolveCarColor(cars[i].transform, m_Markers.Count),
                    cars[i].m_PlayerNumber
                );
            }

            // 自分の車を決めて、強調表示を合わせる
            // （強調しない設定の時は、決めない＝全員を同じ見た目にする）
            Transform focus = m_HighlightFocus ? ResolveFocus(cars) : null;

            Marker focusMarker = null;  // 自分の車のアイコン（無ければ null）

            if (focus != null)
            {
                for (int i = 0; i < m_Markers.Count; i++)
                {
                    if (IsSameObject(m_Markers[i].Target, focus))
                    {
                        focusMarker = m_Markers[i];
                        break;
                    }
                }
            }

            // 自分の車のアイコンだけ強調表示にして、他は通常に戻す。
            // 見た目が変わる時だけ切り替える（毎回やり直さない）
            for (int i = 0; i < m_Markers.Count; i++)
            {
                bool isFocus = m_Markers[i] == focusMarker;

                if (m_Markers[i].IsFocus != isFocus)
                {
                    ApplyMarkerStyle(m_Markers[i], isFocus);
                }
            }

            // プレイヤー番号は、車が2台以上ある時だけ出す（1台だけなら番号は要らない）
            bool showNumbers = m_ShowPlayerNumber && m_Markers.Count > 1;

            for (int i = 0; i < m_Markers.Count; i++)
            {
                Marker marker = m_Markers[i];

                if (marker.Label != null &&
                    marker.Label.gameObject.activeSelf != showNumbers)
                {
                    marker.Label.gameObject.SetActive(showNumbers);
                }
            }
        }

        /// <summary>
        /// 毎フレーム、アイコンの位置と向きを、車に合わせる。
        /// </summary>
        private void UpdateMarkers()
        {
            for (int i = 0; i < m_Markers.Count; i++)
            {
                Marker marker = m_Markers[i];

                if (marker.Target == null || marker.Root == null)
                {
                    continue;
                }

                // 非アクティブな車（ラウンドのリセット中など）は隠す
                bool visible = marker.Target.gameObject.activeInHierarchy;

                if (marker.Root.gameObject.activeSelf != visible)
                {
                    marker.Root.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                // 車のワールド座標を、ミニマップ上の座標に直して、アイコンを動かす
                marker.Root.anchoredPosition =
                    WorldToMap(marker.Target.position);

                // 進行方向（ワールドのXZ平面での向き）
                Vector3 forward = marker.Target.forward;

                // yaw ＝ 車が向いている角度（度）。ワールドのZ方向（上）が0度で、X方向（右）が90度
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                // ワールドの向き(yaw)は時計回り、UIの回転は反時計回りなので
                // 符号を反転し、マップの回転ぶんを足す
                marker.Arrow.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, m_MapRotation - yaw);
            }
        }

        /// <summary>
        /// 自分の車（強調表示する車）を決める。
        /// 優先順位は、Inspectorで指定した車 → カメラ（RaceCameraFollow）が追っている車
        /// → 最初に見つかった車。
        /// </summary>
        /// <param name="cars">シーン内の車の一覧</param>
        /// <returns>自分の車の Transform。車が1台も無ければ null</returns>
        private Transform ResolveFocus(CarMovement[] cars)
        {
            // Inspectorで指定されていればそれを優先
            if (m_FocusTarget != null)
            {
                return m_FocusTarget;
            }

            // 次に、カメラが追っている車
            RaceCameraFollow[] cameras =
                FindObjectsByType<RaceCameraFollow>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].m_Target != null)
                {
                    return cameras[i].m_Target;
                }
            }

            // どちらも無ければ最初に見つかった車
            return cars.Length > 0 ? cars[0].transform : null;
        }

        /// <summary>
        /// 車体の「CarColor」マテリアルの色を取る（CarManagerが色を付ける対象と同じ）。
        /// 取れなければ、予備の色（Fallback Colors）を、出現順に使う。
        /// </summary>
        /// <param name="car">色を知りたい車の Transform</param>
        /// <param name="fallbackIndex">何番目に現れた車か（予備の色を選ぶのに使う）</param>
        /// <returns>アイコンに使う色</returns>
        private Color ResolveCarColor(Transform car, int fallbackIndex)
        {
            // 車の子を含む、すべての Renderer（描画部品）を調べる。
            // true ＝ 非アクティブなものも含める
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                // sharedMaterials ＝ 共有のマテリアル。読むだけなら、コピーを作らないこちらを使う
                Material[] materials = renderers[i].sharedMaterials;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];

                    if (material == null || !material.name.Contains("CarColor"))
                    {
                        continue;
                    }

                    // 色のプロパティの名前は、シェーダーで違う。URPのシェーダーは _BaseColor、
                    // 従来のものは _Color
                    if (material.HasProperty("_BaseColor"))
                    {
                        return material.GetColor("_BaseColor");
                    }

                    if (material.HasProperty("_Color"))
                    {
                        return material.GetColor("_Color");
                    }
                }
            }

            // 取れなかった時は出現順の色
            if (m_FallbackColors != null && m_FallbackColors.Length > 0)
            {
                // 色の数を超えたら、最初の色に戻る（余りで選ぶ）
                return m_FallbackColors[fallbackIndex % m_FallbackColors.Length];
            }

            return Color.white;
        }

        /// <summary>
        /// 同じ車か調べる（カメラの追従先が、車の子オブジェクトでも、同じとみなす）。
        /// </summary>
        /// <param name="a">比べる Transform</param>
        /// <param name="b">比べる Transform</param>
        /// <returns>同じ、または一方がもう一方の子孫なら true。どちらかが null なら false</returns>
        private static bool IsSameObject(Transform a, Transform b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return a == b || a.IsChildOf(b) || b.IsChildOf(a);
        }


        // =====================================================
        // ■ UI生成の補助関数
        // =====================================================

        /// <summary>
        /// 中央基準（anchor・pivot が中央）の RectTransform を持つ、子オブジェクトを作る。
        /// </summary>
        /// <param name="objectName">作るオブジェクトの名前</param>
        /// <param name="parent">親にする Transform</param>
        /// <returns>作ったオブジェクトの RectTransform</returns>
        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));

            // 親と同じレイヤーにする
            // （Screen Space - Cameraで、UIカメラが見えるレイヤーを限定している場合の対策）
            go.layer = parent.gameObject.layer;

            RectTransform rect = go.GetComponent<RectTransform>();

            rect.SetParent(parent, false);

            // 基準（anchor・pivot）を中央にして、親の中心に置く
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        /// <summary>
        /// Image を持つ子オブジェクトを作る（クリックは受け付けない）。
        /// </summary>
        /// <param name="objectName">作るオブジェクトの名前</param>
        /// <param name="parent">親にする Transform</param>
        /// <param name="sprite">表示するスプライト</param>
        /// <param name="color">Image.color（白いスプライトに掛けて、色を付ける）</param>
        /// <returns>作った Image</returns>
        private static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Color color)
        {
            RectTransform rect = CreateRect(objectName, parent);

            Image image = rect.gameObject.AddComponent<Image>();

            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;  // クリックを受け付けない（下のUIの操作を邪魔しない）

            return image;
        }

        /// <summary>
        /// 親いっぱいに広げる。
        /// </summary>
        /// <param name="rect">広げる対象の RectTransform</param>
        private static void Stretch(RectTransform rect)
        {
            // 四隅の基準を、親の四隅に合わせて、余白（offset）を0にする
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// アイコンの中に出す、プレイヤー番号の文字を作る。
        /// 白い太字に黒の縁を付けて、どの車の色の上でも読めるようにする。
        /// 大きさは ApplyMarkerStyle が決める。
        /// </summary>
        /// <param name="parent">アイコンの親（Root）</param>
        /// <param name="number">プレイヤー番号</param>
        /// <returns>作った文字</returns>
        private static TextMeshProUGUI CreateNumberLabel(RectTransform parent, int number)
        {
            RectTransform rect = CreateRect("Number", parent);

            Stretch(rect);

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.text = number.ToString();
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            // 1文字なので、折り返さず、はみ出しても切らない
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            // 白い文字に黒い縁
            label.outlineWidth = 0.3f;
            label.outlineColor = new Color32(0, 0, 0, 255);

            label.raycastTarget = false;  // クリックを受け付けない

            // 表示するかどうかは SyncMarkers が決める
            rect.gameObject.SetActive(false);

            return label;
        }
    }
}
