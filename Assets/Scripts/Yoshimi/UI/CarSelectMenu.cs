using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ 車種選択画面（最大4人）
    // =====================================================================
    //
    // タンクモードの参加画面と同じ作りの、車の種類を選ぶ画面。
    // 画面に4つの枠（P1〜P4）が並び、押したボタンの入力装置ごとに、その場で参加できる。
    //
    //   ① 参加      … 決定ボタンを押すと、空いている一番左の枠に入る
    //   ② 車種を選ぶ … 左右ボタンで、車種を切り替える（性能の5項目が棒で見える）
    //   ③ 準備完了  … 決定ボタンで「READY!」。取り消しボタンで準備を解除（もう一度押すと参加をやめる）
    //   ④ スタート  … 参加した全員が READY になると、少し待ってから自動でレースが始まる
    //
    //   入力装置        決定      取り消し      左右
    //   WASD            Space     Q             A / D
    //   矢印キー        Enter     Backspace     ← / →
    //   ゲームパッド    A         B             十字キー・左スティック
    //
    //   Esc キー … いつでもタイトルへ戻る。
    //   誰も参加していない時に、取り消しボタンを押しても、タイトルへ戻る。
    //
    // 車の見た目
    //   ・できれば、実際の車の形を3Dで回転させて見せる（車の部品のコピーを、遠くの場所に置いて撮影する）。
    //     重い車ほど、少し大きく見える（レースでの車体の大きさと同じ割合）。
    //   ・3Dにできない時（車体の形が見つからない時など）は、プレイヤーの色をつけた2Dの車のアイコンを出す。
    //   ・車体の色は、レース中の車と同じ決め方（RaceGameManager.cs の CarPaint）で塗る。
    //     どのマテリアルを塗るかは、RaceGameManager の Paint Material Names・Auto Detect Paint の設定に従う。
    //
    // 画面に出る文言は、このファイルの「画面の文言」にまとめてある（日本語が出せない時は、英語になる）。
    //
    // 車種ごとの性能の中身は CarSpec.cs（CarTypeCatalog）にある。
    // 参加・切り替え・準備のルールは CarSelectModel（RaceSession.cs）にある。
    // このスクリプトは、その見た目と、ボタンの読み取りだけを受け持つ。
    //
    // 決まった内容は、OnStart で RaceGameManager に渡す。
    //
    // =====================================================================

    /// <summary>
    /// 車種選択画面。
    /// </summary>
    public class CarSelectMenu : MonoBehaviour
    {
        // =================================================================
        // ■ 設定
        // =================================================================

        [Header("表示")]

        [Tooltip("数字や英語の見出しに使うフォント（空ならTextMeshProの標準）。" +
            "RaceGameManager から開く時は、そちらのフォントが使われます")]
        public TMP_FontAsset m_Font;

        [Tooltip("画面の重なり順（大きいほど手前）。レース中の表示より手前、結果発表より奥")]
        public int m_SortingOrder = 200;

        [Tooltip("背景の濃さ（1で後ろが見えない。小さくすると、後ろのコースが透けて見えます）")]
        [Range(0f, 1f)]
        public float m_BackdropAlpha = 0.94f;


        [Header("車のプレビュー")]

        [Tooltip("ONなら、実際の車の形を3Dで回して見せる。OFFなら、2Dの車のアイコンを出す。\n" +
            "3Dにできない時（車体の形が見つからない時）は、自動で2Dのアイコンになります")]
        public bool m_Use3DPreview = true;

        [Tooltip("3Dプレビューの車が回る速さ（1秒あたりの度数）")]
        public float m_PreviewTurnSpeed = 38f;

        [Tooltip("3Dプレビューを見下ろす角度（度）。大きいほど上から見る")]
        public float m_PreviewPitch = 14f;

        [Tooltip("3Dプレビューの画角（度）。小さいほど望遠で、車の形がゆがまない")]
        public float m_PreviewFieldOfView = 24f;

        [Tooltip("3Dプレビューを置く場所。レースのコースから十分に離れた所にします（地面より下の遠く）")]
        public Vector3 m_PreviewStageOrigin = new Vector3(0f, -4000f, 0f);


        [Header("操作")]

        [Tooltip("全員が READY になってから、レースが始まるまでの時間（秒）。" +
            "この間に取り消しボタンを押すと、スタートを止められます")]
        public float m_AutoStartDelay = 1.4f;

        [Tooltip("画面を開いてから、この秒数のあいだは、ボタンを受け付けない" +
            "（結果発表で押した決定ボタンが、そのまま「参加」になるのを防ぐ）")]
        public float m_InputGuard = 0.35f;

        [Tooltip("Esc キーで、タイトルへ戻れるようにする")]
        public bool m_EscapeGoesBack = true;

        /// <summary>
        /// 車体の大きさの差の大きさ。0＝全車同じ大きさ／1＝標準／2＝差が2倍。
        /// レース中の車体の大きさと同じ割合にするため、RaceGameManager が設定する。
        /// </summary>
        [HideInInspector]
        public float m_SizeInfluence = 1f;


        // =================================================================
        // ■ 画面の文言（ここを書き換えると、表示が変わる）
        // =================================================================
        //
        // 日本語が出せる時は「Ja」、出せない時は「En」が表示される（RaceFonts.Pick）。
        // キーの名前（SPACE・A・Q など）は、入力装置に合わせて自動で入るので、ここには書かない。
        // 日本語は、ゲームの画面らしい短い言い方にそろえてある。

        // 画面の下の案内：誰も参加していない時
        private const string TextJoinEn = "PRESS YOUR BUTTON TO JOIN";
        private const string TextJoinJa = "ボタンを押して参加";

        // 画面の下の案内：参加した人の準備を待っている時（このあとに「0 / 3」のような人数が付く）
        private const string TextWaitingEn = "WAITING";
        private const string TextWaitingJa = "待機中";

        // 画面の下の案内：全員の準備ができて、スタートを待っている時
        private const string TextAllReadyEn = "ALL READY!   STARTING...";
        private const string TextAllReadyJa = "全員 準備OK！　まもなくスタート";

        // 画面の左下の案内（前に Esc キーの名前が付く）
        private const string TextBackToTitleEn = "BACK TO TITLE";
        private const string TextBackToTitleJa = "タイトルへ戻る";

        // 参加した枠の下の、操作の案内（前にキーの名前が付く）
        private const string TextChangeEn = "CHANGE";
        private const string TextChangeJa = "車種を選ぶ";
        private const string TextOkEn = "OK";
        private const string TextOkJa = "OK";
        private const string TextBackEn = "BACK";
        private const string TextBackJa = "戻る";

        // READY になった枠の下の、取り消しの案内（前に取り消しキーの名前が付く）
        private const string TextCancelEn = "CANCEL";
        private const string TextCancelJa = "取り消し";

        // 参加前の枠の、参加ボタンの案内に出す「ゲームパッド」（「SPACE / ENTER / ゲームパッド A」の形）
        private const string TextGamepadEn = "GAMEPAD";
        private const string TextGamepadJa = "ゲームパッド";


        // =================================================================
        // ■ 外へ知らせる
        // =================================================================

        /// <summary>
        /// 全員が READY になって、スタートする時に呼ばれる
        /// （引数は、参加した人たち。プレイヤー番号は枠の番号のまま）。
        /// </summary>
        public System.Action<RacePlayerEntry[]> OnStart;

        /// <summary>タイトルへ戻る操作が行われた時に呼ばれる</summary>
        public System.Action OnBack;


        // =================================================================
        // ■ 寸法・色（Canvas の基準 1920 × 1080 でのピクセル）
        // =================================================================
        //
        // 見た目は、タンクモードの参加画面に寄せてある：
        // 傾いた薄い水色の板（カード）・青緑の吹き出し・薄紫の矢印・オレンジのスタートボタン。
        // 色は RaceStyle（RaceUiKit.cs）にまとめてある。
        //
        // パネルの中の位置は「パネルの中心から見た位置（上がプラス）」で書いてある。

        private const int PanelWidth = 420;
        private const int PanelHeight = 740;
        private const float PanelGap = 40f;

        // 画面の中心から見た、パネルの中心の高さ（下がマイナス）
        private const float PanelCenterY = -30f;

        // 板の角の丸さ・影の落ちる大きさ
        private const float PanelRadius = 18f;
        private const float PanelShadowDepth = 22f;

        // 左上の「P1」の札・右上の入力装置の札
        private static readonly Vector2 TagSize = new Vector2(168f, 58f);
        private static readonly Vector2 TagCenter = new Vector2(-112f, 325f);
        private static readonly Vector2 DeviceTagSize = new Vector2(190f, 40f);
        private static readonly Vector2 DeviceTagCenter = new Vector2(100f, 327f);

        // 車の表示（3Dプレビュー・2Dアイコン・足もとのドット）
        private const float PreviewWidth = 384f;
        private const float PreviewHeight = 214f;
        private const float PreviewCenterY = 183f;

        // 左右の矢印（板の上の辺のすぐ上）
        private const float ArrowCenterY = 394f;
        private const float ArrowCenterX = 122f;

        // 何番目の車種かを示す点
        private const float DotsCenterY = 60f;

        // 性能の吹き出し（本体の大きさと位置）
        private static readonly Vector2 BubbleSize = new Vector2(340f, 306f);
        private static readonly Vector2 BubbleCenter = new Vector2(-26f, -109f);

        // 下の案内の帯・準備完了の帯
        private static readonly Vector2 HintSize = new Vector2(392f, 70f);
        private static readonly Vector2 HintCenter = new Vector2(0f, -311f);

        // 画面の下のスタートボタン風の帯（画面の下の端からの位置）
        private static readonly Vector2 StatusSize = new Vector2(780f, 84f);
        private const float StatusCenterY = 66f;

        // 3Dプレビューの画像の大きさ（ピクセル）
        private const int PreviewTextureWidth = 512;
        private const int PreviewTextureHeight = 285;

        // 板の影（参加前は薄く、参加後は濃い）
        private static readonly Color ShadowOff = new Color(0.07f, 0.40f, 0.50f, 0.40f);
        private static readonly Color ShadowOn = new Color(0.07f, 0.40f, 0.50f, 0.95f);

        // 3Dプレビューの背景色（板の面の色に合わせてある。透明にできない環境でも目立たないように）
        private static readonly Color PreviewBackground = new Color(0.61f, 0.69f, 0.73f, 0f);

        // 画面の下の帯の右端の車のシルエット（スタートできる時／まだの時）
        private static readonly Color StatusIconOn = new Color(0.80f, 0.46f, 0f, 0.65f);
        private static readonly Color StatusIconOff = new Color(0.32f, 0.20f, 0.04f, 0.55f);

        // 吹き出しの中の、性能の棒（まだ色がついていない部分）と、車種の点
        private static readonly Color DimPip = new Color(1f, 1f, 1f, 0.20f);
        private static readonly Color DotOff = new Color(0.02f, 0.18f, 0.25f, 0.28f);
        private static readonly Color DotOn = new Color(0.02f, 0.18f, 0.25f, 1f);

        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        // 画面の基準（1920 × 1080）の高さの半分。見出し・下の帯は、画面の真ん中を基準に置く
        // （画面の縦横比が変わっても、板との位置関係が変わらないように）
        private const float HalfHeight = 540f;


        // =================================================================
        // ■ 内部：枠1つぶんの表示
        // =================================================================

        private sealed class SlotView
        {
            public int Index;                      // 枠の番号（0〜3）
            public Color PlayerColor;

            public RectTransform Root;
            public Image Outline;                  // 準備完了の時に光る縁（板の後ろ）
            public RacePlate Card;                 // 傾いた板（面と影）
            public RacePlate Tag;                  // 左上の「P1」の札
            public RacePlate DeviceTag;            // 右上の入力装置の札
            public Image HeaderBand;               // 「P1」の札の面
            public TextMeshProUGUI PlayerText;
            public TextMeshProUGUI DeviceText;

            // 参加前
            public CanvasGroup JoinGroup;
            public TextMeshProUGUI JoinTitle;

            // 参加後
            public CanvasGroup ActiveGroup;
            public RectTransform ActiveRect;
            public RectTransform PreviewArea;
            public Image Icon;                     // 2Dの車のアイコン
            public RawImage PreviewImage;          // 3Dプレビューの画像
            public Image ArrowLeft;
            public Image ArrowRight;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI TaglineText;
            public Image[] Dots;
            public TextMeshProUGUI[] StatLabels;
            public Image[][] Pips;
            public CanvasGroup HintGroup;          // 操作の案内の帯（準備完了の間は隠す）
            public TextMeshProUGUI HintText;

            // 準備完了
            public CanvasGroup ReadyGroup;
            public RectTransform ReadyBox;
            public RectTransform ReadyPlate;       // 緑の帯（ポンと出る動きの対象）
            public TextMeshProUGUI ReadyCancelText;

            // 表示している状態（変わった時だけ更新するための記録）
            public bool ShownJoined;
            public bool ShownReady;
            public int ShownType = -1;

            // 動きの開始時刻
            public float JoinedAt = -10f;
            public float ReadyAt = -10f;
            public float ChangedAt = -10f;
            public float LeftFlashAt = -10f;
            public float RightFlashAt = -10f;

            public Preview3D Stage;
        }

        // 3Dプレビュー1つぶん
        private sealed class Preview3D
        {
            public GameObject Root;
            public Transform Turntable;
            public Transform Model;
            public Camera Camera;
            public RenderTexture Texture;
            public float Yaw;
            public float Scale = 1f;
        }

        // 車体の形（部品のコピー元）
        private sealed class PreviewShape
        {
            public Mesh[] Meshes;
            public Material[][] Materials;
            public Vector3[] Positions;
            public Quaternion[] Rotations;
            public Vector3[] Scales;

            // 車体の範囲（車体モデルの原点から見た位置）
            public Vector3 Min;
            public Vector3 Max;
        }


        // =================================================================
        // ■ 内部変数
        // =================================================================

        private bool m_IsOpen;
        private bool m_Fired;
        private float m_OpenedAt;
        private float m_StartAt = -1f;

        private RaceFonts m_Fonts;
        private GameObject m_Template;
        private CarSelectModel m_Model;

        private Canvas m_Canvas;
        private RectTransform m_Root;
        private Image m_Backdrop;
        private SlotView[] m_Views = new SlotView[0];

        private TextMeshProUGUI m_StatusText;
        private RacePlate m_StatusPlate;       // 画面の下のスタートボタン風の帯
        private Image m_StatusIcon;            // 帯の右端の車のシルエット
        private Image m_CountdownBack;
        private Image m_CountdownFill;
        private RectTransform m_CountdownFillRect;

        // 3Dプレビュー
        private PreviewShape m_Shape;
        private GameObject m_StageRoot;
        private readonly List<Material> m_CreatedMaterials = new List<Material>();

        // 車の色の塗り方（RaceGameManager から SetPaintOptions で渡される）
        private string[] m_PaintNames = new string[0];
        private bool m_PaintAutoDetect = true;

        // プレビューの車で、色を塗るマテリアル（3Dプレビューの時だけ。Open で決める）
        private HashSet<Material> m_PaintTargets;

        private readonly List<RaceMenuEvent> m_Events = new List<RaceMenuEvent>();


        // =================================================================
        // ■ 外から見える状態
        // =================================================================

        /// <summary>画面が開いているか</summary>
        public bool IsOpen
        {
            get { return m_IsOpen; }
        }

        /// <summary>参加・車種・準備の状態（開いていない時は null）</summary>
        public CarSelectModel Model
        {
            get { return m_Model; }
        }

        /// <summary>下に出ている案内の文字（テスト・確認用）</summary>
        public string StatusText
        {
            get { return m_StatusText != null ? m_StatusText.text : ""; }
        }

        /// <summary>この枠の、車種の名前の表示（参加前は空）</summary>
        public string NameTextOf(int slot)
        {
            SlotView view = ViewOf(slot);

            return (view != null && view.ShownJoined && view.NameText != null) ? view.NameText.text : "";
        }

        /// <summary>この枠が「READY!」の表示になっているか</summary>
        public bool IsReadyShown(int slot)
        {
            SlotView view = ViewOf(slot);

            return view != null && view.ShownReady;
        }

        /// <summary>この枠が3Dプレビューで表示されているか（false ＝ 2Dのアイコン）</summary>
        public bool Has3DPreview(int slot)
        {
            SlotView view = ViewOf(slot);

            return view != null && view.Stage != null;
        }

        /// <summary>この枠の性能の棒で、色がついている数（5項目のうち、指定した項目）</summary>
        public int FilledPips(int slot, int statIndex)
        {
            SlotView view = ViewOf(slot);

            if (view == null || view.Pips == null || statIndex < 0 || statIndex >= view.Pips.Length)
            {
                return 0;
            }

            int count = 0;

            for (int p = 0; p < view.Pips[statIndex].Length; p++)
            {
                if (view.Pips[statIndex][p].color.a > 0.5f)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>全員 READY になって、スタートを待っている途中か</summary>
        public bool IsCountingDown
        {
            get { return m_IsOpen && m_StartAt >= 0f && !m_Fired; }
        }

        private SlotView ViewOf(int slot)
        {
            return (slot >= 0 && slot < m_Views.Length) ? m_Views[slot] : null;
        }


        // =================================================================
        // ■ 開く・閉じる
        // =================================================================

        /// <summary>
        /// 車の色の塗り方を決める（Open の前に呼ぶ）。
        /// レース中の車と同じ決め方にするため、RaceGameManager の設定をそのまま渡す。
        /// </summary>
        /// <param name="materialNames">車体のマテリアルの名前（の一部）。null・空でもよい</param>
        /// <param name="autoDetect">名前で見つからない時に、色から自動で選ぶか</param>
        public void SetPaintOptions(string[] materialNames, bool autoDetect)
        {
            m_PaintNames = materialNames != null ? materialNames : new string[0];
            m_PaintAutoDetect = autoDetect;
        }

        /// <summary>
        /// 画面を開く。
        /// </summary>
        /// <param name="fonts">使うフォント（null なら Inspector の Font から用意する）</param>
        /// <param name="template">車のひな形（3Dプレビューの形に使う。null なら2Dのアイコン）</param>
        /// <param name="preload">最初から参加させておく人（前回の顔ぶれ。無ければ null）</param>
        public void Open(RaceFonts fonts, GameObject template, RacePlayerEntry[] preload)
        {
            Close();

            m_Fonts = fonts != null ? fonts : RaceFonts.Resolve(m_Font);
            m_Template = template;

            m_Model = new CarSelectModel();
            m_Model.Preload(preload);

            // 横長・縦長の画面でも、4枚の板がはみ出さないよう、1920 × 1080 の範囲が全部見える Canvas にする
            m_Canvas = RaceUiKit.CreateOverlayCanvas("CarSelect Canvas", transform, m_SortingOrder, true);

            m_Root = RaceUiKit.CreateRect("CarSelect", m_Canvas.transform);

            RaceUiKit.Stretch(m_Root);

            BuildBackdrop();
            BuildTitle();

            // 3Dプレビューの元になる車体の形を調べる（見つからなければ null ＝ 2Dのアイコン）
            m_Shape = m_Use3DPreview ? CollectShape(m_Template) : null;

            if (m_Shape != null)
            {
                m_StageRoot = new GameObject("Car Select Preview Stage");
                m_StageRoot.transform.SetParent(transform, false);

                EnsureStageLight();

                // プレビューの車の、色を塗るマテリアル（レース中の車と同じ決め方）
                string paintReport;

                m_PaintTargets = CarPaint.FindTargets(m_Template, m_PaintNames, m_PaintAutoDetect, out paintReport);

                if (m_PaintTargets.Count == 0)
                {
                    Debug.LogWarning(
                        "CarSelectMenu : プレビューの車の色を変えられるマテリアルが見つかりませんでした（色は変わりません）。" +
                        paintReport +
                        "。Race Game Manager の「Paint Material Names」に、車体のマテリアルの名前（の一部）を書いてください。");
                }
            }

            m_Views = new SlotView[RaceSession.MaxPlayers];

            for (int i = 0; i < m_Views.Length; i++)
            {
                m_Views[i] = BuildPanel(i);
            }

            BuildFooter();

            m_OpenedAt = Time.unscaledTime;
            m_StartAt = -1f;
            m_Fired = false;
            m_IsOpen = true;

            // 最初の1フレームから、正しい状態で表示する（前回の顔ぶれが入っている枠も、動きなしで出す）
            RefreshAll(Time.unscaledTime, 0f, true);
        }

        /// <summary>
        /// 画面を閉じる（3Dプレビューも片付ける）。開いていなければ何もしない。
        /// </summary>
        public void Close()
        {
            m_IsOpen = false;
            m_StartAt = -1f;
            m_Fired = false;

            // 3Dプレビューの後片付け
            for (int i = 0; i < m_Views.Length; i++)
            {
                if (m_Views[i] != null && m_Views[i].Stage != null)
                {
                    DestroyPreview(m_Views[i].Stage);

                    m_Views[i].Stage = null;
                }
            }

            for (int i = 0; i < m_CreatedMaterials.Count; i++)
            {
                if (m_CreatedMaterials[i] != null)
                {
                    Destroy(m_CreatedMaterials[i]);
                }
            }

            m_CreatedMaterials.Clear();

            if (m_StageRoot != null)
            {
                Destroy(m_StageRoot);
            }

            m_StageRoot = null;
            m_Shape = null;
            m_PaintTargets = null;

            if (m_Canvas != null)
            {
                Destroy(m_Canvas.gameObject);
            }

            m_Canvas = null;
            m_Root = null;
            m_Backdrop = null;
            m_StatusText = null;
            m_StatusPlate = null;
            m_StatusIcon = null;
            m_CountdownBack = null;
            m_CountdownFill = null;
            m_CountdownFillRect = null;

            m_Views = new SlotView[0];
        }

        private void OnDestroy()
        {
            Close();
        }


        // =================================================================
        // ■ 毎フレーム
        // =================================================================

        private void Update()
        {
            if (!m_IsOpen)
            {
                return;
            }

            float now = Time.unscaledTime;

            // 開いた直後は、ボタンを受け付けない
            if (now - m_OpenedAt >= m_InputGuard)
            {
                ReadInput(now);

                // 入力の処理の中で、画面が閉じられることがある（タイトルへ戻る時など）
                if (!m_IsOpen)
                {
                    return;
                }

                CheckAutoStart(now);

                if (!m_IsOpen)
                {
                    return;
                }
            }

            RefreshAll(now, Time.unscaledDeltaTime, false);
        }

        /// <summary>
        /// ボタンの操作を読み取って、参加・切り替え・準備に反映する。
        /// </summary>
        private void ReadInput(float now)
        {
            // Esc キー：タイトルへ戻る
            if (m_EscapeGoesBack &&
                Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                GoBack();

                return;
            }

            RaceMenuInput.Poll(m_Events);

            for (int i = 0; i < m_Events.Count; i++)
            {
                HandleEvent(m_Events[i], now);

                if (!m_IsOpen)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 1つの入力装置の操作を処理する。
        /// </summary>
        private void HandleEvent(RaceMenuEvent e, float now)
        {
            int slot = m_Model.FindSlotByDevice(e.ControlIndex);

            // まだ参加していない入力装置
            if (slot < 0)
            {
                if (e.Confirm)
                {
                    Join(e);
                }
                else if (e.Cancel && m_Model.JoinedCount == 0)
                {
                    // 誰も参加していない時の取り消しは、タイトルへ戻る
                    GoBack();
                }

                return;
            }

            SlotView view = m_Views[slot];

            // 取り消し：準備を解除する／参加をやめる
            if (e.Cancel)
            {
                m_Model.Cancel(slot);

                return;
            }

            // 左右：車種を切り替える
            if (e.Horizontal != 0 && m_Model.Cycle(slot, e.Horizontal))
            {
                view.ChangedAt = now;

                if (e.Horizontal < 0)
                {
                    view.LeftFlashAt = now;
                }
                else
                {
                    view.RightFlashAt = now;
                }
            }

            // 決定：準備完了にする
            if (e.Confirm)
            {
                m_Model.SetReady(slot, true);
            }
        }

        private void Join(RaceMenuEvent e)
        {
            // 参加できたら、次の RefreshAll で表示が切り替わる（動きもそこで始まる）
            m_Model.TryJoin(
                e.ControlIndex,
                RaceMenuInput.DeviceLabel(e.ControlIndex),
                0);
        }

        /// <summary>
        /// 全員 READY なら、少し待ってからスタートする。READY でなくなれば、待ちをやめる。
        /// </summary>
        private void CheckAutoStart(float now)
        {
            if (!m_Model.AllReady)
            {
                m_StartAt = -1f;
                m_Fired = false;

                return;
            }

            if (m_Fired)
            {
                return;
            }

            if (m_StartAt < 0f)
            {
                m_StartAt = now + Mathf.Max(0f, m_AutoStartDelay);
            }

            if (now >= m_StartAt)
            {
                m_Fired = true;

                if (OnStart != null)
                {
                    OnStart(m_Model.ToEntries());
                }
            }
        }

        private void GoBack()
        {
            if (OnBack != null)
            {
                OnBack();
            }
        }


        // =================================================================
        // ■ 画面を作る
        // =================================================================

        private void BuildBackdrop()
        {
            // 地面の画像（暗い灰緑にクレーター）。不透明度は m_BackdropAlpha
            m_Backdrop =
                RaceUiKit.CreateImage(
                    "Backdrop",
                    m_Root,
                    RaceUiKit.GroundSprite(),
                    new Color(1f, 1f, 1f, m_BackdropAlpha),
                    false);

            RaceUiKit.Stretch(m_Backdrop.rectTransform);

            // 背景は、画面の外のクリックも受け止める（後ろのボタンを押してしまわないように）
            m_Backdrop.raycastTarget = true;

            // 画面の下の、青緑の帯（タンクモードの参加画面と同じ）
            Image band =
                RaceUiKit.CreateImage(
                    "Band",
                    m_Root,
                    RaceUiKit.BandSprite(),
                    RaceColors.WithAlpha(RaceStyle.Teal, 0.95f),
                    false);

            // 横に長い画面でも端まで届くよう、左右いっぱいに広げる（高さは320）
            band.rectTransform.anchorMin = new Vector2(0f, 0f);
            band.rectTransform.anchorMax = new Vector2(1f, 0f);
            band.rectTransform.offsetMin = Vector2.zero;
            band.rectTransform.offsetMax = new Vector2(0f, 320f);
        }

        // 画面の上の見出し「CAR SELECT」（説明の文は、画面の下の案内と重なるので置かない）
        private void BuildTitle()
        {
            // 影（少し右下にずらした、濃い青緑の文字）→ 本体（白）の順に重ねる
            CreateTitleText("TitleShadow", RaceStyle.ShadowTeal, new Vector2(5f, -66f), false);

            CreateTitleText("Title", Color.white, new Vector2(0f, -60f), true);
        }

        private void CreateTitleText(string name, Color color, Vector2 position, bool outline)
        {
            TextMeshProUGUI title =
                RaceUiKit.CreateText(
                    name,
                    m_Root,
                    m_Fonts.Main,
                    94f,
                    color,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                title.rectTransform,
                Mid,
                Mid,
                new Vector2(position.x, HalfHeight + position.y),
                new Vector2(1100f, 120f));

            title.text = "CAR SELECT";

            // 本体には、細い縁取りを付けて、背景の上で読みやすくする
            if (outline)
            {
                RaceUiKit.SetOutline(title, 0.12f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.6f));
            }
        }

        /// <summary>
        /// 枠（P1〜P4）1つぶんを作る。
        /// </summary>
        private SlotView BuildPanel(int index)
        {
            SlotView v = new SlotView();

            v.Index = index;
            v.PlayerColor = RaceColors.Player(index + 1);

            float x = (index - (RaceSession.MaxPlayers - 1) * 0.5f) * (PanelWidth + PanelGap);

            v.Root = RaceUiKit.CreateRect("Slot P" + (index + 1), m_Root);

            RaceUiKit.Place(
                v.Root,
                Mid,
                Mid,
                new Vector2(x, PanelCenterY),
                new Vector2(PanelWidth, PanelHeight));

            Vector2 cardSize =
                new Vector2(
                    PanelWidth,
                    RaceUiKit.CardSpriteHeight(PanelWidth, PanelHeight, RaceStyle.Tilt));

            // 準備完了の時に光る縁（板より少し大きい、プレイヤーの色の板。ふだんは透明）
            v.Outline =
                RaceUiKit.CreateImage(
                    "Outline",
                    v.Root,
                    RaceUiKit.CardSprite(PanelWidth, PanelHeight, RaceStyle.Tilt, PanelRadius),
                    RaceColors.WithAlpha(v.PlayerColor, 0f),
                    false);

            RaceUiKit.Place(
                v.Outline.rectTransform,
                Mid,
                Mid,
                Vector2.zero,
                cardSize + new Vector2(16f, 16f));

            // 傾いた薄い水色の板（影つき）
            v.Card =
                RaceUiKit.CreateCard(
                    "Card",
                    v.Root,
                    PanelWidth,
                    PanelHeight,
                    RaceStyle.Tilt,
                    PanelRadius,
                    RaceStyle.SteelDim,
                    ShadowOff,
                    PanelShadowDepth);

            RaceUiKit.Place(v.Card.Root, Mid, Mid, Vector2.zero, new Vector2(PanelWidth, PanelHeight));

            BuildHeader(v);
            BuildJoinGroup(v);
            BuildActiveGroup(v);
            BuildReadyGroup(v);

            if (m_Shape != null)
            {
                v.Stage = BuildPreview(v);
            }

            // 3Dプレビューができたら、2Dのアイコンの代わりに画像を出す
            v.PreviewImage.gameObject.SetActive(v.Stage != null);
            v.Icon.gameObject.SetActive(v.Stage == null);

            if (v.Stage != null)
            {
                v.PreviewImage.texture = v.Stage.Texture;
            }

            return v;
        }

        private void BuildHeader(SlotView v)
        {
            Color bandColor = v.PlayerColor;
            Color textColor = TextOn(bandColor);

            // 左上：プレイヤーの色の札に「P1」
            v.Tag =
                RaceUiKit.CreatePlate(
                    "Header",
                    v.Root,
                    TagSize,
                    14f,
                    14f,
                    bandColor,
                    RaceStyle.ShadowTeal,
                    8f,
                    RaceStyle.Tilt);

            RaceUiKit.Place(v.Tag.Root, Mid, Mid, TagCenter, TagSize);

            v.HeaderBand = v.Tag.Face;

            v.PlayerText =
                RaceUiKit.CreateText(
                    "PlayerName",
                    v.Tag.Root,
                    m_Fonts.Main,
                    46f,
                    textColor,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Stretch(v.PlayerText.rectTransform);

            v.PlayerText.text = RaceColors.PlayerName(v.Index + 1);
        }

        // 右上：入力装置の名前（参加したあとだけ出る水色の帯。参加後の表示と一緒に出入りする）
        private void BuildDeviceTag(SlotView v)
        {
            v.DeviceTag =
                RaceUiKit.CreatePlate(
                    "DeviceTag",
                    v.ActiveRect,
                    DeviceTagSize,
                    10f,
                    10f,
                    RaceStyle.Azure,
                    RaceStyle.ShadowTeal,
                    6f,
                    RaceStyle.Tilt);

            RaceUiKit.Place(v.DeviceTag.Root, Mid, Mid, DeviceTagCenter, DeviceTagSize);

            v.DeviceText =
                RaceUiKit.CreateText(
                    "Device",
                    v.DeviceTag.Root,
                    m_Fonts.Main,
                    22f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Stretch(v.DeviceText.rectTransform);

            v.DeviceText.rectTransform.offsetMin = new Vector2(12f, 0f);
            v.DeviceText.rectTransform.offsetMax = new Vector2(-12f, 0f);

            // 長い名前（GAMEPAD 2 など）でも、帯に収まるよう小さくする
            RaceUiKit.FitText(v.DeviceText, 14f, 22f);

            v.DeviceText.text = "";
        }

        /// <summary>
        /// 参加前の表示（「JOIN」と、参加できるボタンの名前）。
        /// 「ボタンを押して参加」という文は、画面の下の案内に出るので、ここには置かない。
        /// </summary>
        private void BuildJoinGroup(SlotView v)
        {
            RectTransform rect = RaceUiKit.CreateRect("Join", v.Root);

            RaceUiKit.Stretch(rect);

            v.JoinGroup = rect.gameObject.AddComponent<CanvasGroup>();

            // 飾りの車のアイコン（プレイヤーの色で、うっすら）
            Image decoration =
                RaceUiKit.CreateImage(
                    "Decoration",
                    rect,
                    RaceUiKit.CarIconSprite(),
                    RaceColors.WithAlpha(v.PlayerColor, 0.5f),
                    false);

            decoration.preserveAspect = true;

            RaceUiKit.Place(
                decoration.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, 170f),
                new Vector2(150f, 225f));

            v.JoinTitle =
                RaceUiKit.CreateText(
                    "JoinTitle",
                    rect,
                    m_Fonts.Main,
                    92f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                v.JoinTitle.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, -40f),
                new Vector2(380f, 110f));

            v.JoinTitle.text = "JOIN";

            RaceUiKit.SetOutline(v.JoinTitle, 0.2f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.85f));

            // 参加できるボタンの名前（1行だけ。入力装置の名前は、参加したあとに枠の上に出る）
            TextMeshProUGUI keys =
                RaceUiKit.CreateText(
                    "JoinKeys",
                    rect,
                    m_Fonts.ForJapanese,
                    26f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Place(
                keys.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, -118f),
                new Vector2(380f, 40f));

            // 「SPACE / ENTER / ゲームパッド A」は長いので、板に収まるよう小さくする
            RaceUiKit.FitText(keys, 16f, 26f);

            keys.text = JoinKeysText();

            RaceUiKit.SetOutline(keys, 0.2f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.85f));
        }

        // 「SPACE / ENTER / ゲームパッド A」のような、参加できる決定ボタンの案内
        // （WASD・矢印キー・ゲームパッドの順。名前は、実際の操作と同じ RaceMenuInput から取る）
        private string JoinKeysText()
        {
            return
                RaceMenuInput.ConfirmName(1) + " / " +
                RaceMenuInput.ConfirmName(2) + " / " +
                m_Fonts.Pick(TextGamepadEn, TextGamepadJa) + " " +
                RaceMenuInput.ConfirmName(10);
        }

        /// <summary>
        /// 参加後の表示（車・車種名・性能・操作の案内）。
        /// </summary>
        private void BuildActiveGroup(SlotView v)
        {
            v.ActiveRect = RaceUiKit.CreateRect("Active", v.Root);

            RaceUiKit.Stretch(v.ActiveRect);

            v.ActiveGroup = v.ActiveRect.gameObject.AddComponent<CanvasGroup>();

            BuildDeviceTag(v);

            // ---- 車の表示 ----
            v.PreviewArea = RaceUiKit.CreateRect("Preview", v.ActiveRect);

            RaceUiKit.Place(
                v.PreviewArea,
                Mid,
                Mid,
                new Vector2(0f, PreviewCenterY),
                new Vector2(PreviewWidth, PreviewHeight));

            // 足もとのドット模様の丸（タンクモードと同じ）
            Image dots =
                RaceUiKit.CreateImage(
                    "Halftone",
                    v.PreviewArea,
                    RaceUiKit.HalftoneSprite(),
                    new Color(1f, 1f, 1f, 0.85f),
                    false);

            RaceUiKit.Place(
                dots.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, -22f),
                new Vector2(330f, 182f));

            // 2Dのアイコン
            v.Icon =
                RaceUiKit.CreateImage(
                    "CarIcon",
                    v.PreviewArea,
                    RaceUiKit.CarIconSprite(),
                    v.PlayerColor,
                    false);

            v.Icon.preserveAspect = true;

            RaceUiKit.Place(
                v.Icon.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, 6f),
                new Vector2(138f, 207f));

            // 3Dプレビューの画像
            RectTransform imageRect = RaceUiKit.CreateRect("CarPreview", v.PreviewArea);

            RaceUiKit.Stretch(imageRect);

            v.PreviewImage = imageRect.gameObject.AddComponent<RawImage>();
            v.PreviewImage.raycastTarget = false;
            v.PreviewImage.color = Color.white;

            // 左右の矢印（薄紫。先が尖って、後ろへ消えていく。左向きは、右向きを左右反転して使う）
            Vector2 arrowSize = new Vector2(176f, 60f);

            // 板の上の辺は、右が高く傾いているので、矢印も同じ角度で並べる
            float arrowRise = ArrowCenterX * Mathf.Tan(RaceStyle.Tilt * Mathf.Deg2Rad);

            v.ArrowLeft =
                RaceUiKit.CreateImage("ArrowLeft", v.ActiveRect, RaceUiKit.ArrowSprite(), RaceStyle.Lavender, false);

            RaceUiKit.Place(
                v.ArrowLeft.rectTransform,
                Mid,
                Mid,
                new Vector2(-ArrowCenterX, ArrowCenterY - arrowRise),
                arrowSize);

            v.ArrowLeft.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            v.ArrowLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, RaceStyle.Tilt);

            v.ArrowRight =
                RaceUiKit.CreateImage("ArrowRight", v.ActiveRect, RaceUiKit.ArrowSprite(), RaceStyle.Lavender, false);

            RaceUiKit.Place(
                v.ArrowRight.rectTransform,
                Mid,
                Mid,
                new Vector2(ArrowCenterX, ArrowCenterY + arrowRise),
                arrowSize);

            v.ArrowRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, RaceStyle.Tilt);

            // 何番目の車種かを示す点
            int typeCount = CarTypeCatalog.Count;

            v.Dots = new Image[typeCount];

            const float dotSize = 14f;
            const float dotGap = 12f;

            float dotsWidth = typeCount * dotSize + (typeCount - 1) * dotGap;

            for (int i = 0; i < typeCount; i++)
            {
                Image dot =
                    RaceUiKit.CreateImage("Dot" + i, v.ActiveRect, RaceUiKit.CircleSprite(), DotOff, false);

                RaceUiKit.Place(
                    dot.rectTransform,
                    Mid,
                    Mid,
                    new Vector2(-dotsWidth * 0.5f + dotSize * 0.5f + i * (dotSize + dotGap), DotsCenterY),
                    new Vector2(dotSize, dotSize));

                v.Dots[i] = dot;
            }

            BuildBubble(v);

            // ---- 操作の案内（水色の帯） ----
            RacePlate hint =
                RaceUiKit.CreatePlate(
                    "HintPlate",
                    v.ActiveRect,
                    HintSize,
                    12f,
                    12f,
                    RaceStyle.Azure,
                    RaceStyle.ShadowTeal,
                    8f,
                    RaceStyle.Tilt);

            RaceUiKit.Place(hint.Root, Mid, Mid, HintCenter, HintSize);

            v.HintGroup = hint.Root.gameObject.AddComponent<CanvasGroup>();

            // 「車種を選ぶ」などの日本語が入るので、日本語のフォントを使う
            // （英語用のフォントだと、日本語の文字が□になる）。長い時は、帯に収まるよう小さくする
            v.HintText =
                RaceUiKit.CreateText(
                    "Hint",
                    hint.Root,
                    m_Fonts.ForJapanese,
                    21f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Stretch(v.HintText.rectTransform);

            v.HintText.rectTransform.offsetMin = new Vector2(16f, 4f);
            v.HintText.rectTransform.offsetMax = new Vector2(-16f, -4f);

            RaceUiKit.FitText(v.HintText, 15f, 21f);

            v.HintText.text = "";
        }

        /// <summary>
        /// 性能の吹き出し（車種名・ひとこと説明・5項目の棒）。吹き出しごと、板と同じ向きに少し傾ける。
        /// </summary>
        private void BuildBubble(SlotView v)
        {
            RectTransform bubble =
                RaceUiKit.CreateBubble("Bubble", v.ActiveRect, BubbleSize, RaceStyle.Teal);

            RaceUiKit.Place(bubble, Mid, Mid, BubbleCenter, BubbleSize);

            bubble.localRotation = Quaternion.Euler(0f, 0f, RaceStyle.Tilt);

            // 吹き出しの中の位置は、吹き出しの中心から見た位置（上がプラス）。
            // 上の端から下へ向かって、車種名 → ひとこと説明 → 性能5項目 の順に並べる
            float top = BubbleSize.y * 0.5f;
            float left = -BubbleSize.x * 0.5f;
            float right = BubbleSize.x * 0.5f;

            // ---- 車種名 ----
            v.NameText =
                RaceUiKit.CreateText(
                    "Name",
                    bubble,
                    m_Fonts.Main,
                    52f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                v.NameText.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, top - 38f),
                new Vector2(BubbleSize.x - 20f, 64f));

            RaceUiKit.SetOutline(v.NameText, 0.12f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.7f));

            // ---- ひとこと説明（1行。長い時は、吹き出しに収まるよう小さくする） ----
            v.TaglineText =
                RaceUiKit.CreateText(
                    "Tagline",
                    bubble,
                    m_Fonts.ForJapanese,
                    22f,
                    new Color(1f, 1f, 1f, 0.92f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Place(
                v.TaglineText.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, top - 86f),
                new Vector2(BubbleSize.x - 40f, 32f));

            RaceUiKit.FitText(v.TaglineText, 14f, 22f);

            // ---- 性能（5項目） ----
            int statCount = CarSpec.StatCount;

            v.StatLabels = new TextMeshProUGUI[statCount];
            v.Pips = new Image[statCount][];

            // 項目の名前は左、棒は右に寄せて並べる
            const float labelWidth = 150f;
            const float pipWidth = 22f;
            const float pipHeight = 14f;
            const float pipPitch = 27f;
            const float rowPitch = 34f;

            float labelCenterX = left + 20f + labelWidth * 0.5f;
            float pipsRight = right - 22f;

            for (int s = 0; s < statCount; s++)
            {
                float y = top - 134f - s * rowPitch;

                TextMeshProUGUI label =
                    RaceUiKit.CreateText(
                        "Stat" + s,
                        bubble,
                        m_Fonts.ForJapanese,
                        24f,
                        Color.white,
                        TextAlignmentOptions.MidlineLeft,
                        FontStyles.Bold);

                RaceUiKit.Place(
                    label.rectTransform,
                    Mid,
                    Mid,
                    new Vector2(labelCenterX, y),
                    new Vector2(labelWidth, 32f));

                RaceUiKit.FitText(label, 15f, 24f);

                label.text = m_Fonts.Pick(CarSpec.StatNamesEn[s], CarSpec.StatNamesJa[s]);

                v.StatLabels[s] = label;

                v.Pips[s] = new Image[CarSpec.MaxPoint];

                for (int p = 0; p < CarSpec.MaxPoint; p++)
                {
                    Image pip =
                        RaceUiKit.CreateImage(
                            "Pip",
                            bubble,
                            RaceUiKit.PlateSprite(Mathf.RoundToInt(pipWidth), Mathf.RoundToInt(pipHeight), 4f, 4f),
                            DimPip,
                            false);

                    float fromRight = CarSpec.MaxPoint - 1 - p;

                    RaceUiKit.Place(
                        pip.rectTransform,
                        Mid,
                        Mid,
                        new Vector2(pipsRight - pipWidth * 0.5f - fromRight * pipPitch, y),
                        new Vector2(pipWidth, pipHeight));

                    v.Pips[s][p] = pip;
                }
            }
        }

        /// <summary>
        /// 準備完了の表示（緑の帯に「READY!」と、取り消しの案内）。
        /// </summary>
        private void BuildReadyGroup(SlotView v)
        {
            v.ReadyBox = RaceUiKit.CreateRect("Ready", v.Root);

            RaceUiKit.Stretch(v.ReadyBox);

            v.ReadyGroup = v.ReadyBox.gameObject.AddComponent<CanvasGroup>();

            RacePlate plate =
                RaceUiKit.CreatePlate(
                    "ReadyPlate",
                    v.ReadyBox,
                    HintSize,
                    12f,
                    12f,
                    RaceStyle.ReadyGreen,
                    RaceStyle.ShadowTeal,
                    8f,
                    RaceStyle.Tilt);

            RaceUiKit.Place(plate.Root, Mid, Mid, HintCenter, HintSize);

            v.ReadyPlate = plate.Root;

            TextMeshProUGUI text =
                RaceUiKit.CreateText(
                    "ReadyText",
                    plate.Root,
                    m_Fonts.Main,
                    40f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                text.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, 12f),
                new Vector2(HintSize.x - 20f, 48f));

            text.text = "READY!";

            RaceUiKit.SetOutline(text, 0.16f, new Color(0f, 0.25f, 0.08f, 0.7f));

            // 取り消しの案内（入力装置ごとに変わるので、毎フレーム更新ではなく、状態が変わった時に書く）。
            // 「取り消し」などの日本語が入るので、日本語のフォントを使う
            TextMeshProUGUI cancel =
                RaceUiKit.CreateText(
                    "ReadyCancel",
                    plate.Root,
                    m_Fonts.ForJapanese,
                    20f,
                    new Color(1f, 1f, 1f, 0.92f),
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            RaceUiKit.Place(
                cancel.rectTransform,
                Mid,
                Mid,
                new Vector2(0f, -20f),
                new Vector2(HintSize.x - 20f, 26f));

            RaceUiKit.FitText(cancel, 14f, 20f);

            cancel.text = "";

            v.ReadyCancelText = cancel;
        }

        /// <summary>
        /// 画面の下（スタートボタン風の帯・スタートまでの待ち・Esc の案内）を作る。
        /// </summary>
        private void BuildFooter()
        {
            // 帯：全員の準備ができるとオレンジ、まだの時は暗い茶色（タンクモードのスタートボタンと同じ）
            m_StatusPlate =
                RaceUiKit.CreatePlate(
                    "StatusPlate",
                    m_Root,
                    StatusSize,
                    0f,
                    24f,
                    RaceStyle.OrangeDim,
                    RaceStyle.ShadowTeal,
                    14f,
                    0f);

            RaceUiKit.Place(
                m_StatusPlate.Root,
                Mid,
                Mid,
                new Vector2(0f, StatusCenterY - HalfHeight),
                StatusSize);

            // 右端の車のシルエット（上を向いた車のアイコンを、右向きに回す）
            m_StatusIcon =
                RaceUiKit.CreateImage(
                    "StatusIcon",
                    m_StatusPlate.Root,
                    RaceUiKit.CarIconSprite(),
                    new Color(0.75f, 0.45f, 0f, 0.55f),
                    false);

            m_StatusIcon.preserveAspect = true;

            RaceUiKit.Place(
                m_StatusIcon.rectTransform,
                Mid,
                Mid,
                new Vector2(StatusSize.x * 0.5f - 62f, 0f),
                new Vector2(66f, 99f));

            m_StatusIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            m_StatusText =
                RaceUiKit.CreateText(
                    "Status",
                    m_StatusPlate.Root,
                    m_Fonts.ForJapanese,
                    40f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold | FontStyles.Italic);

            RaceUiKit.Place(
                m_StatusText.rectTransform,
                Mid,
                Mid,
                new Vector2(-44f, 2f),
                new Vector2(StatusSize.x - 190f, 64f));

            RaceUiKit.FitText(m_StatusText, 24f, 40f);

            RaceUiKit.SetOutline(m_StatusText, 0.16f, new Color(0.25f, 0.12f, 0f, 0.55f));

            // スタートまでの待ちを示す棒（帯の下の縁に沿って出る）
            m_CountdownBack =
                RaceUiKit.CreatePanel(
                    "CountdownBack",
                    m_StatusPlate.Root,
                    new Color(0.4f, 0.2f, 0f, 0.45f),
                    5f);

            RaceUiKit.Place(
                m_CountdownBack.rectTransform,
                Mid,
                Mid,
                new Vector2(-44f, -30f),
                new Vector2(StatusSize.x - 200f, 10f));

            m_CountdownFill =
                RaceUiKit.CreatePanel("CountdownFill", m_CountdownBack.transform, Color.white, 5f);

            m_CountdownFillRect = m_CountdownFill.rectTransform;

            m_CountdownFillRect.anchorMin = new Vector2(0f, 0f);
            m_CountdownFillRect.anchorMax = new Vector2(0f, 1f);
            m_CountdownFillRect.pivot = new Vector2(0f, 0.5f);
            m_CountdownFillRect.anchoredPosition = Vector2.zero;
            m_CountdownFillRect.sizeDelta = new Vector2(0f, 0f);

            m_CountdownBack.gameObject.SetActive(false);

            // Esc の案内
            if (m_EscapeGoesBack)
            {
                TextMeshProUGUI back =
                    RaceUiKit.CreateText(
                        "BackHint",
                        m_Root,
                        m_Fonts.ForJapanese,
                        26f,
                        Color.white,
                        TextAlignmentOptions.MidlineLeft,
                        FontStyles.Bold);

                RaceUiKit.Place(
                    back.rectTransform,
                    Mid,
                    new Vector2(0f, 0f),
                    new Vector2(-RaceUiKit.ReferenceResolution.x * 0.5f + 44f, -HalfHeight + 22f),
                    new Vector2(500f, 36f));

                back.text = KeyGuide("ESC", TextBackToTitleEn, TextBackToTitleJa);

                RaceUiKit.SetOutline(back, 0.2f, RaceColors.WithAlpha(RaceStyle.DeepTeal, 0.9f));
            }
        }

        // 背景の色の上で読みやすい文字の色（明るい背景には暗い文字）
        private static Color TextOn(Color background)
        {
            return RaceColors.TextOn(background);
        }


        // =================================================================
        // ■ 表示を更新する
        // =================================================================

        /// <param name="instant">true なら、参加・退出の動き（フェード）を付けずに切り替える</param>
        private void RefreshAll(float now, float deltaTime, bool instant)
        {
            for (int i = 0; i < m_Views.Length; i++)
            {
                RefreshView(m_Views[i], now, deltaTime, instant);
            }

            RefreshFooter(now);
        }

        /// <summary>
        /// 枠1つぶんの表示を、いまの状態に合わせる。
        /// 状態が変わった時だけ文字や色を書き換え、動き（拡大・点滅）は毎フレーム計算する。
        /// </summary>
        private void RefreshView(SlotView v, float now, float deltaTime, bool instant)
        {
            CarSelectModel.Slot slot = m_Model.Get(v.Index);

            bool joined = slot.Joined;
            bool ready = joined && slot.Ready;

            // ---- 状態が変わった時 ----
            if (joined != v.ShownJoined)
            {
                v.ShownJoined = joined;

                // 動きの始まり（instant の時は、動きが終わった後の状態にする）
                v.JoinedAt = instant ? -10f : now;

                v.ShownType = -1;

                if (v.Stage != null)
                {
                    v.Stage.Camera.enabled = joined;
                    v.Stage.Yaw = 200f;
                }

                v.DeviceText.text = joined ? slot.DeviceLabel : "";
            }

            if (ready != v.ShownReady)
            {
                v.ShownReady = ready;
                v.ReadyAt = now;

                v.ReadyCancelText.text =
                    joined ? KeyGuide(RaceMenuInput.CancelName(slot.ControlIndex), TextCancelEn, TextCancelJa) : "";
            }

            if (joined && slot.CarTypeIndex != v.ShownType)
            {
                bool first = v.ShownType < 0;

                v.ShownType = slot.CarTypeIndex;

                ApplyType(v, slot, first);
            }

            if (joined)
            {
                string hint = HintFor(slot.ControlIndex);

                if (v.HintText.text != hint)
                {
                    v.HintText.text = hint;
                }
            }

            // ---- 動き ----
            AnimateView(v, joined, ready, now);

            if (v.Stage != null && joined)
            {
                UpdatePreview(v, slot, deltaTime);
            }
        }

        /// <summary>
        /// 選んでいる車種に合わせて、名前・説明・点・性能の棒を書き換える。
        /// </summary>
        private void ApplyType(SlotView v, CarSelectModel.Slot slot, bool first)
        {
            CarTypeInfo info = CarTypeCatalog.Get(slot.CarTypeIndex);

            v.NameText.text = info.NameEn;
            v.TaglineText.text = info.Tagline(m_Fonts.HasJapanese);

            for (int i = 0; i < v.Dots.Length; i++)
            {
                v.Dots[i].color = i == slot.CarTypeIndex ? DotOn : DotOff;
            }

            for (int s = 0; s < v.Pips.Length; s++)
            {
                int points = info.Spec.Get(s);

                for (int p = 0; p < v.Pips[s].Length; p++)
                {
                    v.Pips[s][p].color = p < points ? Color.white : DimPip;
                }
            }

            // 2Dのアイコンの大きさ：重い車ほど少し大きい
            float size = IconScaleOf(slot.CarTypeIndex);

            v.Icon.rectTransform.sizeDelta = new Vector2(138f * size, 207f * size);

            // 最初に参加した時は、切り替えの動き（ポン）を付けない
            if (first)
            {
                v.ChangedAt = -10f;

                if (v.Stage != null)
                {
                    v.Stage.Scale = PreviewScaleOf(slot.CarTypeIndex);
                }
            }
        }

        // 車種の「おもさ」から決まる、車体の大きさの倍率（レース中の車体と同じ割合）
        private float PreviewScaleOf(int carTypeIndex)
        {
            int weight = CarTypeCatalog.Get(carTypeIndex).Spec.Weight;

            return CarTuning.Scale(CarTuning.ModelScale(weight), m_SizeInfluence);
        }

        private float IconScaleOf(int carTypeIndex)
        {
            return PreviewScaleOf(carTypeIndex);
        }

        // 入力装置に合わせた、操作の案内（2行）
        //   1行目：左右のキーで、車種を選ぶ
        //   2行目：決定のキーで OK ／ 取り消しのキーで、戻る
        private string HintFor(int controlIndex)
        {
            return
                KeyGuide(RaceMenuInput.MoveName(controlIndex), TextChangeEn, TextChangeJa) + "\n" +
                KeyGuide(RaceMenuInput.ConfirmName(controlIndex), TextOkEn, TextOkJa) +
                m_Fonts.Pick("        ", "　　") +
                KeyGuide(RaceMenuInput.CancelName(controlIndex), TextBackEn, TextBackJa);
        }

        // 「キーの名前 ＋ その働き」の形の案内にする
        // （日本語は「A / D：車種を選ぶ」、英語は「A / D   CHANGE」）
        private string KeyGuide(string key, string english, string japanese)
        {
            return m_Fonts.HasJapanese ? key + "：" + japanese : key + "   " + english;
        }

        /// <summary>
        /// 参加・準備・切り替えの動きと、点滅を計算する。
        /// </summary>
        private void AnimateView(SlotView v, bool joined, bool ready, float now)
        {
            // 参加前 ⇔ 参加後 の入れ替え（0.2秒でフェード）
            float fade = Mathf.Clamp01((now - v.JoinedAt) / 0.2f);
            float activeAlpha = joined ? fade : 1f - fade;

            v.ActiveGroup.alpha = activeAlpha;
            v.JoinGroup.alpha = 1f - activeAlpha;

            // 板の色：参加前は暗い水色、参加後は明るい水色（影も、参加後は濃くなる）
            v.Card.Face.color = Color.Lerp(RaceStyle.SteelDim, RaceStyle.Steel, activeAlpha);
            v.Card.Shadow.color = Color.Lerp(ShadowOff, ShadowOn, activeAlpha);

            // 参加した瞬間の、ふくらむ動き
            float pop = joined ? Mathf.Clamp01((now - v.JoinedAt) / 0.3f) : 1f;
            float popScale = Mathf.Lerp(0.9f, 1f, RaceEase.OutBack(pop));

            v.ActiveRect.localScale = new Vector3(popScale, popScale, 1f);

            // 参加前の「JOIN」：ゆっくり点滅
            float blink = 0.6f + 0.4f * Mathf.Sin(now * 3.2f + v.Index);

            RaceUiKit.SetAlpha(v.JoinTitle, blink);

            // 縁：READY の間だけ、プレイヤーの色で光る
            float outlineAlpha = 0f;

            if (ready)
            {
                outlineAlpha =
                    Mathf.Clamp01((now - v.ReadyAt) / 0.12f) * (0.85f + 0.15f * Mathf.Sin(now * 5f));
            }

            RaceUiKit.SetAlpha(v.Outline, outlineAlpha);

            // 車種を切り替えた時の、車の表示のポン
            float change = Mathf.Clamp01((now - v.ChangedAt) / 0.25f);
            float changeScale = 1f + 0.10f * (1f - RaceEase.OutCubic(change));

            v.PreviewArea.localScale = new Vector3(changeScale, changeScale, 1f);

            // 矢印：押した時に光る
            SetArrow(v.ArrowLeft, now - v.LeftFlashAt, joined && !ready);
            SetArrow(v.ArrowRight, now - v.RightFlashAt, joined && !ready);

            // READY の表示（緑の帯がポンと出る）
            float readyT = ready ? Mathf.Clamp01((now - v.ReadyAt) / 0.3f) : 0f;

            v.ReadyGroup.alpha = ready ? Mathf.Clamp01((now - v.ReadyAt) / 0.12f) : 0f;

            float readyScale = Mathf.Lerp(0.6f, 1f, RaceEase.OutBack(readyT));

            v.ReadyPlate.localScale = new Vector3(readyScale, readyScale, 1f);

            // 操作の案内は、READY の間は隠す（READY の帯が同じ場所に出る）
            v.HintGroup.alpha = ready ? 0f : 1f;
        }

        // 矢印の色：普段は薄紫、押した直後（0.18秒）は白く大きく。準備完了後は隠す
        private static void SetArrow(Image arrow, float sincePressed, bool visible)
        {
            float flash = Mathf.Clamp01(1f - sincePressed / 0.18f);

            Color color = Color.Lerp(RaceStyle.Lavender, Color.white, flash);

            color.a = visible ? 1f : 0f;

            arrow.color = color;

            float scale = 1f + 0.25f * flash;

            Vector3 current = arrow.rectTransform.localScale;

            arrow.rectTransform.localScale =
                new Vector3(Mathf.Sign(current.x == 0f ? 1f : current.x) * scale, scale, 1f);
        }

        /// <summary>
        /// 画面の下の案内（スタートボタン風の帯）と、スタートまでの待ちの棒を更新する。
        /// </summary>
        private void RefreshFooter(float now)
        {
            int joined = m_Model.JoinedCount;

            int ready = 0;

            for (int i = 0; i < CarSelectModel.SlotCount; i++)
            {
                CarSelectModel.Slot slot = m_Model.Get(i);

                if (slot.Joined && slot.Ready)
                {
                    ready++;
                }
            }

            string status;

            if (joined == 0)
            {
                status = m_Fonts.Pick(TextJoinEn, TextJoinJa);
            }
            else if (m_Model.AllReady)
            {
                status = m_Fonts.Pick(TextAllReadyEn, TextAllReadyJa);
            }
            else
            {
                // 準備ができた人数 / 参加した人数
                status =
                    m_Fonts.Pick(
                        TextWaitingEn + "   " + ready + " / " + joined + " READY",
                        TextWaitingJa + "　" + ready + " / " + joined + "人");
            }

            if (m_StatusText.text != status)
            {
                m_StatusText.text = status;
            }

            // 帯の色：全員 READY ならオレンジ（スタートできる）、まだなら暗い茶色（タンクモードのスタートボタンと同じ）
            bool go = joined > 0 && m_Model.AllReady;

            m_StatusPlate.Face.color = go ? RaceStyle.Orange : RaceStyle.OrangeDim;
            m_StatusIcon.color = go ? StatusIconOn : StatusIconOff;

            // スタートまでの待ちの棒
            bool counting = m_StartAt >= 0f && !m_Fired;

            if (m_CountdownBack.gameObject.activeSelf != counting)
            {
                m_CountdownBack.gameObject.SetActive(counting);
            }

            // スタートを待つ間は、帯がわずかに脈打つ
            float pulse = counting ? 1f + 0.012f * Mathf.Sin(now * 14f) : 1f;

            m_StatusPlate.Root.localScale = new Vector3(pulse, pulse, 1f);

            if (counting)
            {
                float length = Mathf.Max(0.01f, m_AutoStartDelay);
                float progress = Mathf.Clamp01(1f - (m_StartAt - now) / length);

                m_CountdownFillRect.anchorMax = new Vector2(progress, 1f);
            }
        }


        // =================================================================
        // ■ 3Dプレビュー
        // =================================================================

        /// <summary>
        /// 車のひな形から、見た目の部品（メッシュとマテリアルの位置）を集める。
        /// 車体モデル（CarMovement の Kart Model。無ければ車全体）の下にある、描画されている部品だけを対象にする。
        /// 部品の位置・向き・大きさは、車体モデルから見た相対の値で覚える。
        /// </summary>
        /// <returns>形が見つかれば、その情報。見つからなければ null</returns>
        private static PreviewShape CollectShape(GameObject template)
        {
            if (template == null)
            {
                return null;
            }

            Transform root = template.transform;

            CarMovement movement = template.GetComponent<CarMovement>();

            if (movement != null && movement.kartModel != null)
            {
                root = movement.kartModel;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            List<Mesh> meshes = new List<Mesh>();
            List<Material[]> materials = new List<Material[]>();
            List<Vector3> positions = new List<Vector3>();
            List<Quaternion> rotations = new List<Quaternion>();
            List<Vector3> scales = new List<Vector3>();

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            Quaternion rootInverse = Quaternion.Inverse(root.rotation);
            Vector3 rootScale = root.lossyScale;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (renderer == null || !renderer.enabled || !IsActiveUnder(filter.transform, root))
                {
                    continue;
                }

                Transform t = filter.transform;

                // 車体モデルから見た位置・向き・大きさ
                Vector3 position = root.InverseTransformPoint(t.position);
                Quaternion rotation = rootInverse * t.rotation;

                Vector3 scale =
                    new Vector3(
                        Ratio(t.lossyScale.x, rootScale.x),
                        Ratio(t.lossyScale.y, rootScale.y),
                        Ratio(t.lossyScale.z, rootScale.z));

                meshes.Add(filter.sharedMesh);
                materials.Add(renderer.sharedMaterials);
                positions.Add(position);
                rotations.Add(rotation);
                scales.Add(scale);

                // メッシュの箱の8つの角を、車体モデルから見た位置に直して、全体の範囲を広げる
                Bounds box = filter.sharedMesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local =
                        box.center +
                        new Vector3(
                            (corner & 1) == 0 ? -box.extents.x : box.extents.x,
                            (corner & 2) == 0 ? -box.extents.y : box.extents.y,
                            (corner & 4) == 0 ? -box.extents.z : box.extents.z);

                    Vector3 point = position + rotation * Vector3.Scale(scale, local);

                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            }

            if (meshes.Count == 0)
            {
                return null;
            }

            // 大きさがほとんど無い（形が壊れている）場合は使わない
            Vector3 size = max - min;

            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f)
            {
                return null;
            }

            PreviewShape shape = new PreviewShape();

            shape.Meshes = meshes.ToArray();
            shape.Materials = materials.ToArray();
            shape.Positions = positions.ToArray();
            shape.Rotations = rotations.ToArray();
            shape.Scales = scales.ToArray();
            shape.Min = min;
            shape.Max = max;

            return shape;
        }

        // root から t までの間に、非表示のオブジェクトが無いか（root 自身は調べない）
        private static bool IsActiveUnder(Transform t, Transform root)
        {
            while (t != null && t != root)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }

                t = t.parent;
            }

            return true;
        }

        // 大きさの比（0で割らない）
        private static float Ratio(float value, float reference)
        {
            return Mathf.Abs(reference) < 0.00001f ? value : value / reference;
        }

        /// <summary>
        /// 枠1つぶんの3Dプレビュー（部品のコピー・回転台・カメラ・画像）を作る。
        /// どこかで失敗したら、警告を出して null を返す（その枠は2Dのアイコンになる）。
        /// </summary>
        private Preview3D BuildPreview(SlotView v)
        {
            GameObject root = null;
            RenderTexture texture = null;

            try
            {
                PreviewShape shape = m_Shape;

                root = new GameObject("Car Preview P" + (v.Index + 1));
                root.transform.SetParent(m_StageRoot.transform, false);

                // 枠ごとに、横へずらして置く（お互いが写り込まないように）
                root.transform.position = m_PreviewStageOrigin + Vector3.right * (v.Index * 60f);

                Preview3D preview = new Preview3D();

                preview.Root = root;

                // 回転台：車体の底の中心が、台の中心にくるように置く
                GameObject turntable = new GameObject("Turntable");

                turntable.transform.SetParent(root.transform, false);

                preview.Turntable = turntable.transform;

                GameObject model = new GameObject("Model");

                model.transform.SetParent(turntable.transform, false);

                preview.Model = model.transform;

                // 部品をコピーする（車体のマテリアルは、プレイヤーの色をつけたコピーに差し替える）
                for (int i = 0; i < shape.Meshes.Length; i++)
                {
                    GameObject part = new GameObject("Part" + i);

                    part.transform.SetParent(model.transform, false);

                    part.transform.localPosition = shape.Positions[i];
                    part.transform.localRotation = shape.Rotations[i];
                    part.transform.localScale = shape.Scales[i];

                    MeshFilter filter = part.AddComponent<MeshFilter>();

                    filter.sharedMesh = shape.Meshes[i];

                    MeshRenderer renderer = part.AddComponent<MeshRenderer>();

                    renderer.sharedMaterials =
                        CarPaint.Tint(shape.Materials[i], m_PaintTargets, v.PlayerColor, m_CreatedMaterials);
                }

                // 映す画像
                texture = new RenderTexture(PreviewTextureWidth, PreviewTextureHeight, 24, RenderTextureFormat.ARGB32);
                texture.name = "Car Preview Texture P" + (v.Index + 1);
                texture.antiAliasing = 4;
                texture.filterMode = FilterMode.Bilinear;

                // カメラ
                GameObject cameraObject = new GameObject("Camera");

                cameraObject.transform.SetParent(root.transform, false);

                Camera camera = cameraObject.AddComponent<Camera>();

                camera.clearFlags = CameraClearFlags.SolidColor;

                // 背景は透明（もし透明にならない環境でも、枠の色と同じなので目立たない）
                camera.backgroundColor = PreviewBackground;
                camera.cullingMask = ~0;
                camera.fieldOfView = m_PreviewFieldOfView;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.allowHDR = false;
                camera.useOcclusionCulling = false;
                camera.targetTexture = texture;

                // 参加するまでは、撮影しない
                camera.enabled = false;

                preview.Camera = camera;
                preview.Texture = texture;

                preview.Yaw = 200f;

                FrameCamera(preview, shape);

                return preview;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    "CarSelectMenu : 3Dプレビューを作れなかったため、2Dのアイコンにします\n" + e);

                if (texture != null)
                {
                    texture.Release();

                    Destroy(texture);
                }

                if (root != null)
                {
                    Destroy(root);
                }

                return null;
            }
        }

        /// <summary>
        /// 車体がちょうど収まるように、カメラの位置と向きを決める。
        /// 車体は回転台の上で回るので、真上から見た時の対角線の長さを基準にする。
        /// 車種ごとの大きさの違い（最大で約 1.1 倍）が見えるよう、車種が変わっても、カメラは動かさない。
        /// </summary>
        private void FrameCamera(Preview3D preview, PreviewShape shape)
        {
            Vector3 size = shape.Max - shape.Min;

            float footprint = Mathf.Sqrt(size.x * size.x + size.z * size.z);

            float pitch = m_PreviewPitch * Mathf.Deg2Rad;

            // 一番大きく見える車種（約1.1倍）と、少しの余白を見込む
            float margin = 1.22f;

            float halfFov = Mathf.Max(5f, m_PreviewFieldOfView) * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfFov);
            float aspect = (float)PreviewTextureWidth / PreviewTextureHeight;

            // 横幅に収める距離／縦に収める距離、の大きい方
            float horizontal = footprint * margin / (2f * tanHalf * aspect);
            float vertical = (size.y + footprint * Mathf.Sin(pitch)) * margin / (2f * tanHalf);

            // 手前に来る部分は近く見えるので、奥行きの半分を足す
            float distance = Mathf.Max(horizontal, vertical) + footprint * 0.5f;

            Vector3 target = new Vector3(0f, size.y * 0.42f, 0f);

            Quaternion rotation = Quaternion.Euler(m_PreviewPitch, 0f, 0f);

            Transform cameraTransform = preview.Camera.transform;

            cameraTransform.localPosition = target - rotation * Vector3.forward * distance;
            cameraTransform.localRotation = rotation;
        }

        /// <summary>
        /// 3Dプレビューの車を回し、車種に合わせた大きさにする。
        /// </summary>
        private void UpdatePreview(SlotView v, CarSelectModel.Slot slot, float deltaTime)
        {
            Preview3D preview = v.Stage;

            // 車種が変わったら、大きさがなめらかに変わる
            float target = PreviewScaleOf(slot.CarTypeIndex);

            preview.Scale = Mathf.Lerp(preview.Scale, target, 1f - Mathf.Exp(-14f * deltaTime));

            // 車体の底の中心を、回転台の中心に合わせる（大きさを変えても、底の位置は動かない）
            Vector3 pivot =
                new Vector3(
                    (m_Shape.Min.x + m_Shape.Max.x) * 0.5f,
                    m_Shape.Min.y,
                    (m_Shape.Min.z + m_Shape.Max.z) * 0.5f);

            preview.Model.localScale = Vector3.one * preview.Scale;
            preview.Model.localPosition = -preview.Scale * pivot;

            // 準備完了の間は、ゆっくり回す
            float speed = slot.Ready ? m_PreviewTurnSpeed * 0.35f : m_PreviewTurnSpeed;

            preview.Yaw = Mathf.Repeat(preview.Yaw + speed * deltaTime, 360f);

            preview.Turntable.localRotation = Quaternion.Euler(0f, preview.Yaw, 0f);
        }

        private void DestroyPreview(Preview3D preview)
        {
            if (preview.Camera != null)
            {
                preview.Camera.targetTexture = null;
                preview.Camera.enabled = false;
            }

            if (preview.Texture != null)
            {
                preview.Texture.Release();

                Destroy(preview.Texture);
            }

            if (preview.Root != null)
            {
                Destroy(preview.Root);
            }
        }

        /// <summary>
        /// シーンに太陽の光（Directional Light）が無い時だけ、プレビュー用の光を足す。
        /// （光が無いと、車が真っ暗に写る。ある時は、その光がそのまま当たる）
        /// </summary>
        private void EnsureStageLight()
        {
            Light[] lights =
                FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == UnityEngine.LightType.Directional && lights[i].enabled)
                {
                    return;
                }
            }

            GameObject lightObject = new GameObject("Preview Light");

            lightObject.transform.SetParent(m_StageRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            Light light = lightObject.AddComponent<Light>();

            light.type = UnityEngine.LightType.Directional;
            light.intensity = 1.1f;
            light.color = Color.white;
        }
    }
}