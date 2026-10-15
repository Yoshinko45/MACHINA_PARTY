using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ ミニマップ用スプライトの自動生成
    // =====================================================
    //
    // 画像ファイルを用意しなくても、ミニマップのアイコンが
    // 表示できるようにするためのクラス。
    // （GameUIHandlerの参加カーソルと同じく、Texture2Dを
    // 　コードで描いてSpriteにして使い回す）
    //
    // ・Circle  … 塗りつぶしの丸（アイコン本体）
    // ・Ring    … 丸の縁取り
    // ・Arrow   … 進行方向を示す三角（上向き）
    // ・Checker … スタート／ゴール位置の市松模様
    //
    // =====================================================

    /// <summary>
    /// ミニマップのアイコン用スプライト（丸・縁・三角・市松模様）を、コードで描いて渡すクラス。
    /// 画像ファイルは要らない。各スプライトは、初めて使われた時に1回だけ作り、
    /// 以降は同じものを使い回す。
    /// どれも白で描いてあり、色は使う側（Image.color）で付ける（白 × 色 ＝ その色になる）。
    /// </summary>
    public static class MinimapSprites
    {
        // 生成済みスプライトの置き場（キャッシュ）。
        // null の間は、まだ作っていない
        private static Sprite s_Circle;
        private static Sprite s_Ring;
        private static Sprite s_Arrow;
        private static Sprite s_Checker;

        // テクスチャの一辺（ピクセル）。
        // アイコンは画面上で最大でも50ピクセル前後で使うので、64あれば拡大されず、
        // 縁がなめらかに見える
        private const int SIZE = 64;


        // =====================================================
        // ■ 塗りつぶしの丸
        // =====================================================

        /// <summary>
        /// 塗りつぶしの丸。アイコン本体（車の色の丸）に使う。
        /// </summary>
        public static Sprite Circle
        {
            get
            {
                // 作成済みなら、それを返す。
                // （Unityのnull判定を使うので、破棄されていても作り直せる）
                if (s_Circle != null)
                {
                    return s_Circle;
                }

                // 全ピクセルの色を入れる配列（添字 ＝ y * SIZE + x。左下が(0, 0)）
                Color32[] pixels = new Color32[SIZE * SIZE];

                // 丸の中心と半径（ピクセル）。
                // 半径は画像の端より1ピクセル小さくして、縁のなめらかな部分が
                // 画像からはみ出さないようにする
                float center = SIZE / 2f;
                float radius = SIZE / 2f - 1f;

                // 全ピクセルを順に調べて、丸の内側なら不透明、外側なら透明にする
                for (int y = 0; y < SIZE; y++)
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        // このピクセルの中心（+0.5）が、画像の中心からどれだけ離れているか（dist）
                        float dx = x + 0.5f - center;
                        float dy = y + 0.5f - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // 縁を1ピクセル分なだらかにして、ギザギザを防ぐ。
                        // 半径より内側は1（不透明）、外側は0（透明）、ちょうど縁の上は0.5（半透明）
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);

                        // 白色で、不透明度だけ alpha にして書き込む
                        pixels[y * SIZE + x] = ToWhite(alpha);
                    }
                }

                // 配列からSpriteを作って保存する
                // （Bilinear ＝ 拡大縮小の時に、周りの色をなめらかに混ぜる）
                s_Circle = MakeSprite(pixels, SIZE, FilterMode.Bilinear);
                return s_Circle;
            }
        }


        // =====================================================
        // ■ 丸の縁取り
        // =====================================================

        /// <summary>
        /// 丸の縁取り（ドーナツ形）。アイコンの縁の色に使う。
        /// </summary>
        public static Sprite Ring
        {
            get
            {
                // 作成済みなら、それを返す
                if (s_Ring != null)
                {
                    return s_Ring;
                }

                Color32[] pixels = new Color32[SIZE * SIZE];

                // 丸の中心と、外側の半径 outer（Circle の radius と同じ）
                float center = SIZE / 2f;
                float outer = SIZE / 2f - 1f;

                // 内側の半径（穴の大きさ）。外半径の72％にするので、
                // 縁の太さは外半径の約28％になる
                float inner = outer * 0.72f;

                for (int y = 0; y < SIZE; y++)
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        float dx = x + 0.5f - center;
                        float dy = y + 0.5f - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // outerAlpha ＝ 外側の丸の内側だけ1　／　innerAlpha ＝ 内側の丸の外側だけ1
                        // 掛け合わせると、2つの丸の間（ドーナツ形）だけが不透明になる
                        float outerAlpha = Mathf.Clamp01(outer - dist + 0.5f);
                        float innerAlpha = Mathf.Clamp01(dist - inner + 0.5f);

                        pixels[y * SIZE + x] =
                            ToWhite(outerAlpha * innerAlpha);
                    }
                }

                s_Ring = MakeSprite(pixels, SIZE, FilterMode.Bilinear);
                return s_Ring;
            }
        }


        // =====================================================
        // ■ 進行方向の三角（上向き）
        // =====================================================
        //
        // 画像の上端付近に三角の先端を置いてある。
        // アイコンと同じ中心で回転させると、
        // 丸の外側に三角が飛び出して向きを示す。
        //
        // =====================================================

        /// <summary>
        /// 進行方向を示す、上向きの三角。車の向きに合わせて回転させて使う。
        /// </summary>
        public static Sprite Arrow
        {
            get
            {
                // 作成済みなら、それを返す
                if (s_Arrow != null)
                {
                    return s_Arrow;
                }

                Color32[] pixels = new Color32[SIZE * SIZE];

                float center = SIZE / 2f;

                // 三角の形（画像は下が0、上がSIZE）
                //   tipY     ＝ 先端のY座標（上端の2ピクセル下）
                //   baseY    ＝ 底辺のY座標（上端の20ピクセル下。三角の高さは18）
                //   halfBase ＝ 底辺の半分の幅（三角全体の幅は22）
                float tipY = SIZE - 2f;
                float baseY = SIZE - 20f;
                float halfBase = 11f;

                for (int y = 0; y < SIZE; y++)
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        // このピクセルの中心の座標
                        float px = x + 0.5f;
                        float py = y + 0.5f;

                        // この高さでの三角の半幅を求める。
                        // t ＝ 高さの割合（先端で0、底辺で1）なので、半幅は先端で0、
                        // 底辺で halfBase になる
                        float t = Mathf.Clamp01((tipY - py) / (tipY - baseY));
                        float halfWidth = halfBase * t;

                        // alphaX ＝ 中心の縦線から半幅の内側だけ1（左右の端はなだらかにする）
                        // alphaY ＝ 底辺より上、先端より下だけ1（上下の端を切る）
                        // 掛け合わせると、三角の内側だけが不透明になる
                        float alphaX =
                            Mathf.Clamp01(halfWidth - Mathf.Abs(px - center) + 0.5f);

                        float alphaY =
                            Mathf.Clamp01(py - baseY + 0.5f) *
                            Mathf.Clamp01(tipY - py + 0.5f);

                        pixels[y * SIZE + x] = ToWhite(alphaX * alphaY);
                    }
                }

                s_Arrow = MakeSprite(pixels, SIZE, FilterMode.Bilinear);
                return s_Arrow;
            }
        }


        // =====================================================
        // ■ 市松模様（スタート／ゴール位置）
        // =====================================================

        /// <summary>
        /// スタート／ゴール位置の市松模様（白と黒っぽいマス目）。
        /// </summary>
        public static Sprite Checker
        {
            get
            {
                // 作成済みなら、それを返す
                if (s_Checker != null)
                {
                    return s_Checker;
                }

                // 16×16ピクセルの画像を、4×4ピクセルのマスに区切る（縦横4マスずつの市松模様）
                const int checkerSize = 16;
                const int cell = 4;

                Color32[] pixels = new Color32[checkerSize * checkerSize];

                // マスの2色（白と、ほぼ黒）
                Color32 light = new Color32(255, 255, 255, 255);
                Color32 dark = new Color32(20, 20, 20, 255);

                for (int y = 0; y < checkerSize; y++)
                {
                    for (int x = 0; x < checkerSize; x++)
                    {
                        // マスの番号（x / cell と y / cell）を足して、偶数なら白、
                        // 奇数なら黒っぽい色にする
                        // → 白と黒が交互に並ぶ
                        bool isLight = ((x / cell) + (y / cell)) % 2 == 0;

                        pixels[y * checkerSize + x] = isLight ? light : dark;
                    }
                }

                // 市松模様はくっきり見せたいのでPoint補間
                s_Checker = MakeSprite(pixels, checkerSize, FilterMode.Point);
                return s_Checker;
            }
        }


        // =====================================================
        // ■ 補助関数
        // =====================================================

        /// <summary>
        /// 白色で、不透明度だけを指定したピクセルを作る。
        /// 色は後から Image.color で付けるので、ここでは白にしておく。
        /// </summary>
        /// <param name="alpha">不透明度（0〜1。範囲外は0か1に丸める）</param>
        /// <returns>RGBは白、Aは不透明度の色</returns>
        private static Color32 ToWhite(float alpha)
        {
            // 0〜1 を 0〜255 に直す（Color32 は 0〜255 の整数で色を持つ）
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);

            return new Color32(255, 255, 255, a);
        }

        /// <summary>
        /// ピクセルの配列から、正方形のSpriteを作る。
        /// </summary>
        /// <param name="pixels">全ピクセルの色（size × size 個）</param>
        /// <param name="size">画像の一辺（ピクセル）</param>
        /// <param name="filterMode">補間方法（Bilinear ＝ なめらか／Point ＝ くっきり）</param>
        /// <returns>作ったSprite</returns>
        private static Sprite MakeSprite(
            Color32[] pixels,
            int size,
            FilterMode filterMode)
        {
            // size × size の空のテクスチャを作る
            // （RGBA32 ＝ 赤・緑・青・透明度の4つを持つ形式、
            // false ＝ ミップマップ［縮小用の小さい画像］は作らない）
            Texture2D texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false
                );

            texture.filterMode = filterMode;
            // 端の外側は端の色を延ばす（反対側の色が縁ににじまないようにする）
            texture.wrapMode = TextureWrapMode.Clamp;
            // ピクセルを書き込み、Apply で画面に使える状態にする
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // テクスチャ全体を使って、中心を基準点（pivot）にしたSpriteにする
            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}