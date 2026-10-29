using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レース中の表示（周回・順位・タイム・ゴール・最下位確定）
    // =====================================================================
    //
    // 画面分割の1区画ごとに、そのプレイヤー専用の表示を出す（マリカーと同じ考え方）。
    //
    //   ・左上（右側の区画は右上）… LAP 2/3 と、レースのタイム
    //   ・左下（右側の区画は右下）… 今の順位（1st・2nd…。1位＝金／2位＝銀／3位＝銅）
    //   ・区画の上の真ん中 …………… プレイヤー名のバッジ（2人以上の時だけ）
    //   ・真ん中 …………………………… 「LAP 2」「FINAL LAP!」「FINISH!」などの大きな表示
    //   ・真ん中 …………………………… 最下位が決まった人にだけ「最下位確定」
    //   ・ゴールした後 ………………… 自分の順位とタイム（他の人を待つ間）
    //   ・どのカメラにも映らない空き枠（3人の時の右下） … 色で塗りつぶす（ミニマップが、この上に載る）
    //
    // 表示は、区画の大きさに合わせて拡大・縮小する
    // （1人プレイの全画面では大きく、4分割の小さな区画では標準の大きさになる）。
    //
    // このクラスは「表示するだけ」。いつ何を表示するかは RaceGameManager が決めて、
    // SetLap・SetRank・ShowFinish などを呼んでくる。
    //
    // =====================================================================

    /// <summary>
    /// レース中の表示（画面分割の区画ごと）。
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        // =================================================================
        // ■ 設定
        // =================================================================

        [Header("見た目")]

        [Tooltip("数字や英語の見出しに使うフォント。空ならTextMeshProの標準フォントを使います。" +
            "日本語（最下位確定）は、Race Game Manager が用意した日本語フォントで表示します")]
        public TMP_FontAsset m_Font;

        [Tooltip("表示の前後の順番（大きいほど手前）。スタート演出（500）より小さくしてください")]
        public int m_SortingOrder = 100;

        [Tooltip("空き枠（3人の時の右下）を塗る画像の、表示の順番（小さいほど奥）。" +
            "シーンにある他の表示（ふつうは 0）より奥にして、ミニマップなどが上に見えるようにします")]
        public int m_EmptyAreaSortingOrder = -100;

        [Tooltip("2人以上の時、各区画の上に「P1」などのバッジを出す")]
        public bool m_ShowPlayerBadge = true;

        [Tooltip("レースのタイムを出す")]
        public bool m_ShowTimer = true;

        [Tooltip("区画の境目の線の太さ（画面の基準の大きさでのピクセル）。0で線なし")]
        public float m_DividerWidth = 5f;

        [Tooltip("区画の境目の線の色")]
        public Color m_DividerColor = new Color(0f, 0f, 0f, 0.9f);

        [Header("ポップアップの長さ（秒）")]

        [Tooltip("「LAP 2」などの表示が出ている時間")]
        public float m_LapPopupSeconds = 1.8f;

        [Tooltip("「FINAL LAP!」が出ている時間")]
        public float m_FinalLapPopupSeconds = 2.4f;

        [Tooltip("「FINISH!」が出ている時間（その後、順位とタイムの表示に切り替わります）")]
        public float m_FinishPopupSeconds = 2.4f;

        [Tooltip("「最下位確定」を出し続ける時間。0なら、消さずに出したままにします")]
        public float m_LastPlaceSeconds = 0f;


        // =================================================================
        // ■ 内部：ポップアップの種類
        // =================================================================

        private enum PopupKind
        {
            None,
            Lap,
            FinalLap,
            Finish
        }


        // =================================================================
        // ■ 内部：区画1つぶんの表示
        // =================================================================

        private sealed class PlayerPanel
        {
            public int PlayerNumber;         // プレイヤー番号（1〜4）
            public Rect Viewport;            // 画面の割合での、この区画の範囲
            public bool OnRight;             // 右側の区画か（表示を右寄せにする）
            public Color PlayerColor;        // プレイヤーの色

            public RectTransform Root;       // 区画（カメラの表示範囲に一致）
            public RectTransform Content;    // 拡大率をかける中身
            public float Scale = 1f;         // 今の拡大率

            // 通常の表示
            public CanvasGroup Info;                 // 周回・順位・タイムのまとまり（ゴールで隠す）
            public TextMeshProUGUI LapValue;         // 「1/3」
            public TextMeshProUGUI TimeText;         // タイム
            public TextMeshProUGUI RankText;         // 順位
            public RacePlate RankPlate;              // 順位の札（1人だけで走る時は隠す）
            public int ShownRank;                    // いま出ている順位（0 ＝ まだ無い）
            public float RankPulseStart = -10f;      // 順位が変わった時刻（「ポン」の動き用）

            // 真ん中のポップアップ
            public RectTransform PopupRect;
            public CanvasGroup PopupGroup;
            public TextMeshProUGUI PopupText;
            public TextMeshProUGUI PopupShadow;      // ポップアップの影の文字
            public PopupKind Popup;
            public float PopupStart;
            public float PopupDuration;

            // 最下位確定
            public RectTransform LastRect;
            public CanvasGroup LastGroup;
            public TextMeshProUGUI LastText;
            public TextMeshProUGUI LastSub;
            public bool LastActive;
            public float LastStart;

            // ゴール後
            public CanvasGroup Done;
            public TextMeshProUGUI DoneRank;
            public TextMeshProUGUI DoneTime;
            public TextMeshProUGUI DoneHint;
            public bool Finished;
            public float FinishedAt;
        }


        // =================================================================
        // ■ 内部変数
        // =================================================================

        private RaceFonts m_Fonts;
        private int m_MaxLaps = 3;

        private Canvas m_Canvas;
        private RectTransform m_Root;

        // 空き枠を塗る画像（HUD 本体とは別の Canvas。スタート演出で表示が隠れている間も、塗ったままにする）
        private Canvas m_EmptyAreaCanvas;
        private Image m_EmptyAreaImage;

        private PlayerPanel[] m_Panels = new PlayerPanel[0];

        private Vector2 m_LastRootSize = Vector2.zero;  // 前回レイアウトした時の画面の大きさ

        // 余白・文字の大きさ（区画の中身の座標。拡大率をかける前）
        private const float Margin = 22f;

        // 区画の中身の拡大率の上限（標準は、横 720 × 縦 540 の区画で 1 倍）
        private const float MaxScale = 1.5f;

        // ポップアップ（LAP 2 など）：文字の枠の大きさと、区画の真ん中から上へずらす量
        // （周回の札の下に収まるように、真ん中より少しだけ上に置く）
        private const float PopupMaxWidth = 800f;
        private const float PopupHeight = 180f;
        private const float PopupOffsetY = 26f;

        // ポップアップが出ている間に大きくなる最大の倍率（ゆっくり拡大 6% ＋ 最終周の脈動 5% の少し上）
        private const float PopupGrowMax = 1.12f;

        // 「最下位確定」の帯：大きさと、区画の真ん中から下へずらす量
        // （ポップアップと順位の札の間に収まる高さにしてある）
        private static readonly Vector2 LastBandSize = new Vector2(780f, 124f);
        private const float LastBandOffsetY = -44f;

        // ゴール後の札の幅と、縦の位置（区画の高さに対する割合）
        private const float DonePlateWidth = 720f;
        private const float DoneAnchorY = 0.49f;

        // 帯の中の、日本語の見出しの縦の位置（英語の小さな見出しがある時／無い時）
        private const float LastTextYWithSub = 0.615f;
        private const float LastTextYAlone = 0.5f;

        // 「最下位確定」の札の赤
        private static readonly Color LastPlaceColor = new Color(0.78f, 0.16f, 0.14f, 0.96f);


        // =================================================================
        // ■ 外から見える状態
        // =================================================================

        /// <summary>表示全体の親（RaceStartSequence の m_ExtraHudObjects に入れると、開始と同時に現れる）</summary>
        public GameObject HudObject
        {
            get { return m_Root != null ? m_Root.gameObject : null; }
        }

        /// <summary>表示が出ているか（作っていない時は false）</summary>
        public bool IsVisible
        {
            get { return m_Canvas != null && m_Canvas.enabled; }
        }

        /// <summary>
        /// 表示全体を、出す／隠す（結果発表の間は、レース中の表示を隠す）。
        /// 表示の中身は消さないので、出し直せば元に戻る。Build() で作り直した時は、また出る。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (m_Canvas != null)
            {
                m_Canvas.enabled = visible;
            }
        }

        /// <summary>区画の数</summary>
        public int PanelCount
        {
            get { return m_Panels.Length; }
        }

        /// <summary>作ってあるか</summary>
        public bool IsBuilt
        {
            get { return m_Root != null; }
        }

        /// <summary>空き枠を塗る画像（塗っていなければ null。テスト・確認用）</summary>
        public Image EmptyAreaImage
        {
            get { return m_EmptyAreaImage; }
        }

        /// <summary>区画の「LAP」表示の文字（テスト・確認用）</summary>
        public string LapTextOf(int index)
        {
            PlayerPanel panel = Panel(index);

            return panel != null ? panel.LapValue.text : "";
        }

        /// <summary>区画の順位表示の文字（テスト・確認用）</summary>
        public string RankTextOf(int index)
        {
            PlayerPanel panel = Panel(index);

            return panel != null ? panel.RankText.text : "";
        }

        /// <summary>区画のポップアップの文字（出ていなければ空）</summary>
        public string PopupTextOf(int index)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || panel.Popup == PopupKind.None)
            {
                return "";
            }

            return panel.PopupText.text;
        }

        /// <summary>「最下位確定」が出ているか</summary>
        public bool IsLastPlaceShown(int index)
        {
            PlayerPanel panel = Panel(index);

            return panel != null && panel.LastActive;
        }

        /// <summary>ゴール後の表示（順位とタイム）に入っているか</summary>
        public bool IsFinishShown(int index)
        {
            PlayerPanel panel = Panel(index);

            return panel != null && panel.Finished;
        }

        /// <summary>ゴール後の表示の文字（順位とタイム。テスト・確認用）</summary>
        public string DoneTextOf(int index)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || !panel.Finished)
            {
                return "";
            }

            return panel.DoneRank.text + " " + panel.DoneTime.text;
        }

        /// <summary>区画の拡大率（確認用）</summary>
        public float ScaleOf(int index)
        {
            PlayerPanel panel = Panel(index);

            return panel != null ? panel.Scale : 0f;
        }


        // =================================================================
        // ■ 作る・片付ける
        // =================================================================

        /// <summary>
        /// 表示を作る。すでにあれば、作り直す。
        /// </summary>
        /// <param name="fonts">使うフォント（null なら Inspector の Font から用意する）</param>
        /// <param name="viewports">各プレイヤーの区画（画面の割合で 0〜1。プレイヤーの順）</param>
        /// <param name="playerNumbers">各プレイヤーの番号（1〜4。色とバッジに使う）</param>
        /// <param name="maxLaps">規定の周回数</param>
        public void Build(RaceFonts fonts, Rect[] viewports, int[] playerNumbers, int maxLaps)
        {
            Clear();

            m_Fonts = fonts != null ? fonts : RaceFonts.Resolve(m_Font);
            m_MaxLaps = Mathf.Max(1, maxLaps);

            int count = viewports == null ? 0 : viewports.Length;

            m_Canvas = RaceUiKit.CreateOverlayCanvas("RaceHud Canvas", transform, m_SortingOrder);

            m_Root = RaceUiKit.CreateRect("RaceHud", m_Canvas.transform);

            RaceUiKit.Stretch(m_Root);

            m_Panels = new PlayerPanel[count];

            for (int i = 0; i < count; i++)
            {
                int number =
                    (playerNumbers != null && i < playerNumbers.Length)
                        ? playerNumbers[i]
                        : i + 1;

                m_Panels[i] = BuildPanel(viewports[i], number, count);
            }

            m_LastRootSize = Vector2.zero;

            LayoutPanels();
        }

        /// <summary>
        /// 作った表示を全部消す。
        /// </summary>
        public void Clear()
        {
            if (m_Canvas != null)
            {
                Destroy(m_Canvas.gameObject);
            }

            m_Canvas = null;
            m_Root = null;
            m_Panels = new PlayerPanel[0];

            // 空き枠の塗りつぶしも消す（レースが終わった後に、四角が残らないように）
            RemoveEmptyArea();
        }

        /// <summary>
        /// 空き枠（どのカメラにも映らない部分）を、色で塗りつぶす。
        ///
        /// カメラではなく、画面の上に重ねる UI の画像で塗る。
        /// （Unity 6.2 の URP には、スクリプトで作った画面分割のカメラが UI の上に重なってしまう
        ///   不具合があるため。UI の画像なら、ミニマップや他の表示と順番を決められる）
        ///
        /// 範囲が無い（幅か高さが 0）時と、色が透明の時は、塗らずに消す。
        /// Build() を呼び直すと消えるので、Build() の後で呼ぶこと。
        /// </summary>
        /// <param name="area">塗る範囲（画面全体を 0〜1 とした割合）</param>
        /// <param name="color">塗る色</param>
        public void SetEmptyArea(Rect area, Color color)
        {
            if (area.width <= 0f || area.height <= 0f || color.a <= 0f)
            {
                RemoveEmptyArea();

                return;
            }

            if (m_EmptyAreaCanvas == null || m_EmptyAreaImage == null)
            {
                RemoveEmptyArea();

                m_EmptyAreaCanvas =
                    RaceUiKit.CreateOverlayCanvas(
                        "RaceHud EmptyArea Canvas", transform, m_EmptyAreaSortingOrder);

                m_EmptyAreaImage =
                    RaceUiKit.CreateImage(
                        "EmptyAreaFill", m_EmptyAreaCanvas.transform, null, color, false);
            }

            m_EmptyAreaCanvas.sortingOrder = m_EmptyAreaSortingOrder;

            RaceUiKit.SetNormalizedRect(m_EmptyAreaImage.rectTransform, area);

            m_EmptyAreaImage.color = color;
        }

        /// <summary>空き枠の塗りつぶしを消す。</summary>
        private void RemoveEmptyArea()
        {
            if (m_EmptyAreaCanvas != null)
            {
                Destroy(m_EmptyAreaCanvas.gameObject);
            }

            m_EmptyAreaCanvas = null;
            m_EmptyAreaImage = null;
        }

        private void OnDestroy()
        {
            Clear();
        }


        // =================================================================
        // ■ 表示を変える（RaceGameManager から呼ばれる）
        // =================================================================

        /// <summary>
        /// 「LAP 2/3」の数字を変える（ポップアップは出さない）。
        /// </summary>
        /// <param name="index">プレイヤーの番号（0から。画面の区画の順）</param>
        /// <param name="lap">今走っている周（1から）</param>
        public void SetLap(int index, int lap)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null)
            {
                return;
            }

            panel.LapValue.text =
                Mathf.Clamp(lap, 1, m_MaxLaps) + "<size=60%>/" + m_MaxLaps + "</size>";
        }

        /// <summary>
        /// 新しい周に入った時の大きな表示を出す。最終周なら「FINAL LAP!」、
        /// それ以外は「LAP 2」。数字（LAP 2/3）も同時に変える。
        /// </summary>
        public void AnnounceLap(int index, int lap)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || panel.Finished)
            {
                return;
            }

            SetLap(index, lap);

            if (lap >= m_MaxLaps)
            {
                StartPopup(
                    panel,
                    PopupKind.FinalLap,
                    "FINAL LAP!",
                    RaceStyle.Orange,
                    m_FinalLapPopupSeconds);
            }
            else
            {
                StartPopup(
                    panel,
                    PopupKind.Lap,
                    "LAP " + lap,
                    Color.white,
                    m_LapPopupSeconds);
            }
        }

        /// <summary>
        /// 今の順位を変える。前と違う順位になった時は、数字が「ポン」と動く。
        /// ゴールした後は変わらない。
        /// </summary>
        /// <param name="rank">順位（1から）</param>
        /// <param name="total">走っている人数（1人の時は順位を出さない）</param>
        public void SetRank(int index, int rank, int total)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || panel.Finished)
            {
                return;
            }

            // 1人だけで走る時は、順位に意味が無いので出さない（札ごと隠す）
            panel.RankText.gameObject.SetActive(total > 1);
            panel.RankPlate.Root.gameObject.SetActive(total > 1);

            if (rank == panel.ShownRank)
            {
                return;
            }

            bool firstTime = panel.ShownRank == 0;

            panel.ShownRank = rank;

            panel.RankText.text = RankString(rank);
            panel.RankText.color = RaceColors.Rank(rank);

            if (!firstTime)
            {
                panel.RankPulseStart = Time.unscaledTime;
            }
        }

        /// <summary>
        /// レースのタイムを変える（全員の区画に同じ時間を出す）。
        /// </summary>
        public void SetTime(float seconds)
        {
            if (!m_ShowTimer)
            {
                return;
            }

            string text = RaceTimeFormat.FormatShort(seconds);

            for (int i = 0; i < m_Panels.Length; i++)
            {
                m_Panels[i].TimeText.text = text;
            }
        }

        /// <summary>
        /// ゴールした。「FINISH!」を出して、そのあと順位とタイムの表示に切り替える。
        /// </summary>
        /// <param name="rank">ゴール時点の順位（1から）</param>
        /// <param name="time">ゴールタイム（秒）</param>
        public void ShowFinish(int index, int rank, float time)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || panel.Finished)
            {
                return;
            }

            panel.Finished = true;
            panel.FinishedAt = Time.unscaledTime;

            panel.DoneRank.text = RankString(rank);
            panel.DoneRank.color = RaceColors.Rank(rank);
            panel.DoneTime.text = RaceTimeFormat.Format(time);

            // 他の人を待つ間の案内
            panel.DoneHint.text = m_Fonts.Pick("Waiting for the others...", "ほかの人を待っています...");

            StartPopup(
                panel,
                PopupKind.Finish,
                "FINISH!",
                RaceColors.Gold,
                m_FinishPopupSeconds);
        }

        /// <summary>
        /// 最下位が確定した人に「最下位確定」を出す。
        /// 日本語のフォントが無い時は「LAST PLACE」と出す。
        /// </summary>
        public void ShowLastPlace(int index)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || panel.LastActive || panel.Finished)
            {
                return;
            }

            panel.LastActive = true;
            panel.LastStart = Time.unscaledTime;

            panel.LastText.text = m_Fonts.Pick("LAST PLACE", "最下位確定");
            panel.LastSub.text = m_Fonts.HasJapanese ? "LAST PLACE" : "";

            // 英語の小さな見出しが無い時は、大きな見出しを帯の真ん中に置く
            float textY = m_Fonts.HasJapanese ? LastTextYWithSub : LastTextYAlone;

            panel.LastText.rectTransform.anchorMin = new Vector2(0.5f, textY);
            panel.LastText.rectTransform.anchorMax = new Vector2(0.5f, textY);
            panel.LastText.rectTransform.anchoredPosition = Vector2.zero;

            panel.LastRect.gameObject.SetActive(true);
        }

        /// <summary>
        /// 「最下位確定」を消す。
        /// </summary>
        public void HideLastPlace(int index)
        {
            PlayerPanel panel = Panel(index);

            if (panel == null || !panel.LastActive)
            {
                return;
            }

            panel.LastActive = false;
            panel.LastRect.gameObject.SetActive(false);
        }


        // =================================================================
        // ■ 毎フレーム
        // =================================================================

        private void LateUpdate()
        {
            if (m_Root == null)
            {
                return;
            }

            // 画面の大きさが変わったら、レイアウトし直す
            Vector2 size = m_Root.rect.size;

            if ((size - m_LastRootSize).sqrMagnitude > 0.01f)
            {
                LayoutPanels();
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < m_Panels.Length; i++)
            {
                UpdatePanel(m_Panels[i], now);
            }
        }

        /// <summary>
        /// 区画1つぶんの動きを進める（ポップアップ・順位の「ポン」・ゴール後の切り替え）。
        /// </summary>
        private void UpdatePanel(PlayerPanel panel, float now)
        {
            // ---- 順位の「ポン」 ----
            float pulseT = (now - panel.RankPulseStart) / 0.35f;

            float rankScale = pulseT < 1f ? 1f + 0.35f * RaceEase.Pulse(pulseT) : 1f;

            panel.RankText.rectTransform.localScale = new Vector3(rankScale, rankScale, 1f);

            // ---- 真ん中のポップアップ ----
            UpdatePopup(panel, now);

            // ---- ゴール後：通常の表示を薄くして、順位とタイムの表示を出す ----
            if (panel.Finished)
            {
                float since = now - panel.FinishedAt;

                // FINISH! が出ている間に、通常の表示を消す
                panel.Info.alpha = 1f - Mathf.Clamp01(since / 0.4f);

                // FINISH! が終わったら、順位とタイムを出す
                float doneT = (since - m_FinishPopupSeconds) / 0.4f;

                panel.Done.alpha = Mathf.Clamp01(doneT);
            }

            // ---- 最下位確定 ----
            if (panel.LastActive)
            {
                UpdateLastPlace(panel, now);
            }
        }

        /// <summary>
        /// ポップアップの大きさと透明度を、経過時間に合わせる。
        ///   はじめ：大きい状態から、ポンと定位置に来る（0.3秒）
        ///   途中：ゆっくり少しだけ大きくなりながら表示
        ///   終わり：最後の0.4秒で消える（FINISH! だけは、ゴール後の表示に切り替わるまで消える）
        /// </summary>
        private void UpdatePopup(PlayerPanel panel, float now)
        {
            if (panel.Popup == PopupKind.None)
            {
                return;
            }

            float t = now - panel.PopupStart;
            float duration = panel.PopupDuration;

            if (t >= duration)
            {
                panel.Popup = PopupKind.None;
                panel.PopupGroup.alpha = 0f;
                panel.PopupRect.gameObject.SetActive(false);

                return;
            }

            float appear = RaceEase.OutBack(t / 0.30f);

            float startScale = panel.Popup == PopupKind.Lap ? 1.6f : 2.0f;

            float scale = Mathf.Lerp(startScale, 1f, appear);

            // 表示中は、ゆっくり大きくなる
            scale *= 1f + 0.06f * Mathf.Clamp01(t / duration);

            // 最終周は、少しだけ脈打つ
            if (panel.Popup == PopupKind.FinalLap)
            {
                scale *= 1f + 0.05f * Mathf.Sin(t * 9f);
            }

            panel.PopupRect.localScale = new Vector3(scale, scale, 1f);

            float fadeIn = Mathf.Clamp01(t / 0.12f);
            float fadeOut = Mathf.Clamp01((duration - t) / 0.4f);

            panel.PopupGroup.alpha = Mathf.Min(fadeIn, fadeOut);
        }

        /// <summary>
        /// 「最下位確定」の帯を、右から滑り込ませて表示する。
        /// </summary>
        private void UpdateLastPlace(PlayerPanel panel, float now)
        {
            float t = now - panel.LastStart;

            // 時間で消す設定の時
            if (m_LastPlaceSeconds > 0f && t >= m_LastPlaceSeconds)
            {
                HideLastPlace(System.Array.IndexOf(m_Panels, panel));

                return;
            }

            float slide = 1f - RaceEase.OutCubic(t / 0.45f);

            // 帯が右から入ってくる（区画の幅の半分ぶん）
            float width = panel.Content.rect.width;

            panel.LastRect.anchoredPosition = new Vector2(slide * width * 0.6f, LastBandOffsetY);

            float alpha = Mathf.Clamp01(t / 0.15f);

            // 消える設定の時は、最後の0.4秒で薄くなる
            if (m_LastPlaceSeconds > 0f)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01((m_LastPlaceSeconds - t) / 0.4f));
            }

            // ポップアップ（LAP 2 など）が重なって出ている間は、帯を薄くして、ポップアップを読みやすくする
            if (panel.Popup != PopupKind.None)
            {
                alpha *= 1f - 0.65f * panel.PopupGroup.alpha;
            }

            panel.LastGroup.alpha = alpha;
        }

        private void StartPopup(
            PlayerPanel panel,
            PopupKind kind,
            string text,
            Color color,
            float seconds)
        {
            panel.Popup = kind;
            panel.PopupStart = Time.unscaledTime;
            panel.PopupDuration = Mathf.Max(0.5f, seconds);

            panel.PopupText.text = text;
            panel.PopupText.color = color;
            panel.PopupShadow.text = text;

            panel.PopupGroup.alpha = 0f;
            panel.PopupRect.gameObject.SetActive(true);
        }


        // =================================================================
        // ■ 区画を作る
        // =================================================================

        /// <summary>
        /// 区画1つぶんの表示を作る。
        /// 見た目は、タンクモードに寄せてある：青緑の斜めの札・プレイヤー色の札・青緑の影つきの大きな文字。
        /// </summary>
        /// <param name="viewport">画面の割合での範囲</param>
        /// <param name="playerNumber">プレイヤー番号</param>
        /// <param name="panelCount">区画の数（1人の時は、バッジなどを出さない）</param>
        private PlayerPanel BuildPanel(Rect viewport, int playerNumber, int panelCount)
        {
            PlayerPanel panel = new PlayerPanel();

            panel.PlayerNumber = playerNumber;
            panel.Viewport = viewport;
            panel.PlayerColor = RaceColors.Player(playerNumber);

            // 画面の右半分にある区画は、表示を右寄せにする（真ん中にあるミニマップと重ならないように）
            panel.OnRight = viewport.center.x > 0.5f + 0.01f && viewport.width < 0.9f;

            TMP_FontAsset font = m_Fonts.Main;
            TMP_FontAsset fontJa = m_Fonts.ForJapanese;

            // ---- 区画の枠 ----
            panel.Root = RaceUiKit.CreateRect("Panel P" + playerNumber, m_Root);

            RaceUiKit.SetNormalizedRect(panel.Root, viewport);

            panel.Content = RaceUiKit.CreateRect("Content", panel.Root);

            RaceUiKit.Place(
                panel.Content,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(960f, 540f));

            // 左右の寄せ方
            bool right = panel.OnRight;

            Vector2 cornerTop = new Vector2(right ? 1f : 0f, 1f);
            Vector2 cornerBottom = new Vector2(right ? 1f : 0f, 0f);
            Vector2 pivotTop = cornerTop;
            Vector2 pivotBottom = cornerBottom;

            float sign = right ? -1f : 1f;

            TextAlignmentOptions alignTop =
                right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;

            // 文字の縁取りは、濃い青緑
            Color outline = RaceStyle.DeepTeal;

            // 札の色：青緑（少し透けて、後ろのコースが見える）／札の影
            Color plateColor = RaceColors.WithAlpha(RaceStyle.Teal, 0.84f);
            Color plateShadow = RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.45f);

            // ---- 通常の表示（周回・タイム・順位） ----
            RectTransform info = RaceUiKit.CreateRect("Info", panel.Content);

            RaceUiKit.Stretch(info);

            panel.Info = info.gameObject.AddComponent<CanvasGroup>();

            // 左上（または右上）：LAP とタイム（青緑の札の上）
            Vector2 lapSize = new Vector2(262f, 152f);

            RacePlate lapPlate =
                RaceUiKit.CreatePlate(
                    "LapBlock",
                    info,
                    lapSize,
                    16f,
                    16f,
                    plateColor,
                    plateShadow,
                    6f,
                    0f);

            RectTransform lapBlock = lapPlate.Root;

            RaceUiKit.Place(
                lapBlock,
                cornerTop,
                pivotTop,
                new Vector2(sign * Margin, -Margin),
                lapSize);

            // 札の中の文字は、札の端から少し内側に置く
            float inset = 26f;

            TextMeshProUGUI lapLabel =
                RaceUiKit.CreateText(
                    "LapLabel",
                    lapBlock,
                    font,
                    26f,
                    new Color(1f, 1f, 1f, 0.92f),
                    alignTop,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                lapLabel.rectTransform,
                cornerTop,
                cornerTop,
                new Vector2(sign * inset, -12f),
                new Vector2(210f, 34f));

            lapLabel.text = "LAP";

            RaceUiKit.SetOutline(lapLabel, 0.18f, outline);

            panel.LapValue =
                RaceUiKit.CreateText(
                    "LapValue",
                    lapBlock,
                    font,
                    68f,
                    Color.white,
                    alignTop,
                    FontStyles.Bold | FontStyles.Italic);

            panel.LapValue.richText = true;

            RaceUiKit.Place(
                panel.LapValue.rectTransform,
                cornerTop,
                cornerTop,
                new Vector2(sign * inset, -38f),
                new Vector2(210f, 84f));

            RaceUiKit.SetOutline(panel.LapValue, 0.18f, outline);

            panel.TimeText =
                RaceUiKit.CreateText(
                    "Time",
                    lapBlock,
                    font,
                    30f,
                    Color.white,
                    alignTop,
                    FontStyles.Bold);

            RaceUiKit.Place(
                panel.TimeText.rectTransform,
                cornerTop,
                cornerTop,
                new Vector2(sign * inset, -116f),
                new Vector2(210f, 34f));

            RaceUiKit.SetOutline(panel.TimeText, 0.18f, outline);

            panel.TimeText.text = RaceTimeFormat.FormatShort(0f);
            panel.TimeText.gameObject.SetActive(m_ShowTimer);

            // 左下（または右下）：順位（青緑の札の上。1人だけで走る時は、札ごと隠す）
            Vector2 rankSize = new Vector2(232f, 144f);

            panel.RankPlate =
                RaceUiKit.CreatePlate(
                    "RankPlate",
                    info,
                    rankSize,
                    16f,
                    16f,
                    plateColor,
                    plateShadow,
                    6f,
                    0f);

            RaceUiKit.Place(
                panel.RankPlate.Root,
                cornerBottom,
                pivotBottom,
                new Vector2(sign * Margin, Margin * 0.5f),
                rankSize);

            panel.RankText =
                RaceUiKit.CreateText(
                    "Rank",
                    panel.RankPlate.Root,
                    font,
                    112f,
                    RaceColors.Plain,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            panel.RankText.richText = true;

            RaceUiKit.Stretch(panel.RankText.rectTransform);

            RaceUiKit.SetOutline(panel.RankText, 0.22f, outline);

            panel.RankText.text = "";
            panel.RankText.gameObject.SetActive(false);
            panel.RankPlate.Root.gameObject.SetActive(false);

            // 周回の初期表示
            SetLapDirect(panel, 1);

            // 上の真ん中：プレイヤーのバッジ（プレイヤーの色の札）
            if (m_ShowPlayerBadge && panelCount >= 2)
            {
                Vector2 badgeSize = new Vector2(84f, 44f);

                RacePlate badge =
                    RaceUiKit.CreatePlate(
                        "Badge",
                        info,
                        badgeSize,
                        9f,
                        9f,
                        panel.PlayerColor,
                        plateShadow,
                        4f,
                        0f);

                RaceUiKit.Place(
                    badge.Root,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -Margin * 0.6f),
                    badgeSize);

                // 黄色・緑など明るいバッジの上では、白い文字が読みにくいので、黒っぽい文字にする
                TextMeshProUGUI badgeText =
                    RaceUiKit.CreateText(
                        "BadgeText",
                        badge.Root,
                        font,
                        28f,
                        RaceColors.TextOn(panel.PlayerColor),
                        TextAlignmentOptions.Center,
                        FontStyles.Bold | FontStyles.Italic);

                RaceUiKit.Stretch(badgeText.rectTransform);

                badgeText.text = RaceColors.PlayerName(playerNumber);

                if (!RaceColors.UsesDarkText(panel.PlayerColor))
                {
                    RaceUiKit.SetOutline(badgeText, 0.2f, new Color(0f, 0f, 0f, 0.8f));
                }
            }

            // ---- 最下位確定の帯（赤い斜めの札） ----
            // ポップアップより先に作る（＝奥に置く）。同時に出た時は、ポップアップが手前に見える。
            panel.LastRect = RaceUiKit.CreateRect("LastPlace", panel.Content);

            RaceUiKit.Place(
                panel.LastRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, LastBandOffsetY),
                LastBandSize);

            panel.LastGroup = panel.LastRect.gameObject.AddComponent<CanvasGroup>();

            RacePlate lastPlate =
                RaceUiKit.CreatePlate(
                    "Band",
                    panel.LastRect,
                    LastBandSize,
                    24f,
                    24f,
                    LastPlaceColor,
                    plateShadow,
                    10f,
                    0f);

            RaceUiKit.Place(lastPlate.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, LastBandSize);

            // 日本語の見出し（上）と、英語の小さな見出し（下）。重ならないように、上下に分けて置く
            panel.LastText =
                RaceUiKit.CreateText(
                    "LastText",
                    panel.LastRect,
                    fontJa,
                    70f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                panel.LastText.rectTransform,
                new Vector2(0.5f, LastTextYWithSub),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(700f, 80f));

            RaceUiKit.FitText(panel.LastText, 36f, 70f);

            RaceUiKit.SetOutline(panel.LastText, 0.2f, new Color(0.30f, 0.03f, 0.02f, 0.9f));

            panel.LastSub =
                RaceUiKit.CreateText(
                    "LastSub",
                    panel.LastRect,
                    font,
                    26f,
                    new Color(1f, 1f, 1f, 0.9f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                panel.LastSub.rectTransform,
                new Vector2(0.5f, 0.15f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(700f, 30f));

            panel.LastRect.gameObject.SetActive(false);

            // ---- 真ん中のポップアップ（LAP 2／FINAL LAP!／FINISH!） ----
            panel.PopupRect = RaceUiKit.CreateRect("Popup", panel.Content);

            // 枠の幅は、区画の幅に合わせて LayoutPanels が決め直す（狭い区画で、文字がはみ出さないように）
            RaceUiKit.Place(
                panel.PopupRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, PopupOffsetY),
                new Vector2(PopupMaxWidth, PopupHeight));

            panel.PopupGroup = panel.PopupRect.gameObject.AddComponent<CanvasGroup>();

            // 影（少し右下にずらした青緑の文字）→ 本体 の順に重ねる
            panel.PopupShadow =
                RaceUiKit.CreateText(
                    "PopupShadow",
                    panel.PopupRect,
                    font,
                    116f,
                    RaceStyle.ShadowTeal,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Stretch(panel.PopupShadow.rectTransform);

            panel.PopupShadow.rectTransform.offsetMin = new Vector2(7f, -7f);
            panel.PopupShadow.rectTransform.offsetMax = new Vector2(7f, -7f);

            RaceUiKit.FitText(panel.PopupShadow, 50f, 116f);

            panel.PopupText =
                RaceUiKit.CreateText(
                    "PopupText",
                    panel.PopupRect,
                    font,
                    116f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Stretch(panel.PopupText.rectTransform);

            RaceUiKit.FitText(panel.PopupText, 50f, 116f);

            RaceUiKit.SetOutline(panel.PopupText, 0.2f, outline);

            panel.PopupRect.gameObject.SetActive(false);

            // ---- ゴール後の表示（順位・タイム・案内。青緑の札の上） ----
            RectTransform done = RaceUiKit.CreateRect("Done", panel.Content);

            RaceUiKit.Stretch(done);

            panel.Done = done.gameObject.AddComponent<CanvasGroup>();
            panel.Done.alpha = 0f;

            Vector2 doneSize = new Vector2(DonePlateWidth, 372f);

            RacePlate donePlate =
                RaceUiKit.CreatePlate(
                    "DonePlate",
                    done,
                    doneSize,
                    26f,
                    26f,
                    RaceColors.WithAlpha(RaceStyle.Teal, 0.9f),
                    plateShadow,
                    12f,
                    0f);

            RaceUiKit.Place(donePlate.Root, new Vector2(0.5f, DoneAnchorY), new Vector2(0.5f, 0.5f), Vector2.zero, doneSize);

            panel.DoneRank =
                RaceUiKit.CreateText(
                    "DoneRank",
                    done,
                    font,
                    176f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            panel.DoneRank.richText = true;

            // 文字は、札の真ん中を基準に（区画の高さが変わっても札の中に収まるように）置く
            RaceUiKit.Place(
                panel.DoneRank.rectTransform,
                new Vector2(0.5f, DoneAnchorY),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 59f),
                new Vector2(660f, 210f));

            RaceUiKit.SetOutline(panel.DoneRank, 0.24f, outline);

            panel.DoneTime =
                RaceUiKit.CreateText(
                    "DoneTime",
                    done,
                    font,
                    50f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                panel.DoneTime.rectTransform,
                new Vector2(0.5f, DoneAnchorY),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f),
                new Vector2(660f, 64f));

            RaceUiKit.SetOutline(panel.DoneTime, 0.2f, outline);

            panel.DoneHint =
                RaceUiKit.CreateText(
                    "DoneHint",
                    done,
                    fontJa,
                    26f,
                    new Color(1f, 1f, 1f, 0.92f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Place(
                panel.DoneHint.rectTransform,
                new Vector2(0.5f, DoneAnchorY),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -146f),
                new Vector2(660f, 38f));

            RaceUiKit.FitText(panel.DoneHint, 16f, 26f);

            RaceUiKit.SetOutline(panel.DoneHint, 0.18f, outline);

            // ---- 区画の境目の線（画面の端に接していない辺だけ） ----
            BuildDividers(panel);

            return panel;
        }

        /// <summary>
        /// 区画の境目の線を作る。画面の端に接している辺には、線を引かない。
        /// </summary>
        private void BuildDividers(PlayerPanel panel)
        {
            if (m_DividerWidth <= 0f || m_Panels == null)
            {
                return;
            }

            Rect vp = panel.Viewport;

            const float epsilon = 0.001f;

            // 左・右・下・上の辺が、画面の内側にあるか
            bool left = vp.xMin > epsilon;
            bool rightEdge = vp.xMax < 1f - epsilon;
            bool bottom = vp.yMin > epsilon;
            bool top = vp.yMax < 1f - epsilon;

            // 隣り合う2つの区画が、それぞれ「半分の太さ」の線を引くので、合わせて m_DividerWidth になる
            float half = m_DividerWidth * 0.5f;

            if (left)
            {
                AddDivider(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), half, true, 1f);
            }

            if (rightEdge)
            {
                AddDivider(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), half, true, -1f);
            }

            if (bottom)
            {
                AddDivider(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), half, false, 1f);
            }

            if (top)
            {
                AddDivider(panel, new Vector2(0f, 1f), new Vector2(1f, 1f), half, false, -1f);
            }
        }

        // 線を1本作る（区画の辺に沿って、辺の内側に置く）
        // thickness ＝ 線の太さ／inward ＝ 区画の内側へ向かう向き（+1 か -1）
        private void AddDivider(
            PlayerPanel panel,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float thickness,
            bool vertical,
            float inward)
        {
            Image line =
                RaceUiKit.CreateImage("Divider", panel.Root, null, m_DividerColor, false);

            line.rectTransform.anchorMin = anchorMin;
            line.rectTransform.anchorMax = anchorMax;
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 辺から内側へ、太さの半分だけずらす（線の端が、ちょうど辺に揃う）
            line.rectTransform.anchoredPosition =
                vertical
                    ? new Vector2(inward * thickness * 0.5f, 0f)
                    : new Vector2(0f, inward * thickness * 0.5f);

            // 縦線は幅だけ、横線は高さだけを決める（長さは anchor で区画いっぱい）
            line.rectTransform.sizeDelta =
                vertical
                    ? new Vector2(thickness, 0f)
                    : new Vector2(0f, thickness);
        }

        /// <summary>
        /// 画面の大きさに合わせて、区画の中身の大きさ・拡大率を決める。
        /// </summary>
        private void LayoutPanels()
        {
            if (m_Root == null)
            {
                return;
            }

            Vector2 rootSize = m_Root.rect.size;

            m_LastRootSize = rootSize;

            for (int i = 0; i < m_Panels.Length; i++)
            {
                PlayerPanel panel = m_Panels[i];

                // この区画の、画面の基準の大きさでのピクセル数
                Vector2 areaSize =
                    new Vector2(
                        rootSize.x * panel.Viewport.width,
                        rootSize.y * panel.Viewport.height);

                // 全画面（1人）でも、大きくなりすぎないように、拡大の上限を決める
                float scale = Mathf.Min(MaxScale, RaceUiKit.FitScale(areaSize));

                panel.Scale = scale;

                panel.Content.localScale = new Vector3(scale, scale, 1f);
                panel.Content.sizeDelta = new Vector2(areaSize.x / scale, areaSize.y / scale);

                FitWideParts(panel, areaSize.x / scale);
            }
        }


        /// <summary>
        /// 区画が狭い時（2人の左右分割など）に、横に長い部品が区画からはみ出さないようにする。
        ///   ポップアップ：文字の枠を狭くする（文字は自動で小さくなる）
        ///   最下位確定の帯・ゴール後の札：全体を小さくする
        /// </summary>
        /// <param name="contentWidth">区画の中身の幅（拡大率をかける前）</param>
        private void FitWideParts(PlayerPanel panel, float contentWidth)
        {
            float room = Mathf.Max(100f, contentWidth - Margin * 2f);

            // ポップアップは、出ている間に最大で 1.12 倍まで大きくなるので、その分を見込んで狭くする
            panel.PopupRect.sizeDelta = new Vector2(Mathf.Min(PopupMaxWidth, room / PopupGrowMax), PopupHeight);

            float bandFit = Mathf.Min(1f, room / LastBandSize.x);

            panel.LastRect.localScale = new Vector3(bandFit, bandFit, 1f);

            float doneFit = Mathf.Min(1f, room / DonePlateWidth);

            panel.Done.transform.localScale = new Vector3(doneFit, doneFit, 1f);
        }


        // =================================================================
        // ■ 補助
        // =================================================================

        private PlayerPanel Panel(int index)
        {
            if (index < 0 || index >= m_Panels.Length)
            {
                return null;
            }

            return m_Panels[index];
        }

        // 周回の数字を、アニメーションなしで書き換える
        private void SetLapDirect(PlayerPanel panel, int lap)
        {
            panel.LapValue.text =
                Mathf.Clamp(lap, 1, m_MaxLaps) + "<size=60%>/" + m_MaxLaps + "</size>";
        }

        // 順位の文字（「1st」。語尾は小さく）
        private static string RankString(int rank)
        {
            return rank + "<size=45%>" + RaceOrdinal.Suffix(rank) + "</size>";
        }
    }
}