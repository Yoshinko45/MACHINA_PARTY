using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レースの結果発表（順位表）
    // =====================================================================
    //
    // レースが終わった後に出す、マリカー風の順位表。
    //
    //   ・上に「RESULTS」
    //   ・順位の表：1位〜最下位まで。下の順位から1行ずつ右から滑り込んで、最後に1位が出る
    //       （順位／車のアイコン／プレイヤー名と車種／ゴールタイム）
    //   ・表が出そろったら、下に3つのボタン
    //       「もう一度（RETRY）」「車を選びなおす（CAR SELECT）」「タイトルへ（TITLE）」
    //
    // ボタンは、誰の入力装置でも操作できる（左右で選ぶ／決定ボタンで決める）。
    // マウスでも選べる。決めた結果は、OnRetry・OnCarSelect・OnTitle で RaceGameManager に伝える。
    //
    // ゴールしていない人は、タイムの代わりに「---」と出す。
    //   ・最下位の人（IsLastPlace）… 「最下位（LAST PLACE）」
    //   ・それ以外（時間切れで終わった時など）… 「未ゴール（DID NOT FINISH）」
    //
    // =====================================================================

    /// <summary>
    /// 結果の表の1行ぶん（1人の結果）。
    /// </summary>
    public sealed class RaceResultRow
    {
        /// <summary>プレイヤー番号（1〜4）</summary>
        public int PlayerNumber = 1;

        /// <summary>最終順位（1から）</summary>
        public int Rank = 1;

        /// <summary>ゴールしたか（最後の1人は、ゴールせずに最下位が決まるので false）</summary>
        public bool Finished = true;

        /// <summary>
        /// 最下位か（ゴールせずに最下位になった人だけ true）。
        /// ゴールしていない人のうち、これが false の人は「未ゴール」（時間切れで終わった時など）と出す。
        /// </summary>
        public bool IsLastPlace = false;

        /// <summary>ゴールタイム（秒）。Finished が true の時だけ意味がある</summary>
        public float Time;

        /// <summary>選んだ車種の番号（CarTypeCatalog の番号）</summary>
        public int CarTypeIndex;

        /// <summary>入力装置の表示名（「WASD」など）</summary>
        public string DeviceLabel = "";
    }

    /// <summary>
    /// レースの結果発表。
    /// </summary>
    public class RaceResultsUI : MonoBehaviour
    {
        // =================================================================
        // ■ 設定
        // =================================================================

        [Header("見た目")]

        [Tooltip("英語の見出しに使うフォント。空ならTextMeshProの標準フォントを使います")]
        public TMP_FontAsset m_Font;

        [Tooltip("表示の前後の順番（大きいほど手前）。レース中の表示（100）より大きくしてください")]
        public int m_SortingOrder = 300;

        [Tooltip("画面全体を暗くする濃さ（0〜1）")]
        [Range(0f, 1f)]
        public float m_BackdropAlpha = 0.78f;

        [Header("動き")]

        [Tooltip("1行ずつ現れる間隔（秒）")]
        public float m_RowInterval = 0.5f;

        [Tooltip("最初の行が現れるまでの待ち時間（秒）")]
        public float m_StartDelay = 0.6f;

        [Tooltip("表が出そろってから、ボタンが現れるまでの時間（秒）")]
        public float m_ButtonDelay = 0.6f;

        [Tooltip("ボタンが現れてから、決定を受け付け始めるまでの時間（秒）。" +
            "ゴール直後にボタンを連打していた人が、うっかり決めてしまうのを防ぎます")]
        public float m_InputGuard = 0.5f;

        [Tooltip("マウスで選べるようにする")]
        public bool m_AllowMouse = true;


        // =================================================================
        // ■ イベント
        // =================================================================

        /// <summary>「もう一度」が決まった</summary>
        public System.Action OnRetry;

        /// <summary>「車を選びなおす」が決まった</summary>
        public System.Action OnCarSelect;

        /// <summary>「タイトルへ」が決まった</summary>
        public System.Action OnTitle;


        // =================================================================
        // ■ 内部：行・ボタン
        // =================================================================

        private sealed class RowView
        {
            public RectTransform Root;     // 行の親（位置を動かす）
            public CanvasGroup Group;      // 透明度
            public float RevealAt;         // 現れ始める時刻（表示してからの秒）
            public float BaseY;            // 行の高さの位置
            public Image Glow;             // 1位の行の光
            public bool IsWinner;          // 1位の行か
            public string Text;            // 行の文字（テスト・確認用）
        }

        private sealed class ButtonView
        {
            public RectTransform Root;
            public Image Background;
            public Image Outline;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Sub;
            public System.Action Action;
        }

        // ---- 寸法・色（Canvas の基準 1920 × 1080 でのピクセル。位置は、画面の真ん中から見たもの） ----
        //
        // 見た目は、タンクモードの参加画面に寄せてある：
        // 傾いた薄い水色の板・青緑の札・水色とオレンジの斜めの帯。色は RaceStyle（RaceUiKit.cs）にまとめてある。

        // 画面の基準（1920 × 1080）の高さの半分
        private const float HalfHeight = 540f;

        // 順位の行（板）の大きさ・間隔・傾き・影
        private const float RowWidth = 1240f;
        private const float RowHeight = 132f;
        private const float RowGap = 14f;
        private const float RowSlant = 22f;         // 板の左右の端の、斜めの大きさ
        private const float RowTilt = 1f;           // 板の傾き（度。右側が少し高い）
        private const float RowShadowDepth = 12f;

        // 表・ボタン・操作案内の、真ん中から見た高さ
        private const float TableCenterY = 10f;
        private const float ButtonsCenterY = -390f;
        private const float HintCenterY = -486f;

        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        // ボタンの色：選んでいない時は水色、選んでいる時はオレンジ
        private static readonly Color ButtonNormal = RaceStyle.Azure;
        private static readonly Color ButtonSelected = RaceStyle.Orange;

        // タイムの札の色：最下位の人／時間切れで終わった人（ゴールした人は青緑）
        private static readonly Color LastPlaceColor = new Color(0.78f, 0.16f, 0.14f, 1f);
        private static readonly Color NotFinishedColor = new Color(0.30f, 0.42f, 0.48f, 1f);


        // =================================================================
        // ■ 内部変数
        // =================================================================

        private RaceFonts m_Fonts;
        private Canvas m_Canvas;
        private RectTransform m_Root;
        private Image m_Backdrop;
        private Image m_Band;
        private CanvasGroup m_TitleGroup;
        private CanvasGroup m_ButtonsGroup;
        private CanvasGroup m_HintGroup;

        private readonly List<RowView> m_Rows = new List<RowView>();
        private readonly List<ButtonView> m_Buttons = new List<ButtonView>();
        private readonly List<RaceMenuEvent> m_Events = new List<RaceMenuEvent>();

        private float m_ShownAt;       // 表示を始めた時刻（Time.unscaledTime）
        private float m_ButtonsAt;     // ボタンが現れ始める時刻（表示してからの秒）
        private int m_Selected;        // 選んでいるボタン
        private bool m_Decided;        // どれかを決めた（二重に決めない）


        // =================================================================
        // ■ 外から見える状態
        // =================================================================

        /// <summary>結果を表示しているか</summary>
        public bool IsShown
        {
            get { return m_Root != null; }
        }

        /// <summary>表の行の数</summary>
        public int RowCount
        {
            get { return m_Rows.Count; }
        }

        /// <summary>
        /// 行の文字（上から数えた番号。テスト・確認用）。
        /// 「順位 P番号 車種 タイム」の順。
        /// </summary>
        public string RowTextOf(int index)
        {
            if (index < 0 || index >= m_Rows.Count)
            {
                return "";
            }

            return m_Rows[index].Text;
        }

        /// <summary>選んでいるボタン（0 ＝ もう一度／1 ＝ 車を選びなおす／2 ＝ タイトルへ）</summary>
        public int SelectedIndex
        {
            get { return m_Selected; }
        }

        /// <summary>ボタンが現れて、操作できるか</summary>
        public bool ButtonsReady
        {
            get
            {
                return m_Root != null &&
                    Time.unscaledTime - m_ShownAt >= m_ButtonsAt + m_InputGuard;
            }
        }


        // =================================================================
        // ■ 表示する・消す
        // =================================================================

        /// <summary>
        /// 結果を表示する。すでに出ている時は、作り直す。
        /// </summary>
        /// <param name="rows">1人ぶんの結果（順番は問わない。順位の順に並べ直す）</param>
        /// <param name="fonts">使うフォント（null なら Inspector の Font から用意する）</param>
        public void Show(RaceResultRow[] rows, RaceFonts fonts)
        {
            Hide();

            m_Fonts = fonts != null ? fonts : RaceFonts.Resolve(m_Font);

            // 順位の順に並べる（同じ順位なら、プレイヤー番号の順）
            List<RaceResultRow> sorted = new List<RaceResultRow>();

            if (rows != null)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] != null)
                    {
                        sorted.Add(rows[i]);
                    }
                }
            }

            sorted.Sort(
                delegate (RaceResultRow a, RaceResultRow b)
                {
                    int byRank = a.Rank.CompareTo(b.Rank);

                    return byRank != 0 ? byRank : a.PlayerNumber.CompareTo(b.PlayerNumber);
                });

            // 横長・縦長の画面でも、表やボタンがはみ出さないよう、1920 × 1080 の範囲が全部見える Canvas にする
            m_Canvas = RaceUiKit.CreateOverlayCanvas("RaceResults Canvas", transform, m_SortingOrder, true);

            m_Root = RaceUiKit.CreateRect("RaceResults", m_Canvas.transform);

            RaceUiKit.Stretch(m_Root);

            BuildBackdrop();
            BuildTitle();
            BuildRows(sorted);
            BuildButtons();
            BuildHint();

            m_ShownAt = Time.unscaledTime;
            m_Selected = 0;
            m_Decided = false;

            // ボタンが現れる時刻 ＝ 最初の行 ＋ 行の数ぶんの間隔 ＋ 待ち
            m_ButtonsAt =
                m_StartDelay +
                Mathf.Max(0, sorted.Count - 1) * m_RowInterval +
                0.5f +
                m_ButtonDelay;

            ApplySelection();
            UpdateAnimation(0f);
        }

        /// <summary>
        /// 結果を消す。
        /// </summary>
        public void Hide()
        {
            if (m_Canvas != null)
            {
                Destroy(m_Canvas.gameObject);
            }

            m_Canvas = null;
            m_Root = null;
            m_Backdrop = null;
            m_Band = null;

            m_Rows.Clear();
            m_Buttons.Clear();
        }

        private void OnDestroy()
        {
            Hide();
        }


        // =================================================================
        // ■ 毎フレーム
        // =================================================================

        private void Update()
        {
            if (m_Root == null)
            {
                return;
            }

            float t = Time.unscaledTime - m_ShownAt;

            UpdateAnimation(t);

            // ボタンが操作できるようになってから、入力を受け付ける
            if (t >= m_ButtonsAt + m_InputGuard && !m_Decided)
            {
                HandleInput();
            }
        }

        /// <summary>
        /// 行が滑り込み始める位置（真ん中からの横のずれ）。画面の右の外に、行がすっかり隠れる距離。
        /// </summary>
        private static float SlideDistance()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

            // 画面の半分の幅（基準の高さは 1080。横に長い画面は、その分だけ広い）
            float halfWidth = Mathf.Max(960f, HalfHeight * aspect);

            return halfWidth + RowWidth * 0.5f + 60f;
        }

        /// <summary>
        /// 表示してからの時間に合わせて、背景・行・ボタンの動きを進める。
        /// </summary>
        private void UpdateAnimation(float t)
        {
            // 背景がだんだん暗くなる（下の帯も一緒に出てくる）
            float backdropT = Mathf.Clamp01(t / 0.4f);

            RaceUiKit.SetAlpha(m_Backdrop, m_BackdropAlpha * backdropT);
            RaceUiKit.SetAlpha(m_Band, 0.95f * backdropT);

            m_TitleGroup.alpha = Mathf.Clamp01(t / 0.35f);

            // 行：右から滑り込む（画面の外から。横に長い画面でも、途中で見えてしまわない距離）
            float slideFrom = SlideDistance();

            for (int i = 0; i < m_Rows.Count; i++)
            {
                RowView row = m_Rows[i];

                float local = t - row.RevealAt;

                float slide = RaceEase.OutCubic(local / 0.5f);

                row.Group.alpha = Mathf.Clamp01(local / 0.2f);

                row.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(slideFrom, 0f, slide), row.BaseY);

                // 1位の行は、現れた後でゆっくり光る
                if (row.IsWinner && row.Glow != null)
                {
                    float glow =
                        Mathf.Clamp01((local - 0.4f) / 0.3f) * (0.85f + 0.15f * Mathf.Sin(local * 3.2f));

                    RaceUiKit.SetAlpha(row.Glow, Mathf.Clamp01(glow));
                }
            }

            // ボタン：表が出そろってから現れる
            float buttonT = (t - m_ButtonsAt) / 0.4f;

            m_ButtonsGroup.alpha = Mathf.Clamp01(buttonT);
            m_HintGroup.alpha = Mathf.Clamp01(buttonT);

            // 選んでいるボタンは、ゆっくり脈打つ
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                float scale = 1f;

                if (i == m_Selected)
                {
                    scale = 1.06f + 0.015f * Mathf.Sin(t * 6f);
                }

                m_Buttons[i].Root.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>
        /// 入力を調べる。左右で選び、決定ボタンで決める。誰の入力装置でも使える。
        /// </summary>
        private void HandleInput()
        {
            RaceMenuInput.Poll(m_Events);

            for (int i = 0; i < m_Events.Count; i++)
            {
                RaceMenuEvent e = m_Events[i];

                if (e.Horizontal != 0)
                {
                    m_Selected = Wrap(m_Selected + e.Horizontal, m_Buttons.Count);

                    ApplySelection();
                }

                if (e.Confirm)
                {
                    Decide(m_Selected);

                    return;
                }
            }

            if (m_AllowMouse)
            {
                HandleMouse();
            }
        }

        /// <summary>
        /// マウスの操作を調べる。ボタンの上に動かすと選び、クリックで決める。
        /// </summary>
        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();

            bool moved = mouse.delta.ReadValue().sqrMagnitude > 0.01f;

            for (int i = 0; i < m_Buttons.Count; i++)
            {
                // null ＝ Overlay の Canvas なので、カメラは要らない
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        m_Buttons[i].Root, position, null))
                {
                    continue;
                }

                if (moved && m_Selected != i)
                {
                    m_Selected = i;

                    ApplySelection();
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Decide(i);

                    return;
                }
            }
        }

        /// <summary>
        /// ボタンを決める。決めたら、対応するイベントを呼ぶ。
        /// </summary>
        private void Decide(int index)
        {
            if (m_Decided || index < 0 || index >= m_Buttons.Count)
            {
                return;
            }

            m_Decided = true;

            System.Action action = m_Buttons[index].Action;

            if (action != null)
            {
                action();
            }
        }

        /// <summary>
        /// 選んでいるボタンの見た目を合わせる。
        /// </summary>
        private void ApplySelection()
        {
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                bool selected = i == m_Selected;

                m_Buttons[i].Background.color = selected ? ButtonSelected : ButtonNormal;

                RaceUiKit.SetAlpha(m_Buttons[i].Outline, selected ? 1f : 0f);
            }
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return ((value % count) + count) % count;
        }


        // =================================================================
        // ■ 画面を作る
        // =================================================================

        private void BuildBackdrop()
        {
            // 画面全体を覆う、濃い青緑の幕（後ろのレースの画面が、暗く透けて見える）
            m_Backdrop =
                RaceUiKit.CreateImage(
                    "Backdrop",
                    m_Root,
                    null,
                    RaceColors.WithAlpha(RaceStyle.DeepTeal, 0f),
                    false);

            RaceUiKit.Stretch(m_Backdrop.rectTransform);

            // 結果の表示中は、後ろの画面への操作を受けない
            m_Backdrop.raycastTarget = true;

            // 画面の下の、青緑の帯（車種選択の画面と同じ）
            m_Band =
                RaceUiKit.CreateImage(
                    "Band",
                    m_Root,
                    RaceUiKit.BandSprite(),
                    RaceColors.WithAlpha(RaceStyle.Teal, 0f),
                    false);

            // 横に長い画面でも端まで届くよう、左右いっぱいに広げる（高さは320）
            m_Band.rectTransform.anchorMin = new Vector2(0f, 0f);
            m_Band.rectTransform.anchorMax = new Vector2(1f, 0f);
            m_Band.rectTransform.offsetMin = Vector2.zero;
            m_Band.rectTransform.offsetMax = new Vector2(0f, 320f);
        }

        /// <summary>
        /// 上の見出し（RESULTS）を作る。
        /// </summary>
        private void BuildTitle()
        {
            RectTransform title = RaceUiKit.CreateRect("Title", m_Root);

            // 画面の真ん中を基準に置く（画面の縦横比が変わっても、表との位置関係が変わらないように）
            RaceUiKit.Place(
                title,
                Mid,
                Mid,
                new Vector2(0f, HalfHeight - 121f),
                new Vector2(1240f, 170f));

            m_TitleGroup = title.gameObject.AddComponent<CanvasGroup>();

            // 影（少し右下にずらした、青緑の文字）→ 本体（白）の順に重ねる
            CreateTitleText(title, "TitleShadow", RaceStyle.ShadowTeal, new Vector2(7f, -7f), false);
            CreateTitleText(title, "TitleText", Color.white, Vector2.zero, true);

            // 日本語のフォントがある時は、水色の小さな帯で添える
            if (m_Fonts.HasJapanese)
            {
                Vector2 subSize = new Vector2(280f, 48f);

                RacePlate sub =
                    RaceUiKit.CreatePlate(
                        "TitleSubPlate",
                        title,
                        subSize,
                        12f,
                        12f,
                        RaceStyle.Azure,
                        RaceStyle.ShadowTeal,
                        6f,
                        0f);

                RaceUiKit.Place(sub.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), subSize);

                TextMeshProUGUI text =
                    RaceUiKit.CreateText(
                        "TitleSub",
                        sub.Root,
                        m_Fonts.Japanese,
                        30f,
                        Color.white,
                        TextAlignmentOptions.Center,
                        FontStyles.Bold);

                RaceUiKit.Stretch(text.rectTransform);

                text.text = "レース結果";
            }
        }

        private void CreateTitleText(RectTransform parent, string name, Color color, Vector2 offset, bool outline)
        {
            TextMeshProUGUI text =
                RaceUiKit.CreateText(
                    name,
                    parent,
                    m_Fonts.Main,
                    116f,
                    color,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                text.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                offset,
                new Vector2(1240f, 130f));

            text.text = "RESULTS";

            if (outline)
            {
                RaceUiKit.SetOutline(text, 0.16f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.65f));
            }
        }

        /// <summary>
        /// 順位の表を作る。1行ずつ傾けた水色の板で、上から1位。
        /// </summary>
        private void BuildRows(List<RaceResultRow> rows)
        {
            RectTransform table = RaceUiKit.CreateRect("Table", m_Root);

            RaceUiKit.Place(
                table,
                Mid,
                Mid,
                new Vector2(0f, TableCenterY),
                new Vector2(RowWidth, 620f));

            int count = rows.Count;

            // 行の数が少ない時は、真ん中に寄せる
            float totalHeight = count * RowHeight + Mathf.Max(0, count - 1) * RowGap;

            for (int i = 0; i < count; i++)
            {
                RaceResultRow data = rows[i];

                RowView view = new RowView();

                // 行の板（傾いた水色。影つき）。行の親は、この板
                RacePlate plate =
                    RaceUiKit.CreatePlate(
                        "Row " + data.Rank,
                        table,
                        new Vector2(RowWidth, RowHeight),
                        RowSlant,
                        RowSlant,
                        RaceStyle.Steel,
                        RaceStyle.ShadowTeal,
                        RowShadowDepth,
                        RowTilt);

                view.Root = plate.Root;

                // 上から i 行目（原点は表の真ん中）
                view.BaseY = totalHeight * 0.5f - RowHeight * 0.5f - i * (RowHeight + RowGap);

                RaceUiKit.Place(
                    view.Root,
                    Mid,
                    Mid,
                    new Vector2(0f, view.BaseY),
                    new Vector2(RowWidth, RowHeight));

                view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();

                // 下の順位から現れる（最後に1位）
                int order = count - 1 - i;

                view.RevealAt = m_StartDelay + order * m_RowInterval;
                view.IsWinner = i == 0;

                BuildRowContents(view, data);

                m_Rows.Add(view);
            }
        }

        /// <summary>
        /// 1行の中身（順位・車のアイコン・名前・タイム）を作る。
        /// </summary>
        private void BuildRowContents(RowView view, RaceResultRow data)
        {
            Color playerColor = RaceColors.Player(data.PlayerNumber);

            CarTypeInfo car = CarTypeCatalog.Get(data.CarTypeIndex);

            Color outline = RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.75f);

            Vector2 left = new Vector2(0f, 0.5f);
            Vector2 right = new Vector2(1f, 0.5f);

            // 1位の行の光（板より少し大きい金色の縁。板の後ろに置く）
            if (view.IsWinner)
            {
                Vector2 glowSize = new Vector2(RowWidth + 22f, RowHeight + 22f);

                view.Glow =
                    RaceUiKit.CreateImage(
                        "Glow",
                        view.Root,
                        RaceUiKit.PlateSprite(Mathf.RoundToInt(glowSize.x), Mathf.RoundToInt(glowSize.y), RowSlant, RowSlant),
                        RaceColors.WithAlpha(RaceColors.Gold, 0f),
                        false);

                RaceUiKit.Place(view.Glow.rectTransform, Mid, Mid, Vector2.zero, glowSize);

                view.Glow.rectTransform.SetAsFirstSibling();
            }

            // 順位（青緑の札に、金・銀・銅の数字）
            Vector2 rankSize = new Vector2(216f, 104f);

            RacePlate rankPlate =
                RaceUiKit.CreatePlate(
                    "RankPlate",
                    view.Root,
                    rankSize,
                    14f,
                    14f,
                    RaceStyle.Teal,
                    RaceStyle.DeepTeal,
                    0f,
                    0f);

            RaceUiKit.Place(rankPlate.Root, left, left, new Vector2(26f, 0f), rankSize);

            TextMeshProUGUI rank =
                RaceUiKit.CreateText(
                    "Rank",
                    rankPlate.Root,
                    m_Fonts.Main,
                    92f,
                    RaceColors.Rank(data.Rank),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            rank.richText = true;

            RaceUiKit.Stretch(rank.rectTransform);

            rank.text = data.Rank + "<size=45%>" + RaceOrdinal.Suffix(data.Rank) + "</size>";

            RaceUiKit.SetOutline(rank, 0.2f, outline);

            // 車のアイコン（プレイヤーの色）
            Image icon =
                RaceUiKit.CreateImage(
                    "CarIcon",
                    view.Root,
                    RaceUiKit.CarIconSprite(),
                    playerColor,
                    false);

            float iconScale = CarTuning.ModelScale(car.Spec.Weight);

            RaceUiKit.Place(
                icon.rectTransform,
                left,
                Mid,
                new Vector2(296f, 0f),
                new Vector2(64f * iconScale, 96f * iconScale));

            icon.preserveAspect = true;

            // プレイヤーのバッジ（P1 など。プレイヤーの色の札）
            Vector2 badgeSize = new Vector2(96f, 50f);

            RacePlate badge =
                RaceUiKit.CreatePlate(
                    "Badge",
                    view.Root,
                    badgeSize,
                    10f,
                    10f,
                    playerColor,
                    RaceStyle.ShadowTeal,
                    5f,
                    0f);

            RaceUiKit.Place(badge.Root, left, left, new Vector2(354f, 0f), badgeSize);

            TextMeshProUGUI badgeText =
                RaceUiKit.CreateText(
                    "BadgeText",
                    badge.Root,
                    m_Fonts.Main,
                    34f,
                    RaceColors.TextOn(playerColor),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Stretch(badgeText.rectTransform);

            badgeText.text = RaceColors.PlayerName(data.PlayerNumber);

            // 車種の名前と、入力装置（水色の板の上なので、濃い青緑の文字）
            TextMeshProUGUI name =
                RaceUiKit.CreateText(
                    "CarName",
                    view.Root,
                    m_Fonts.Main,
                    50f,
                    RaceStyle.DeepTeal,
                    TextAlignmentOptions.MidlineLeft,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                name.rectTransform,
                left,
                left,
                new Vector2(480f, 18f),
                new Vector2(340f, 64f));

            RaceUiKit.FitText(name, 28f, 50f);

            name.text = car.NameEn;

            TextMeshProUGUI device =
                RaceUiKit.CreateText(
                    "Device",
                    view.Root,
                    m_Fonts.Main,
                    28f,
                    RaceStyle.Teal,
                    TextAlignmentOptions.MidlineLeft,
                    FontStyles.Bold);

            RaceUiKit.Place(
                device.rectTransform,
                left,
                left,
                new Vector2(480f, -28f),
                new Vector2(340f, 36f));

            RaceUiKit.FitText(device, 16f, 28f);

            device.text = data.DeviceLabel ?? "";

            // タイム（ゴールしていない人は「---」）。右端の札の上に出す
            string timeString;

            if (data.Finished)
            {
                timeString = RaceTimeFormat.Format(data.Time);
            }
            else
            {
                timeString = RaceTimeFormat.Format(-1f);
            }

            // 札の色：ゴールした人は青緑／最下位は赤／時間切れで終わった人は灰色がかった青緑
            Color timeColor = RaceStyle.Teal;

            if (!data.Finished)
            {
                timeColor = data.IsLastPlace ? LastPlaceColor : NotFinishedColor;
            }

            Vector2 timeSize = new Vector2(400f, 104f);

            RacePlate timePlate =
                RaceUiKit.CreatePlate(
                    "TimePlate",
                    view.Root,
                    timeSize,
                    14f,
                    14f,
                    timeColor,
                    RaceStyle.DeepTeal,
                    0f,
                    0f);

            RaceUiKit.Place(timePlate.Root, right, right, new Vector2(-40f, 0f), timeSize);

            TextMeshProUGUI time =
                RaceUiKit.CreateText(
                    "Time",
                    timePlate.Root,
                    m_Fonts.Main,
                    62f,
                    data.Finished ? Color.white : new Color(1f, 1f, 1f, 0.7f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                time.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, data.Finished ? 0f : 14f),
                new Vector2(timeSize.x - 40f, 76f));

            time.text = timeString;

            RaceUiKit.SetOutline(time, 0.16f, outline);

            if (!data.Finished)
            {
                // 最下位は「最下位」、時間切れで終わった人は「未ゴール」
                TextMeshProUGUI last =
                    RaceUiKit.CreateText(
                        data.IsLastPlace ? "LastPlace" : "DidNotFinish",
                        timePlate.Root,
                        m_Fonts.ForJapanese,
                        32f,
                        Color.white,
                        TextAlignmentOptions.Center,
                        FontStyles.Bold);

                RaceUiKit.Place(
                    last.rectTransform,
                    Mid,
                    Mid,
                    new Vector2(0f, -28f),
                    new Vector2(timeSize.x - 40f, 40f));

                RaceUiKit.FitText(last, 18f, 32f);

                last.text =
                    data.IsLastPlace
                        ? m_Fonts.Pick("LAST PLACE", "最下位")
                        : m_Fonts.Pick("DID NOT FINISH", "未ゴール");
            }

            view.Text =
                data.Rank + RaceOrdinal.Suffix(data.Rank) + " " +
                RaceColors.PlayerName(data.PlayerNumber) + " " +
                car.NameEn + " " +
                (data.Finished ? timeString : (data.IsLastPlace ? "LAST" : "DNF"));
        }

        /// <summary>
        /// 下のボタン（もう一度／車を選びなおす／タイトルへ）を作る。
        /// 選んでいないボタンは水色、選んでいるボタンはオレンジ（タンクモードのスタートボタンと同じ）。
        /// </summary>
        private void BuildButtons()
        {
            RectTransform holder = RaceUiKit.CreateRect("Buttons", m_Root);

            RaceUiKit.Place(
                holder,
                Mid,
                Mid,
                new Vector2(0f, ButtonsCenterY),
                new Vector2(1240f, 110f));

            m_ButtonsGroup = holder.gameObject.AddComponent<CanvasGroup>();

            string[] labels = { "RETRY", "CAR SELECT", "TITLE" };
            string[] subs = { "もう一度走る", "車を選びなおす", "タイトルへ" };

            System.Action[] actions =
            {
                delegate { if (OnRetry != null) OnRetry(); },
                delegate { if (OnCarSelect != null) OnCarSelect(); },
                delegate { if (OnTitle != null) OnTitle(); }
            };

            const float buttonWidth = 380f;
            const float buttonHeight = 100f;
            const float gap = 50f;
            const float slant = 20f;
            const float rim = 6f;

            for (int i = 0; i < 3; i++)
            {
                ButtonView button = new ButtonView();

                button.Root = RaceUiKit.CreateRect("Button " + labels[i], holder);

                float x = (i - 1) * (buttonWidth + gap);

                RaceUiKit.Place(
                    button.Root,
                    Mid,
                    Mid,
                    new Vector2(x, 0f),
                    new Vector2(buttonWidth, buttonHeight));

                // 選んでいる時の白い縁（板より少し大きい）
                Vector2 rimSize = new Vector2(buttonWidth + rim * 2f, buttonHeight + rim * 2f);

                button.Outline =
                    RaceUiKit.CreateImage(
                        "Outline",
                        button.Root,
                        RaceUiKit.PlateSprite(Mathf.RoundToInt(rimSize.x), Mathf.RoundToInt(rimSize.y), slant, slant),
                        Color.white,
                        false);

                RaceUiKit.Place(button.Outline.rectTransform, Mid, Mid, Vector2.zero, rimSize);

                // 斜めに切った帯（影つき）
                RacePlate plate =
                    RaceUiKit.CreatePlate(
                        "Plate",
                        button.Root,
                        new Vector2(buttonWidth, buttonHeight),
                        slant,
                        slant,
                        ButtonNormal,
                        RaceStyle.ShadowTeal,
                        10f,
                        0f);

                RaceUiKit.Place(plate.Root, Mid, Mid, Vector2.zero, new Vector2(buttonWidth, buttonHeight));

                button.Background = plate.Face;

                bool hasSub = m_Fonts.HasJapanese;

                button.Label =
                    RaceUiKit.CreateText(
                        "Label",
                        button.Root,
                        m_Fonts.Main,
                        46f,
                        Color.white,
                        TextAlignmentOptions.Center,
                        FontStyles.Bold | FontStyles.Italic);

                RaceUiKit.Place(
                    button.Label.rectTransform,
                    Mid,
                    Mid,
                    new Vector2(0f, hasSub ? 12f : 0f),
                    new Vector2(buttonWidth - 60f, 56f));

                RaceUiKit.FitText(button.Label, 28f, 46f);

                RaceUiKit.SetOutline(button.Label, 0.16f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.6f));

                button.Label.text = labels[i];

                if (hasSub)
                {
                    button.Sub =
                        RaceUiKit.CreateText(
                            "Sub",
                            button.Root,
                            m_Fonts.Japanese,
                            24f,
                            new Color(1f, 1f, 1f, 0.95f),
                            TextAlignmentOptions.Center,
                            FontStyles.Bold);

                    RaceUiKit.Place(
                        button.Sub.rectTransform,
                        Mid,
                        Mid,
                        new Vector2(0f, -26f),
                        new Vector2(buttonWidth - 60f, 32f));

                    RaceUiKit.FitText(button.Sub, 16f, 24f);

                    RaceUiKit.SetOutline(button.Sub, 0.14f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.55f));

                    button.Sub.text = subs[i];
                }

                button.Action = actions[i];

                m_Buttons.Add(button);
            }
        }

        /// <summary>
        /// 一番下の操作案内を作る。
        /// </summary>
        private void BuildHint()
        {
            TextMeshProUGUI hint =
                RaceUiKit.CreateText(
                    "Hint",
                    m_Root,
                    m_Fonts.Main,
                    26f,
                    new Color(1f, 1f, 1f, 0.9f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Place(
                hint.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, HintCenterY),
                new Vector2(1500f, 40f));

            RaceUiKit.FitText(hint, 16f, 26f);

            RaceUiKit.SetOutline(hint, 0.18f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.9f));

            hint.text = "SELECT : A D / LEFT RIGHT / D-PAD        OK : SPACE / ENTER / A";

            m_HintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        }
    }
}