using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レース画面のUI部品（色・文字・メニュー入力・画像づくり）
    // =====================================================================
    //
    // RaceHud（レース中の表示）、RaceResultsUI（順位発表）、CarSelectMenu（車種選択）が
    // 共通で使う道具をまとめたファイル。
    //
    //   RaceColors     … プレイヤーごとの色（P1 青／P2 赤／P3 緑／P4 黄）と、順位の色
    //   RaceFonts      … 日本語を出せるフォントの用意（無ければ英語の表示に切り替える）
    //   RaceMenuInput  … メニューの操作（WASD／矢印キー／ゲームパッドの、決定・取り消し・移動）
    //   RaceEase       … 動きの付け方（ゆっくり止まる、行き過ぎて戻る、など）
    //   RaceUiKit      … UIの部品を作る道具（Canvas・画像・文字・丸や三角の画像）
    //
    // =====================================================================


    // =====================================================================
    // ■ 色
    // =====================================================================

    /// <summary>
    /// レース画面で使う色。
    /// </summary>
    public static class RaceColors
    {
        /// <summary>P1〜P4 の色（車の色・画面の枠・ミニマップのアイコンで共通）</summary>
        public static readonly Color[] Players = new Color[]
        {
            new Color(0.20f, 0.48f, 1.00f, 1f),  // P1 青
            new Color(0.95f, 0.24f, 0.24f, 1f),  // P2 赤
            new Color(0.22f, 0.80f, 0.36f, 1f),  // P3 緑
            new Color(1.00f, 0.82f, 0.12f, 1f)   // P4 黄
        };

        /// <summary>1位の色（金）</summary>
        public static readonly Color Gold = new Color(1.00f, 0.84f, 0.00f, 1f);

        /// <summary>2位の色（銀）</summary>
        public static readonly Color Silver = new Color(0.80f, 0.82f, 0.86f, 1f);

        /// <summary>3位の色（銅）</summary>
        public static readonly Color Bronze = new Color(0.85f, 0.52f, 0.22f, 1f);

        /// <summary>4位以下の色（白）</summary>
        public static readonly Color Plain = new Color(1f, 1f, 1f, 1f);

        /// <summary>画面の暗い背景色（パネルなど）</summary>
        public static readonly Color Panel = new Color(0.05f, 0.06f, 0.10f, 0.78f);

        /// <summary>
        /// プレイヤー番号（1〜4）の色。範囲外の番号は、1〜4 に循環させる。
        /// </summary>
        public static Color Player(int playerNumber)
        {
            int count = Players.Length;
            int index = (((playerNumber - 1) % count) + count) % count;

            return Players[index];
        }

        /// <summary>プレイヤー番号の表示名（「P1」など）</summary>
        public static string PlayerName(int playerNumber)
        {
            return "P" + playerNumber;
        }

        /// <summary>順位（1から）の色。1位＝金／2位＝銀／3位＝銅／4位以下＝白</summary>
        public static Color Rank(int rank)
        {
            switch (rank)
            {
                case 1: return Gold;
                case 2: return Silver;
                case 3: return Bronze;
                default: return Plain;
            }
        }

        // 明るい背景の上に載せる、黒っぽい文字の色
        private static readonly Color DarkText = new Color(0.07f, 0.08f, 0.11f, 1f);

        /// <summary>
        /// この背景の上では、白い文字が読みにくいか（黄・緑などの明るい色）。
        /// 白との明るさの差（コントラスト比）が 3 より小さい時に true。
        /// </summary>
        public static bool UsesDarkText(Color background)
        {
            float luminance =
                0.2126f * ToLinear(background.r) +
                0.7152f * ToLinear(background.g) +
                0.0722f * ToLinear(background.b);

            float whiteContrast = 1.05f / (luminance + 0.05f);

            return whiteContrast < 3f;
        }

        /// <summary>
        /// 背景の色の上で読みやすい文字の色。明るい背景（黄・緑）には黒っぽい文字、
        /// それ以外（青・赤など）には白い文字。
        /// </summary>
        public static Color TextOn(Color background)
        {
            return UsesDarkText(background) ? DarkText : Color.white;
        }

        // sRGB の値（0〜1）を、明るさの計算用の値に直す
        private static float ToLinear(float value)
        {
            return value <= 0.04045f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>色の明るさを変える（1より小さいと暗く、大きいと明るく。アルファは変えない）</summary>
        public static Color Scaled(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }

        /// <summary>色のアルファ（不透明度）だけを変える</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }


    // =====================================================================
    // ■ タンクモード風の配色
    // =====================================================================

    /// <summary>
    /// タンクモードの画面（参加画面・スタート画面）に寄せた配色。
    ///
    /// 薄い水色の板・青緑の吹き出し・オレンジのスタートボタン・薄紫の矢印、という組み合わせ。
    /// 色を変えたい時は、ここだけ書き換えればよい（画面ごとの色の指定は、すべてここを通る）。
    /// </summary>
    public static class RaceStyle
    {
        /// <summary>板（カード）の色：薄い水色。ほぼ不透明</summary>
        public static readonly Color Steel = new Color(0.64f, 0.73f, 0.77f, 0.95f);

        /// <summary>参加前の板：同じ水色を少し暗くしたもの（影が透けて見えないよう、ほぼ不透明）</summary>
        public static readonly Color SteelDim = new Color(0.46f, 0.55f, 0.60f, 0.95f);

        /// <summary>吹き出し・見出し帯の青緑</summary>
        public static readonly Color Teal = new Color(0.00f, 0.41f, 0.54f, 1f);

        /// <summary>板の下に落ちる影の青緑</summary>
        public static readonly Color ShadowTeal = new Color(0.07f, 0.40f, 0.50f, 0.95f);

        /// <summary>文字の縁取り・暗い帯に使う、濃い青緑</summary>
        public static readonly Color DeepTeal = new Color(0.02f, 0.18f, 0.25f, 1f);

        /// <summary>水色の帯（案内・ボタン）</summary>
        public static readonly Color Azure = new Color(0.04f, 0.565f, 1.00f, 1f);

        /// <summary>水色の帯の濃い色（選んでいないボタンなど）</summary>
        public static readonly Color AzureDark = new Color(0.00f, 0.376f, 0.757f, 1f);

        /// <summary>オレンジ（スタート・選んでいるボタン）</summary>
        public static readonly Color Orange = new Color(1.00f, 0.624f, 0.00f, 1f);

        /// <summary>オレンジの暗い色（まだスタートできない時）</summary>
        public static readonly Color OrangeDim = new Color(0.54f, 0.35f, 0.07f, 1f);

        /// <summary>矢印の薄紫</summary>
        public static readonly Color Lavender = new Color(0.62f, 0.63f, 1.00f, 1f);

        /// <summary>準備完了の緑</summary>
        public static readonly Color ReadyGreen = new Color(0.18f, 0.74f, 0.34f, 1f);

        /// <summary>画面の地面の色（暗い灰緑。背景の画像の元の色）</summary>
        public static readonly Color Ground = new Color(0.28f, 0.31f, 0.31f, 1f);

        /// <summary>板の傾き（度）。反時計回りが正で、右側が少し高くなる（タンクモードの板と同じ向き）</summary>
        public const float Tilt = 2.4f;
    }


    // =====================================================================
    // ■ フォント（日本語が出せるか）
    // =====================================================================
    //
    // TextMeshPro の標準フォント（LiberationSans）には日本語の文字が入っていない。
    // そのため「最下位確定」などの日本語は、日本語が出せるフォントがある時だけ使い、
    // 無い時は英語の表示にする（四角い□が並ぶのを防ぐ）。
    //
    // 日本語フォントの探し方（上から順に試す）
    //   ① Inspector で指定されたフォント（日本語の文字が入っているか調べる）
    //   ② パソコンに入っている日本語フォント（Yu Gothic・Meiryo・Hiragino など）
    //   ③ どちらも無ければ、英語の表示にする
    //
    // =====================================================================

    /// <summary>
    /// 画面に使うフォントの組。
    /// </summary>
    public sealed class RaceFonts
    {
        // 日本語が出せるか調べるための文字（ひらがな・漢字）
        private const string ProbeText = "あ位確";

        // パソコンに入っている日本語フォントの名前（Windows → Mac → Linux の順）
        private static readonly string[] SystemFamilies = new string[]
        {
            "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS UI Gothic", "MS Gothic",
            "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Hiragino Kaku Gothic Pro",
            "Noto Sans CJK JP", "Noto Sans JP", "IPAexGothic", "IPAGothic", "TakaoGothic", "VL Gothic"
        };

        // 何度も探さないための記録
        private static bool s_SystemSearched;
        private static bool s_SystemFontWasFound;  // 日本語フォントを見つけたことがあるか
        private static TMP_FontAsset s_SystemFont;

        /// <summary>数字や英語の見出しに使うフォント（null ならTextMeshProの標準）</summary>
        public TMP_FontAsset Main;

        /// <summary>日本語に使うフォント（null ＝ 日本語を出せない）</summary>
        public TMP_FontAsset Japanese;

        /// <summary>日本語を出せるか</summary>
        public bool HasJapanese
        {
            get { return Japanese != null; }
        }

        /// <summary>
        /// 日本語を出せる時は日本語、出せない時は英語を返す。
        /// </summary>
        public string Pick(string english, string japanese)
        {
            return HasJapanese ? japanese : english;
        }

        /// <summary>日本語の文字に使うフォント（日本語が出せない時は Main）</summary>
        public TMP_FontAsset ForJapanese
        {
            get { return Japanese != null ? Japanese : Main; }
        }

        /// <summary>
        /// フォントの組を作る。
        /// </summary>
        /// <param name="preferred">Inspector で指定されたフォント（無ければ null）</param>
        public static RaceFonts Resolve(TMP_FontAsset preferred)
        {
            RaceFonts fonts = new RaceFonts();

            fonts.Main = preferred;

            if (SupportsJapanese(preferred))
            {
                fonts.Japanese = preferred;
            }
            else
            {
                fonts.Japanese = FindSystemFont();
            }

            return fonts;
        }

        /// <summary>
        /// このフォントで、日本語（ひらがな・漢字）が出せるか調べる。
        /// 動的フォント（必要な文字を後から足せるフォント）なら、文字を足して調べる。
        /// </summary>
        public static bool SupportsJapanese(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            for (int i = 0; i < ProbeText.Length; i++)
            {
                // searchFallbacks ＝ 予備のフォントも探す／tryAddCharacter ＝ 動的フォントなら文字を足す
                if (!font.HasCharacter(ProbeText[i], true, true))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// パソコンに入っている日本語フォントを探して、TextMeshPro用のフォントを作る。
        /// 見つからなければ null。結果は覚えておくので、2回目からは探さない。
        /// </summary>
        public static TMP_FontAsset FindSystemFont()
        {
            // 前に作ったフォントが、まだ使える状態ならそれを使う
            if (s_SystemFont != null)
            {
                return s_SystemFont;
            }

            // 前に探して、見つからなかった：もう探さない
            if (s_SystemSearched && !s_SystemFontWasFound)
            {
                return null;
            }

            // ここに来るのは「初めて探す時」か、「作ったフォントが壊れた時（シーンの切り替えなどで破棄された）」
            s_SystemSearched = true;
            s_SystemFontWasFound = false;
            s_SystemFont = null;

            for (int i = 0; i < SystemFamilies.Length; i++)
            {
                TMP_FontAsset font = null;

                try
                {
                    // "Regular" で探すと、その名前の字体が無い時は、同じファミリーの最初の字体になる
                    font = TMP_FontAsset.CreateFontAsset(SystemFamilies[i], "Regular");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(
                        "RaceFonts : フォント「" + SystemFamilies[i] +
                        "」を作れませんでした : " + e.Message);
                }

                if (font != null && SupportsJapanese(font))
                {
                    s_SystemFont = font;
                    s_SystemFontWasFound = true;

                    return font;
                }
            }

            Debug.Log(
                "RaceFonts : 日本語のフォントが見つからなかったため、英語の表示にします。" +
                "日本語で表示したい時は、日本語の文字が入った TextMeshPro のフォントを、" +
                "Race Game Manager の Font に設定してください。");

            return null;
        }
    }


    // =====================================================================
    // ■ メニューの操作
    // =====================================================================
    //
    // 車種選択・順位発表のメニューは、3種類の入力装置を同じ決まりで扱う
    // （タンクモードの参加画面と同じ操作）。
    //
    //   装置                 決定        取り消し        左右        上下
    //   WASD（番号 1）       Space       Q               A / D       W / S
    //   矢印キー（番号 2）   Enter       Backspace       ←  / →      ↑ / ↓
    //   ゲームパッド（10〜） A（南）     B（東）         十字キー・左スティック
    //
    // 入力装置の番号は InputReader.ControlIndex と同じ
    // （1 ＝ WASD／2 ＝ 矢印キー／10 + n ＝ n 台目のゲームパッド）。
    //
    // =====================================================================

    /// <summary>
    /// 1つの入力装置が、このフレームに行った操作。
    /// </summary>
    public struct RaceMenuEvent
    {
        /// <summary>入力装置の番号（1 ＝ WASD／2 ＝ 矢印キー／10 以上 ＝ ゲームパッド）</summary>
        public int ControlIndex;

        /// <summary>決定ボタンが押された</summary>
        public bool Confirm;

        /// <summary>取り消しボタンが押された</summary>
        public bool Cancel;

        /// <summary>左右の操作（-1 ＝ 左／0 ＝ なし／+1 ＝ 右）</summary>
        public int Horizontal;

        /// <summary>上下の操作（+1 ＝ 上／0 ＝ なし／-1 ＝ 下）</summary>
        public int Vertical;

        /// <summary>何か操作があったか</summary>
        public bool Any
        {
            get { return Confirm || Cancel || Horizontal != 0 || Vertical != 0; }
        }
    }

    /// <summary>
    /// メニューの操作を読み取る。
    /// </summary>
    public static class RaceMenuInput
    {
        // スティックを倒したとみなす強さ／戻したとみなす強さ
        // （倒し続けている間に何度も反応しないよう、2つの値で区切る）
        private const float StickPress = 0.6f;
        private const float StickRelease = 0.3f;

        // ゲームパッドごとの、スティックの状態（-1 / 0 / +1）。倒しっぱなしの間は同じ値のまま
        private static readonly Dictionary<int, int> s_StickX = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> s_StickY = new Dictionary<int, int>();

        /// <summary>
        /// 入力装置の表示名（「WASD」「ARROW KEYS」「GAMEPAD 1」など）。
        /// </summary>
        public static string DeviceLabel(int controlIndex)
        {
            if (controlIndex == 1) return "WASD";
            if (controlIndex == 2) return "ARROW KEYS";
            if (controlIndex >= 10) return "GAMEPAD " + (controlIndex - 9);

            return "AUTO";
        }

        /// <summary>決定ボタンの名前（画面の案内に出す）</summary>
        public static string ConfirmName(int controlIndex)
        {
            if (controlIndex == 1) return "SPACE";
            if (controlIndex == 2) return "ENTER";

            return "A";
        }

        /// <summary>取り消しボタンの名前（画面の案内に出す）</summary>
        public static string CancelName(int controlIndex)
        {
            if (controlIndex == 1) return "Q";
            if (controlIndex == 2) return "BACKSPACE";

            return "B";
        }

        /// <summary>左右の操作の名前（画面の案内に出す）</summary>
        public static string MoveName(int controlIndex)
        {
            if (controlIndex == 1) return "A / D";
            if (controlIndex == 2) return "LEFT / RIGHT";

            return "D-PAD";
        }

        /// <summary>
        /// 今のフレームに、どの入力装置が何を操作したかを調べる。
        /// 操作があった装置の分だけ、events に入れる（events は最初に空にする）。
        /// </summary>
        /// <param name="events">結果を入れるリスト</param>
        public static void Poll(List<RaceMenuEvent> events)
        {
            events.Clear();

            // ---- キーボード ----
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                // WASD（決定 Space／取り消し Q）
                Add(
                    events,
                    1,
                    keyboard.spaceKey.wasPressedThisFrame,
                    keyboard.qKey.wasPressedThisFrame,
                    Axis(keyboard.aKey.wasPressedThisFrame, keyboard.dKey.wasPressedThisFrame),
                    Axis(keyboard.sKey.wasPressedThisFrame, keyboard.wKey.wasPressedThisFrame));

                // 矢印キー（決定 Enter／取り消し Backspace）
                Add(
                    events,
                    2,
                    keyboard.enterKey.wasPressedThisFrame ||
                        keyboard.numpadEnterKey.wasPressedThisFrame,
                    keyboard.backspaceKey.wasPressedThisFrame,
                    Axis(
                        keyboard.leftArrowKey.wasPressedThisFrame,
                        keyboard.rightArrowKey.wasPressedThisFrame),
                    Axis(
                        keyboard.downArrowKey.wasPressedThisFrame,
                        keyboard.upArrowKey.wasPressedThisFrame));
            }

            // ---- ゲームパッド ----
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];

                if (pad == null)
                {
                    continue;
                }

                int horizontal =
                    Axis(pad.dpad.left.wasPressedThisFrame, pad.dpad.right.wasPressedThisFrame);

                int vertical =
                    Axis(pad.dpad.down.wasPressedThisFrame, pad.dpad.up.wasPressedThisFrame);

                // 十字キーが押されていなければ、左スティックを見る
                Vector2 stick = pad.leftStick.ReadValue();

                if (horizontal == 0)
                {
                    horizontal = StickEdge(s_StickX, i, stick.x);
                }
                else
                {
                    StickEdge(s_StickX, i, stick.x);
                }

                if (vertical == 0)
                {
                    vertical = StickEdge(s_StickY, i, stick.y);
                }
                else
                {
                    StickEdge(s_StickY, i, stick.y);
                }

                Add(
                    events,
                    10 + i,
                    pad.buttonSouth.wasPressedThisFrame,
                    pad.buttonEast.wasPressedThisFrame,
                    horizontal,
                    vertical);
            }
        }

        /// <summary>
        /// 操作があった時だけ、リストに足す。
        /// </summary>
        private static void Add(
            List<RaceMenuEvent> events,
            int controlIndex,
            bool confirm,
            bool cancel,
            int horizontal,
            int vertical)
        {
            if (!confirm && !cancel && horizontal == 0 && vertical == 0)
            {
                return;
            }

            RaceMenuEvent e = new RaceMenuEvent();

            e.ControlIndex = controlIndex;
            e.Confirm = confirm;
            e.Cancel = cancel;
            e.Horizontal = horizontal;
            e.Vertical = vertical;

            events.Add(e);
        }

        /// <summary>
        /// 2つのボタンから、方向（-1 ／ 0 ／ +1）を決める。
        /// 同時に押されたら 0（打ち消し合う）。
        /// </summary>
        private static int Axis(bool negative, bool positive)
        {
            if (negative == positive)
            {
                return 0;
            }

            return positive ? 1 : -1;
        }

        /// <summary>
        /// スティックを倒した瞬間だけ方向を返す（倒しっぱなしの間は 0）。
        /// </summary>
        /// <param name="latch">ゲームパッドごとの記録</param>
        /// <param name="padIndex">ゲームパッドの番号</param>
        /// <param name="value">スティックの値（-1〜1）</param>
        private static int StickEdge(Dictionary<int, int> latch, int padIndex, float value)
        {
            int state;

            if (!latch.TryGetValue(padIndex, out state))
            {
                state = 0;
            }

            // 戻した：記録を消す
            if (Mathf.Abs(value) < StickRelease)
            {
                latch[padIndex] = 0;

                return 0;
            }

            // まだ倒したままで、前回までに反応済み
            if (state != 0 || Mathf.Abs(value) < StickPress)
            {
                return 0;
            }

            int direction = value > 0f ? 1 : -1;

            latch[padIndex] = direction;

            return direction;
        }
    }


    // =====================================================================
    // ■ 動きの付け方
    // =====================================================================

    /// <summary>
    /// アニメーションの時間の進め方（0〜1 の値を、動きの値に直す）。
    /// </summary>
    public static class RaceEase
    {
        /// <summary>はじめ速く、終わりゆっくり止まる</summary>
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);

            float u = 1f - t;

            return 1f - u * u * u;
        }

        /// <summary>少し行き過ぎてから、ぴたっと戻る（ポンと出る感じ）</summary>
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            float u = t - 1f;

            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>はじめも終わりもゆっくり</summary>
        public static float InOutSine(float t)
        {
            t = Mathf.Clamp01(t);

            return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        }

        /// <summary>
        /// 一瞬だけ大きくなってから戻る山形の値（0 → 1 → 0）。
        /// 順位の数字が変わった時の「ポン」に使う。
        /// </summary>
        public static float Pulse(float t)
        {
            t = Mathf.Clamp01(t);

            return Mathf.Sin(t * Mathf.PI);
        }
    }


    // =====================================================================
    // ■ 板（影つきの面）
    // =====================================================================

    /// <summary>
    /// 影つきの板（カード・帯・ボタン）。RaceUiKit.CreateCard / CreatePlate が作る。
    /// 中身（文字など）は Root の下に置く。
    /// </summary>
    public sealed class RacePlate
    {
        /// <summary>板の本体。大きさ・位置はこれで決め、中身はこの下に置く</summary>
        public RectTransform Root;

        /// <summary>下に落ちる影（色を変えると影の色が変わる）</summary>
        public Image Shadow;

        /// <summary>板の面（色を変えると板の色が変わる）</summary>
        public Image Face;
    }


    // =====================================================================
    // ■ UI部品を作る道具
    // =====================================================================

    /// <summary>
    /// UI（Canvas・画像・文字）をコードで作る道具と、丸や三角などの画像。
    /// 作った画像は覚えておくので、何度呼んでも同じものを返す。
    /// </summary>
    public static class RaceUiKit
    {
        /// <summary>画面の基準の大きさ（Canvas Scaler の Reference Resolution）</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // 作った画像（覚えておく）
        private static Sprite s_White;
        private static Sprite s_Rounded;
        private static Sprite s_Circle;
        private static Sprite s_Triangle;
        private static Sprite s_CarIcon;


        // =================================================================
        // ■ Canvas・RectTransform
        // =================================================================

        /// <summary>
        /// 画面全体に重ねて出す Canvas を作る（Screen Space - Overlay）。
        /// 大きさは、1920 × 1080 を基準に画面に合わせる。
        /// </summary>
        /// <param name="name">GameObject の名前</param>
        /// <param name="parent">親（無ければ null）</param>
        /// <param name="sortingOrder">表示の順番（大きいほど手前）</param>
        public static Canvas CreateOverlayCanvas(string name, Transform parent, int sortingOrder)
        {
            return CreateOverlayCanvas(name, parent, sortingOrder, false);
        }

        /// <summary>
        /// 画面全体に重ねて出す Canvas を作る（Screen Space - Overlay）。
        /// </summary>
        /// <param name="fitWhole">
        /// true なら、1920 × 1080 の範囲が、どんな縦横比の画面でも、必ず全部見えるようにする
        /// （横長・縦長の画面では、余った部分が左右・上下にできる）。
        /// 車種選択や結果発表のように、決まった大きさの画面を、はみ出さずに出したい時に使う。
        /// false なら、幅と高さの真ん中の倍率に合わせる（レース中の表示向き）
        /// </param>
        public static Canvas CreateOverlayCanvas(string name, Transform parent, int sortingOrder, bool fitWhole)
        {
            GameObject canvasObject = new GameObject(name);

            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            Canvas canvas = canvasObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode =
                fitWhole ? CanvasScaler.ScreenMatchMode.Expand : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>
        /// 空の UI オブジェクト（RectTransform）を作る。
        /// </summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));

            RectTransform rect = go.GetComponent<RectTransform>();

            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            return rect;
        }

        /// <summary>親の枠いっぱいに広げる。</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 親の枠の割合（0〜1）で、置く範囲を決める。画面分割の1区画に合わせる時などに使う。
        /// </summary>
        public static void SetNormalizedRect(RectTransform rect, Rect area)
        {
            rect.anchorMin = new Vector2(area.xMin, area.yMin);
            rect.anchorMax = new Vector2(area.xMax, area.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 親の枠の1点（anchor）を基準に、大きさと位置を決める。
        /// </summary>
        /// <param name="anchor">基準の点（0〜1。左下が (0,0)、右上が (1,1)）</param>
        /// <param name="pivot">自分の中心にする点（0〜1）</param>
        /// <param name="position">基準の点からのずれ</param>
        /// <param name="size">大きさ</param>
        public static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }


        // =================================================================
        // ■ 画像・文字
        // =================================================================

        /// <summary>
        /// 画像（Image）を作る。クリックは受け付けない（raycastTarget を切る）。
        /// </summary>
        /// <param name="sprite">使う画像（null なら白い四角）</param>
        /// <param name="color">色</param>
        /// <param name="sliced">角丸の画像を、角を保ったまま伸ばすか</param>
        public static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            bool sliced)
        {
            RectTransform rect = CreateRect(name, parent);

            Image image = rect.gameObject.AddComponent<Image>();

            image.sprite = sprite != null ? sprite : WhiteSprite();
            image.color = color;
            image.raycastTarget = false;

            if (sliced)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>
        /// 角丸の四角を作る。
        /// </summary>
        /// <param name="radius">角の丸さ（画面の基準の大きさでのピクセル。標準の画像は24）</param>
        public static Image CreatePanel(string name, Transform parent, Color color, float radius)
        {
            Image image = CreateImage(name, parent, RoundedSprite(), color, true);

            // 標準の画像の角は24。それより小さい角にしたい時は、割合を変える
            image.pixelsPerUnitMultiplier = Mathf.Max(0.1f, 24f / Mathf.Max(1f, radius));

            return image;
        }

        /// <summary>
        /// 文字（TextMeshPro）を作る。折り返さず、枠からはみ出してもそのまま表示する。
        /// </summary>
        /// <param name="font">フォント（null ならTextMeshProの標準）</param>
        /// <param name="fontSize">文字の大きさ</param>
        /// <param name="color">色</param>
        /// <param name="alignment">文字の寄せ方</param>
        /// <param name="style">太字などの装飾</param>
        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style)
        {
            RectTransform rect = CreateRect(name, parent);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = false;
            text.richText = false;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// 文字に縁取りを付ける。
        /// </summary>
        /// <param name="width">縁の太さ（0〜1。0.2 くらいが読みやすい）</param>
        /// <param name="color">縁の色</param>
        public static void SetOutline(TextMeshProUGUI text, float width, Color color)
        {
            text.outlineWidth = width;
            text.outlineColor = color;
        }

        /// <summary>
        /// 文字が枠に収まらない時に、収まるまで小さくする（大きさは minSize 〜 maxSize の間）。
        /// 長い名前や、翻訳で長くなった文字が、枠からはみ出さないための保険。
        /// </summary>
        /// <param name="minSize">一番小さくする大きさ</param>
        /// <param name="maxSize">一番大きい大きさ（枠に余裕がある時の大きさ）</param>
        public static void FitText(TextMeshProUGUI text, float minSize, float maxSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = maxSize;
        }

        /// <summary>
        /// Graphic（画像・文字）の不透明度だけを変える。
        /// </summary>
        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;

            color.a = alpha;

            graphic.color = color;
        }

        /// <summary>
        /// 区画の大きさ（画面の基準の大きさでのピクセル）から、中身の拡大率を決める。
        /// 高さ 540 × 幅 720 を基準の大きさとして、その区画に収まる最大の倍率を返す。
        /// （画面が大きい1人プレイでは大きく、4分割の小さい区画では標準の大きさになる）
        /// </summary>
        public static float FitScale(Vector2 areaSize)
        {
            if (areaSize.x <= 0f || areaSize.y <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.3f, Mathf.Min(areaSize.y / 540f, areaSize.x / 720f));
        }


        // =================================================================
        // ■ 画像（プログラムで作る）
        // =================================================================

        /// <summary>白い小さな四角（色だけ付けたい時に使う）</summary>
        public static Sprite WhiteSprite()
        {
            if (s_White == null)
            {
                Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);

                Color32[] pixels = new Color32[16];

                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;

                s_White =
                    Sprite.Create(
                        texture,
                        new Rect(0f, 0f, 4f, 4f),
                        new Vector2(0.5f, 0.5f),
                        100f);
            }

            return s_White;
        }

        /// <summary>
        /// 角丸の四角（9分割。伸ばしても角の丸さは変わらない）。
        /// 角の丸さは 24 ピクセル。
        /// </summary>
        public static Sprite RoundedSprite()
        {
            if (s_Rounded == null)
            {
                const int size = 64;
                const float radius = 24f;

                s_Rounded =
                    MakeSprite(
                        size,
                        size,
                        delegate (float x, float y)
                        {
                            // 四角の中心からの位置
                            float px = Mathf.Abs(x - size * 0.5f);
                            float py = Mathf.Abs(y - size * 0.5f);

                            // 角丸の四角までの距離（中にあるほど小さい。0が縁）
                            float qx = px - (size * 0.5f - radius);
                            float qy = py - (size * 0.5f - radius);

                            float outside =
                                Mathf.Sqrt(
                                    Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                    Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));

                            float distance =
                                outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                            return Mathf.Clamp01(0.5f - distance);
                        },
                        new Vector4(radius, radius, radius, radius));
            }

            return s_Rounded;
        }

        /// <summary>丸</summary>
        public static Sprite CircleSprite()
        {
            if (s_Circle == null)
            {
                const int size = 64;

                s_Circle =
                    MakeSprite(
                        size,
                        size,
                        delegate (float x, float y)
                        {
                            float dx = x - size * 0.5f;
                            float dy = y - size * 0.5f;

                            float distance = Mathf.Sqrt(dx * dx + dy * dy) - (size * 0.5f - 1f);

                            return Mathf.Clamp01(0.5f - distance);
                        },
                        Vector4.zero);
            }

            return s_Circle;
        }

        /// <summary>右を向いた三角（左右の切り替えの矢印に使う。左向きにしたい時は x 方向の拡大率を -1 にする）</summary>
        public static Sprite TriangleSprite()
        {
            if (s_Triangle == null)
            {
                const int size = 64;

                // 三角の3つの頂点（反時計回り）
                Vector2 a = new Vector2(size * 0.22f, size * 0.10f);
                Vector2 b = new Vector2(size * 0.90f, size * 0.50f);
                Vector2 c = new Vector2(size * 0.22f, size * 0.90f);

                s_Triangle =
                    MakeSprite(
                        size,
                        size,
                        delegate (float x, float y)
                        {
                            Vector2 p = new Vector2(x, y);

                            // 3つの辺それぞれまでの距離（内側がプラス）の、いちばん小さいもの
                            float distance =
                                Mathf.Min(
                                    EdgeDistance(a, b, p),
                                    Mathf.Min(EdgeDistance(b, c, p), EdgeDistance(c, a, p)));

                            return Mathf.Clamp01(0.5f + distance);
                        },
                        Vector4.zero);
            }

            return s_Triangle;
        }

        /// <summary>
        /// 上から見た車の形（上が前）。写真の代わりに出す簡易アイコン。
        /// 車体は白、窓とタイヤは暗い灰色なので、Image の色を付けると
        /// 「その色の車」になる（窓・タイヤはその色を暗くした色）。
        /// </summary>
        public static Sprite CarIconSprite()
        {
            if (s_CarIcon == null)
            {
                const int width = 96;
                const int height = 144;

                s_CarIcon = MakeColorSprite(width, height, CarIconPixel);
            }

            return s_CarIcon;
        }

        // 車のアイコンの1ピクセルの色（x, y はピクセルの中心。左下が原点）
        private static Color32 CarIconPixel(float x, float y)
        {
            const float width = 96f;
            const float height = 144f;

            // 車体の中心からの位置
            float px = x - width * 0.5f;
            float py = y - height * 0.5f;

            // タイヤ（左右・前後の4つ）：車体の外にはみ出す暗い四角
            float wheel = 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = -1; row <= 1; row += 2)
                {
                    float wheelDistance =
                        RoundedBoxDistance(
                            px - side * 36f,
                            py - row * 40f,
                            9f,
                            17f,
                            5f);

                    wheel = Mathf.Max(wheel, Mathf.Clamp01(0.5f - wheelDistance));
                }
            }

            // 車体：前が少し細い角丸の四角
            float taper = Mathf.Lerp(0f, 8f, Mathf.Clamp01((py + 20f) / 70f));
            float bodyDistance = RoundedBoxDistance(px, py, 31f - taper, 62f, 18f);
            float body = Mathf.Clamp01(0.5f - bodyDistance);

            // 窓：車体の前寄りに置く暗い角丸の四角
            float windowDistance = RoundedBoxDistance(px, py - 12f, 20f - taper * 0.5f, 15f, 7f);
            float window = Mathf.Clamp01(0.5f - windowDistance) * body;

            // 後ろの窓
            float rearDistance = RoundedBoxDistance(px, py + 28f, 18f, 8f, 4f);
            float rear = Mathf.Clamp01(0.5f - rearDistance) * body;

            float alpha = Mathf.Max(body, wheel);

            if (alpha <= 0.001f)
            {
                return new Color32(255, 255, 255, 0);
            }

            // 明るさ：車体 255／窓 90／タイヤ 38
            float darkWindow = Mathf.Max(window, rear);
            float shade = 255f;

            shade = Mathf.Lerp(shade, 90f, darkWindow);

            // タイヤは、車体より手前ではなく奥にあるので、車体のない所だけ暗くする
            shade = Mathf.Lerp(38f, shade, body);

            byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(shade), 0, 255);
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);

            return new Color32(value, value, value, a);
        }

        // 角丸の四角までの距離（中にあるほど小さい。0 が縁）
        // halfWidth / halfHeight は半分の大きさ、radius は角の丸さ
        private static float RoundedBoxDistance(
            float x,
            float y,
            float halfWidth,
            float halfHeight,
            float radius)
        {
            float qx = Mathf.Abs(x) - (halfWidth - radius);
            float qy = Mathf.Abs(y) - (halfHeight - radius);

            float outside =
                Mathf.Sqrt(
                    Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                    Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));

            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // 線分 a→b を境にした、点 p までの距離（反時計回りの三角形で、内側がプラス）
        private static float EdgeDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 edge = b - a;

            float length = Mathf.Max(0.0001f, edge.magnitude);

            // 外積（edge × (p - a)）を長さで割ると、直線までの符号つきの距離になる
            return (edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / length;
        }

        // =================================================================
        // ■ タンクモード風の部品（傾いた板・吹き出し・矢印・ドット・背景）
        // =================================================================
        //
        // 大きさが決まっている画像（傾いた板・吹き出しなど）は、同じ指定なら同じ画像を使い回す。
        // 色は付けずに白〜灰色で作ってあるので、Image の色（RaceStyle）を付けて使う。

        // 作った画像（指定の文字列をキーにして覚えておく）
        private static readonly Dictionary<string, Sprite> s_Styled = new Dictionary<string, Sprite>();

        private static Sprite FindStyled(string key)
        {
            Sprite sprite;

            if (s_Styled.TryGetValue(key, out sprite) && sprite != null)
            {
                return sprite;
            }

            return null;
        }

        // 0〜1 の値を、0〜255 にする（範囲の外は丸める）
        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        /// <summary>
        /// 傾いた板（カード）の画像の高さ。傾けた分だけ、板の高さより少し高くなる。
        /// </summary>
        public static int CardSpriteHeight(int width, int height, float tiltDegrees)
        {
            float slope = Mathf.Tan(tiltDegrees * Mathf.Deg2Rad);

            return height + Mathf.CeilToInt(Mathf.Abs(slope) * width) + 2;
        }

        /// <summary>
        /// 傾いた板（カード）の画像。長方形を「右側が高くなるように」ずらした形で、
        /// 左右の辺は縦のまま、上下の辺だけが傾く（タンクモードの板と同じ形）。
        /// 上が少し明るく、下が少し暗い。角は丸い。9分割はしないので、大きさは作った時のまま使うこと。
        /// </summary>
        /// <param name="width">板の幅</param>
        /// <param name="height">板の高さ（傾ける前）</param>
        /// <param name="tiltDegrees">傾き（度。正で右側が高くなる）</param>
        /// <param name="radius">角の丸さ</param>
        public static Sprite CardSprite(int width, int height, float tiltDegrees, float radius)
        {
            string key =
                "card:" + width + ":" + height + ":" +
                Mathf.RoundToInt(tiltDegrees * 100f) + ":" + Mathf.RoundToInt(radius * 10f);

            Sprite cached = FindStyled(key);

            if (cached != null)
            {
                return cached;
            }

            int spriteHeight = CardSpriteHeight(width, height, tiltDegrees);

            float slope = Mathf.Tan(tiltDegrees * Mathf.Deg2Rad);

            float cx = width * 0.5f;
            float cy = spriteHeight * 0.5f;

            Sprite sprite =
                MakeColorSprite(
                    width,
                    spriteHeight,
                    delegate (float x, float y)
                    {
                        // 傾きを戻した位置で、角丸の四角までの距離を測る
                        float distance =
                            RoundedBoxDistance(
                                x - cx,
                                y - (x - cx) * slope - cy,
                                width * 0.5f,
                                height * 0.5f,
                                radius);

                        byte alpha = ToByte(0.5f - distance);

                        if (alpha == 0)
                        {
                            return new Color32(255, 255, 255, 0);
                        }

                        // 上が明るく、下が少し暗い
                        byte shade = ToByte(Mathf.Lerp(0.90f, 1f, Mathf.Clamp01(y / spriteHeight)));

                        return new Color32(shade, shade, shade, alpha);
                    });

            s_Styled[key] = sprite;

            return sprite;
        }

        /// <summary>
        /// 傾いた板（カード）を作る。影つき。
        /// 返す RacePlate の Root は板の本来の大きさ（幅 × 高さ）で、中身はここに置く。
        /// 影と面の画像は、傾けた分だけ上下にはみ出す。
        /// </summary>
        /// <param name="face">板の面の色</param>
        /// <param name="shadow">影の色</param>
        /// <param name="depth">影が下へずれる大きさ</param>
        public static RacePlate CreateCard(
            string name,
            Transform parent,
            int width,
            int height,
            float tiltDegrees,
            float radius,
            Color face,
            Color shadow,
            float depth)
        {
            RacePlate card = new RacePlate();

            card.Root = CreateRect(name, parent);

            card.Root.sizeDelta = new Vector2(width, height);

            Sprite sprite = CardSprite(width, height, tiltDegrees, radius);

            Vector2 size = new Vector2(width, CardSpriteHeight(width, height, tiltDegrees));

            Vector2 center = new Vector2(0.5f, 0.5f);

            card.Shadow = CreateImage("Shadow", card.Root, sprite, shadow, false);

            Place(card.Shadow.rectTransform, center, center, new Vector2(0f, -depth), size);

            card.Face = CreateImage("Face", card.Root, sprite, face, false);

            Place(card.Face.rectTransform, center, center, Vector2.zero, size);

            return card;
        }

        /// <summary>
        /// 斜めに切った帯・ボタンの画像。四角の左右の端を斜めにした形（タンクモードのスタートボタンや水色の帯と同じ形）。
        /// 角は少しだけ丸い。大きさは作った時のまま使うこと（9分割はしない）。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="slantLeft">左の辺の傾き（上の端が右へずれる大きさ。0なら垂直）</param>
        /// <param name="slantRight">右の辺の傾き（下の端が左へずれる大きさ。0なら垂直）</param>
        public static Sprite PlateSprite(int width, int height, float slantLeft, float slantRight)
        {
            string key =
                "plate:" + width + ":" + height + ":" +
                Mathf.RoundToInt(slantLeft * 10f) + ":" + Mathf.RoundToInt(slantRight * 10f);

            Sprite cached = FindStyled(key);

            if (cached != null)
            {
                return cached;
            }

            // 角を丸くするために、四角を内側へ縮めてから、その分だけふくらませる
            const float rounding = 3f;

            float w = width;
            float h = height;

            // 左上から時計回り（下向きがプラス）
            Vector2[] outline = new Vector2[]
            {
                new Vector2(slantLeft + rounding, rounding),
                new Vector2(w - rounding, rounding),
                new Vector2(w - slantRight - rounding, h - rounding),
                new Vector2(rounding, h - rounding)
            };

            Sprite sprite =
                MakeSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        Vector2 p = new Vector2(x, height - y);

                        return Mathf.Clamp01(0.5f - (PolygonDistance(outline, p) - rounding));
                    },
                    Vector4.zero);

            s_Styled[key] = sprite;

            return sprite;
        }

        /// <summary>
        /// 斜めに切った帯・ボタンを作る。影つき。
        /// 返す RacePlate の Root は帯の大きさで、傾きは Root の回転として付く
        /// （文字などの中身を Root の下に置くと、一緒に傾く）。位置は呼び出し側で Place する。
        /// </summary>
        /// <param name="slantLeft">左の辺の傾き（上の端が右へずれる大きさ）</param>
        /// <param name="slantRight">右の辺の傾き（下の端が左へずれる大きさ）</param>
        /// <param name="depth">影が下へずれる大きさ（0なら影なし）</param>
        /// <param name="tiltDegrees">傾き（度。正で右側が高くなる）</param>
        public static RacePlate CreatePlate(
            string name,
            Transform parent,
            Vector2 size,
            float slantLeft,
            float slantRight,
            Color face,
            Color shadow,
            float depth,
            float tiltDegrees)
        {
            RacePlate plate = new RacePlate();

            plate.Root = CreateRect(name, parent);

            plate.Root.sizeDelta = size;
            plate.Root.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);

            Sprite sprite = PlateSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), slantLeft, slantRight);

            plate.Shadow = CreateImage("Shadow", plate.Root, sprite, shadow, false);

            Stretch(plate.Shadow.rectTransform);

            plate.Shadow.rectTransform.offsetMin = new Vector2(0f, -depth);
            plate.Shadow.rectTransform.offsetMax = new Vector2(0f, -depth);

            plate.Shadow.gameObject.SetActive(depth > 0f);

            plate.Face = CreateImage("Face", plate.Root, sprite, face, false);

            Stretch(plate.Face.rectTransform);

            return plate;
        }

        // 吹き出しの画像が、本体（文字を置く四角）の外にはみ出す大きさ（左・下・右・上）。
        // 右と上には、尖った飾りが出る
        public const int BubbleLeft = 6;
        public const int BubbleBottom = 6;
        public const int BubbleRight = 66;
        public const int BubbleTop = 30;

        /// <summary>
        /// 吹き出しの画像。四角い本体の右側に、3つの尖り（上・横・下）が付いた形（タンクモードの能力表示と同じ形）。
        /// 画像の大きさは、本体の大きさに、BubbleLeft などのはみ出し分を足したもの。
        /// </summary>
        /// <param name="bodyWidth">本体の幅</param>
        /// <param name="bodyHeight">本体の高さ</param>
        public static Sprite BubbleSprite(int bodyWidth, int bodyHeight)
        {
            string key = "bubble:" + bodyWidth + ":" + bodyHeight;

            Sprite cached = FindStyled(key);

            if (cached != null)
            {
                return cached;
            }

            int width = bodyWidth + BubbleLeft + BubbleRight;
            int height = bodyHeight + BubbleTop + BubbleBottom;

            float w = bodyWidth;
            float h = bodyHeight;

            // 本体の左上を原点にした形（下向きがプラス）。時計回り
            Vector2[] outline = new Vector2[]
            {
                new Vector2(0f, 10f),            // 左上
                new Vector2(w - 4f, 0f),         // 右上
                new Vector2(w + 32f, -20f),      // 上の尖り
                new Vector2(w + 16f, 10f),       // 谷
                new Vector2(w + 58f, 38f),       // 横へ伸びる大きな尖り
                new Vector2(w + 20f, 80f),       // 谷
                new Vector2(w + 36f, 114f),      // 下の尖り
                new Vector2(w + 4f, 108f),       // 本体の右の辺に戻る
                new Vector2(w + 4f, h - 12f),    // 右下
                new Vector2(8f, h),              // 左下
            };

            // 角をほんの少し丸める（尖りの先も、少しだけ丸くなる）
            const float rounding = 2.5f;

            Sprite sprite =
                MakeSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        Vector2 p = new Vector2(x - BubbleLeft, (height - y) - BubbleTop);

                        return Mathf.Clamp01(0.5f - (PolygonDistance(outline, p) - rounding));
                    },
                    Vector4.zero);

            s_Styled[key] = sprite;

            return sprite;
        }

        /// <summary>
        /// 吹き出しを作る。返す Rect は本体の大きさ（bodySize）で、文字などの中身はここに置く。
        /// 尖りの飾りは、本体の外（右・上）にはみ出して描かれる。
        /// </summary>
        public static RectTransform CreateBubble(string name, Transform parent, Vector2 bodySize, Color color)
        {
            RectTransform root = CreateRect(name, parent);

            root.sizeDelta = bodySize;

            Image image =
                CreateImage(
                    "BubbleImage",
                    root,
                    BubbleSprite(Mathf.RoundToInt(bodySize.x), Mathf.RoundToInt(bodySize.y)),
                    color,
                    false);

            Stretch(image.rectTransform);

            image.rectTransform.offsetMin = new Vector2(-BubbleLeft, -BubbleBottom);
            image.rectTransform.offsetMax = new Vector2(BubbleRight, BubbleTop);

            return root;
        }

        /// <summary>
        /// 右を向いた矢印（先が大きな三角で、後ろへ伸びる帯が、だんだん消えていく）。
        /// 左向きにしたい時は x 方向の拡大率を -1 にする。
        /// </summary>
        public static Sprite ArrowSprite()
        {
            Sprite cached = FindStyled("arrow");

            if (cached != null)
            {
                return cached;
            }

            const int width = 176;
            const int height = 60;

            // 先の三角の長さ
            const float headLength = 60f;

            // 先の三角（反時計回り：先端 → 左上 → 左下）
            Vector2 tip = new Vector2(width - 1.5f, height * 0.5f);
            Vector2 upper = new Vector2(width - headLength, height - 2f);
            Vector2 lower = new Vector2(width - headLength, 2f);

            // 帯は、先の三角の中で終わる
            const float bodyEnd = width - headLength + 8f;
            const float bodyHalf = 12f;

            Sprite sprite =
                MakeSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        Vector2 p = new Vector2(x, y);

                        float head =
                            Mathf.Clamp01(
                                0.5f +
                                Mathf.Min(
                                    EdgeDistance(tip, upper, p),
                                    Mathf.Min(EdgeDistance(upper, lower, p), EdgeDistance(lower, tip, p))));

                        // 帯：先に近い所は濃く、後ろへいくほど消えていく
                        float across = Mathf.Clamp01(0.5f - (Mathf.Abs(y - height * 0.5f) - bodyHalf));
                        float along = Mathf.Clamp01(x / (width - headLength));
                        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(along * 1.6f));

                        float body = x <= bodyEnd ? across * fade : 0f;

                        return Mathf.Max(head, body);
                    },
                    Vector4.zero);

            s_Styled["arrow"] = sprite;

            return sprite;
        }

        /// <summary>
        /// ドット模様の丸（ハーフトーン）。細かい点が斜めに並び、中心ほど点が大きく、ふちへ向かって小さくなって消える。
        /// 横長の楕円で、車の足もとに敷く。
        /// </summary>
        public static Sprite HalftoneSprite()
        {
            Sprite cached = FindStyled("halftone");

            if (cached != null)
            {
                return cached;
            }

            const int width = 320;
            const int height = 176;

            // 点と点の間隔
            const float cell = 11f;

            Sprite sprite =
                MakeSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        // 楕円の中心から、ふちまでを 1 とした距離
                        float nx = (x - width * 0.5f) / (width * 0.5f);
                        float ny = (y - height * 0.5f) / (height * 0.5f);

                        float u = Mathf.Sqrt(nx * nx + ny * ny);

                        if (u >= 1f)
                        {
                            return 0f;
                        }

                        // 斜めに並んだ点の格子（印刷のハーフトーンと同じ並び）
                        float a = (x + y) * 0.70710678f;
                        float b = (x - y) * 0.70710678f;

                        float da = Mathf.Repeat(a, cell) - cell * 0.5f;
                        float db = Mathf.Repeat(b, cell) - cell * 0.5f;

                        float distance = Mathf.Sqrt(da * da + db * db);

                        // 中心ほど大きい点（点どうしはくっつかない大きさまで）
                        float radius = 3.3f * Mathf.Pow(1f - u, 0.6f);

                        return Mathf.Clamp01(radius - distance + 0.5f);
                    },
                    Vector4.zero);

            s_Styled["halftone"] = sprite;

            return sprite;
        }

        /// <summary>
        /// 背景の画像（暗い灰緑の地面に、うっすらクレーター）。16:9 で作ってあり、画面いっぱいに引き伸ばして使う。
        /// 白黒ではなく色つきなので、Image の色は白（不透明度だけ変える）にする。
        /// </summary>
        public static Sprite GroundSprite()
        {
            Sprite cached = FindStyled("ground");

            if (cached != null)
            {
                return cached;
            }

            const int width = 480;
            const int height = 270;

            // クレーターの位置と大きさ（毎回同じ形になるように、決まった並びの乱数で作る）
            const int craterCount = 16;

            float[] craters = new float[craterCount * 3];

            uint seed = 20241030u;

            for (int i = 0; i < craterCount; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float rx = ((seed >> 8) & 0xFFFF) / 65535f;

                seed = seed * 1664525u + 1013904223u;
                float ry = ((seed >> 8) & 0xFFFF) / 65535f;

                seed = seed * 1664525u + 1013904223u;
                float rr = ((seed >> 8) & 0xFFFF) / 65535f;

                craters[i * 3] = rx * width;
                craters[i * 3 + 1] = ry * height;
                craters[i * 3 + 2] = 10f + rr * rr * 46f;
            }

            Color ground = RaceStyle.Ground;

            Sprite sprite =
                MakeColorSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        float shade = 0f;

                        for (int i = 0; i < craterCount; i++)
                        {
                            float dx = x - craters[i * 3];
                            float dy = (y - craters[i * 3 + 1]) * 1.25f;

                            float radius = craters[i * 3 + 2];

                            float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                            // 中はへこんで暗く、ふちは盛り上がって明るい
                            if (d < 1f)
                            {
                                shade -= 0.07f * (1f - d * d);
                            }

                            float rim = (d - 1f) / 0.11f;

                            shade += 0.085f * Mathf.Exp(-rim * rim);
                        }

                        // ざらつき（ピクセルごとの、ごく小さな明るさの違い）
                        uint n = (uint)(Mathf.FloorToInt(x) * 73856093) ^ (uint)(Mathf.FloorToInt(y) * 19349663);

                        n = n * 2654435761u;

                        shade += (((n >> 12) & 0xFF) / 255f - 0.5f) * 0.035f;

                        // 画面のすみは暗く
                        float vx = (x / width - 0.5f) * 2f;
                        float vy = (y / height - 0.5f) * 2f;

                        float vignette = 1f - 0.20f * (vx * vx * 0.6f + vy * vy * 0.9f);

                        float k = (1f + shade) * vignette;

                        return new Color32(
                            ToByte(ground.r * k),
                            ToByte(ground.g * k),
                            ToByte(ground.b * k),
                            255);
                    });

            s_Styled["ground"] = sprite;

            return sprite;
        }

        /// <summary>
        /// 下へいくほど濃くなる帯の画像（白。上は透明、いちばん下で不透明）。
        /// 画面の下に敷いて、青緑の色を付けて使う（タンクモードの参加画面の下の帯）。
        /// </summary>
        public static Sprite BandSprite()
        {
            Sprite cached = FindStyled("band");

            if (cached != null)
            {
                return cached;
            }

            const int width = 4;
            const int height = 128;

            Sprite sprite =
                MakeSprite(
                    width,
                    height,
                    delegate (float x, float y)
                    {
                        // y は下から数える。上（t = 1）で透明、下（t = 0）で不透明
                        float t = Mathf.Clamp01(y / height);

                        return Mathf.Pow(1f - t, 1.6f);
                    },
                    Vector4.zero);

            s_Styled["band"] = sprite;

            return sprite;
        }

        // 多角形までの距離（外側がプラス、内側がマイナス）
        private static float PolygonDistance(Vector2[] polygon, Vector2 p)
        {
            float best = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                // 辺（線分 a-b）までの距離
                Vector2 edge = b - a;

                float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(0.0001f, Vector2.Dot(edge, edge)));

                float distance = (p - (a + edge * t)).magnitude;

                if (distance < best)
                {
                    best = distance;
                }

                // 内側か：右へ伸ばした線が辺と交わる回数が、奇数なら内側
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside ? -best : best;
        }

        /// <summary>
        /// 白地にアルファ（透明度）だけを計算した画像を作る。
        /// </summary>
        /// <param name="alphaAt">ピクセルの中心の位置から、アルファ（0〜1）を返す関数</param>
        /// <param name="border">9分割の境目（左・下・右・上のピクセル数。使わなければゼロ）</param>
        private static Sprite MakeSprite(
            int width,
            int height,
            System.Func<float, float, float> alphaAt,
            Vector4 border)
        {
            return MakeColorSpriteWithBorder(
                width,
                height,
                delegate (float x, float y)
                {
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaAt(x, y) * 255f), 0, 255);

                    return new Color32(255, 255, 255, a);
                },
                border);
        }

        private static Sprite MakeColorSprite(
            int width,
            int height,
            System.Func<float, float, Color32> colorAt)
        {
            return MakeColorSpriteWithBorder(width, height, colorAt, Vector4.zero);
        }

        private static Sprite MakeColorSpriteWithBorder(
            int width,
            int height,
            System.Func<float, float, Color32> colorAt,
            Vector4 border)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // ピクセルの中心の位置で計算する
                    pixels[y * width + x] = colorAt(x + 0.5f, y + 0.5f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
        }
    }
}