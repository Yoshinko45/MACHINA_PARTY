using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ スタート演出で使う「画像」と「音」を、コードで作る部品
    // =====================================================================
    //
    // RaceStartSequence が使う次のものを、再生時にコードで作る。
    // 画像ファイルや音ファイルを用意しなくても動くようにするため。
    // （GameUIHandler の参加カーソルと同じく、Texture2D をコードで作る方式）
    //
    //   画像 … 信号のランプ（丸）・ランプの光彩・信号の本体（角丸四角）・スピード線
    //   音   … カウントダウンの「ピッ」・GO! の高い「ピーッ」
    //
    // 画像は「白色＋透明度だけ」で作り、色は Image.color で付ける。
    //
    // このファイルは RaceStartSequence.cs と同じフォルダに入れること。
    // （RaceStartSequence.cs だけだと、このクラスが見つからずエラーになる）
    //
    // =====================================================================

    /// <summary>
    /// スタート演出（RaceStartSequence）で使う、信号の画像とカウントダウンの音を、
    /// コードで作る道具箱。
    /// すべて static で、状態は持たない。
    /// </summary>
    public static class StartSequenceAssets
    {
        /// <summary>
        /// 音のサンプリング周波数（1秒間の音を、何個の数値で表すか）。44100 は、CDと同じ。
        /// </summary>
        public const int SampleRate = 44100;


        // =================================================================
        // ■ 画像
        // =================================================================
        //
        // 〇〇Alpha(...) は、画像の「透明度だけ」を並べた配列を返す。
        // 並びは Texture2D と同じで、左下から右へ、下から上へ（0〜1）。
        //
        // =================================================================

        /// <summary>
        /// 透明度の配列から、白色のスプライトを作る。
        /// 色は後から Image.color で付ける（白 × 色 ＝ その色になる）。
        /// </summary>
        /// <param name="name">スプライト（とテクスチャ）の名前</param>
        /// <param name="width">画像の幅（ピクセル）</param>
        /// <param name="height">画像の高さ（ピクセル）</param>
        /// <param name="alpha">全ピクセルの透明度（0〜1。左下から右へ、下から上への順）</param>
        /// <returns>作ったスプライト</returns>
        public static Sprite CreateSprite(
            string name,
            int width,
            int height,
            float[] alpha)
        {
            // 画像の大きさは、最低でも1ピクセルにする
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            // 全ピクセルの色を入れる配列（添字 ＝ y * width + x。左下が(0, 0)）
            Color32[] pixels = new Color32[width * height];

            // 透明度（0〜1）を 0〜255 の整数にして、白色（赤・緑・青は255）に付ける。
            // 配列が足りない所は、透明（0）にする
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = (alpha != null && i < alpha.Length) ? alpha[i] : 0f;

                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);

                pixels[i] = new Color32(255, 255, 255, value);
            }

            // 空のテクスチャを作る（RGBA32 ＝ 赤・緑・青・透明度の4つを持つ形式、
            // false ＝ ミップマップ［縮小用の小さい画像］は作らない）
            Texture2D texture =
                new Texture2D(width, height, TextureFormat.RGBA32, false);

            texture.name = name;
            // 端の外側は、端の色を延ばす（反対側の色が、縁ににじまないようにする）
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;  // 拡大縮小の時に、なめらかにつなぐ

            // ピクセルを書き込み、Apply で、画面に使える状態にする
            texture.SetPixels32(pixels);
            texture.Apply();

            // テクスチャ全体を使って、中心を基準点（pivot）にした Sprite にする。
            // （100 ＝ Pixels Per Unit、FullRect ＝ 画像と同じ四角形のメッシュ）
            Sprite sprite =
                Sprite.Create(
                    texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);

            sprite.name = name;

            return sprite;
        }

        /// <summary>
        /// 丸（縁を1ピクセルだけなめらかにした円）の、透明度の配列を作る。
        /// </summary>
        /// <param name="size">画像の一辺（ピクセル）</param>
        /// <returns>透明度の配列（size × size 個。丸の内側が1、外側が0）</returns>
        public static float[] CircleAlpha(int size)
        {
            size = Mathf.Max(2, size);  // 小さすぎる値は 2 にそろえる

            float[] alpha = new float[size * size];

            // center ＝ 丸の中心（ピクセルの番号で数えた座標）
            // radius ＝ 半径（画像の端より1ピクセル小さくして、縁が切れないようにする）
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // このピクセルが、中心からどれだけ離れているか（distance）
                    float dx = x - center;
                    float dy = y - center;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // 縁の前後1ピクセルを、なだらかにする（半径より内側は1＝不透明、外側は0＝透明）
                    alpha[y * size + x] =
                        Mathf.Clamp01(radius - distance + 0.5f);
                }
            }

            return alpha;
        }

        /// <summary>
        /// 光彩（中心が濃く、外へなめらかに薄くなって、端で0になる丸）の、透明度の配列を作る。
        /// </summary>
        /// <param name="size">画像の一辺（ピクセル）</param>
        /// <returns>透明度の配列（size × size 個）</returns>
        public static float[] GlowAlpha(int size)
        {
            size = Mathf.Max(2, size);

            float[] alpha = new float[size * size];

            // center ＝ 中心、radius ＝ 半径（画像の端で、ちょうど0になる）
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // t ＝ 中心からの距離の割合（中心で0、端で1）
                    // u ＝ 1 − t（中心で1、端で0）
                    // u を2乗すると、中心が濃く、外へなめらかに薄くなる
                    float t = Mathf.Clamp01(distance / radius);
                    float u = 1f - t;

                    alpha[y * size + x] = u * u;
                }
            }

            return alpha;
        }

        /// <summary>
        /// 角丸の四角（信号の本体）の、透明度の配列を作る。
        /// </summary>
        /// <param name="width">画像の幅（ピクセル）</param>
        /// <param name="height">画像の高さ（ピクセル）</param>
        /// <param name="radius">角の丸みの半径（ピクセル）</param>
        /// <returns>透明度の配列（width × height 個。四角の内側が1、外側が0）</returns>
        public static float[] RoundedRectAlpha(int width, int height, float radius)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);

            float[] alpha = new float[width * height];

            // halfWidth / halfHeight ＝ 幅・高さの半分（中心から端までの距離）
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            // 角の丸みが、辺の長さを超えないようにする
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(halfWidth, halfHeight));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 中心からの距離（ピクセルの中心で測る）
                    float px = Mathf.Abs(x + 0.5f - halfWidth);
                    float py = Mathf.Abs(y + 0.5f - halfHeight);

                    // 角の丸みの中心から外側へ、どれだけはみ出しているか
                    float qx = px - (halfWidth - r);
                    float qy = py - (halfHeight - r);

                    float outsideX = Mathf.Max(qx, 0f);
                    float outsideY = Mathf.Max(qy, 0f);

                    // 形の外側ならプラス、内側ならマイナスになる距離
                    float distance =
                        Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                        Mathf.Min(Mathf.Max(qx, qy), 0f) -
                        r;

                    // 形の境目の前後0.5ピクセルを、なだらかにする（内側は1＝不透明、外側は0＝透明）
                    alpha[y * width + x] = Mathf.Clamp01(0.5f - distance);
                }
            }

            return alpha;
        }

        /// <summary>
        /// スピード線（画面の中心から外へ向かう細い線。中心付近は透明）の、透明度の配列を作る。
        /// seed が同じなら、いつも同じ模様になる。
        /// </summary>
        /// <param name="size">画像の一辺（ピクセル）</param>
        /// <param name="seed">乱数の種（線の模様を決める）</param>
        /// <returns>透明度の配列（size × size 個）</returns>
        public static float[] SpeedLinesAlpha(int size, int seed)
        {
            size = Mathf.Max(8, size);  // 小さすぎる値は 8 にそろえる

            // 角度ごとの「線の濃さ」と「線が始まる距離」を先に作っておく
            const int Bins = 1440;     // 角度の分け方（360度を1440個に区切る。1個 ＝ 0.25度）
            const int LineCount = 72;  // 線の本数

            float[] ray = new float[Bins];    // 角度ごとの、線の濃さ（0〜1）
            float[] start = new float[Bins];  // 角度ごとの、線が始まる距離

            // seed が同じなら、毎回同じ乱数の並びになる（＝同じ模様になる）。
            // Unity の Random は使わないので、ゲーム側の乱数には影響しない
            System.Random random = new System.Random(seed);

            // 線を1本ずつ、乱数で決めて、ray / start に書き込む
            for (int i = 0; i < LineCount; i++)
            {
                float center = (float)random.NextDouble() * Bins;  // 線の中心の角度（0〜Bins）

                // 線の太さ（角度の幅。1 = 0.25度）
                float halfWidth = Mathf.Lerp(1.2f, 3.2f, (float)random.NextDouble());

                // 線の濃さ（0.40〜1）
                float strength = Mathf.Lerp(0.40f, 1f, (float)random.NextDouble());

                // 中心からどれだけ離れた所から線が始まるか（画面の半分の長さに対する割合）
                float begin = Mathf.Lerp(0.30f, 0.70f, (float)random.NextDouble());

                // 線の中心の近くの角度だけを調べる。
                // baseBin ＝ 線の中心がある角度の番号、reach ＝ そこから左右に調べる幅
                int reach = Mathf.CeilToInt(halfWidth) + 1;
                int baseBin = Mathf.FloorToInt(center);

                for (int k = -reach; k <= reach + 1; k++)
                {
                    int bin = baseBin + k;

                    // distance ＝ 線の中心からの距離の割合（中心で0、線の太さの端で1）
                    float distance = Mathf.Abs(bin + 0.5f - center) / halfWidth;

                    // 線の太さの外側は、何もしない
                    if (distance >= 1f)
                    {
                        continue;
                    }

                    // 中心が濃く、端がなめらかに消える
                    float falloff = 1f - distance * distance * (3f - 2f * distance);

                    float value = strength * falloff;

                    // bin が負の数や Bins 以上でも、0〜Bins-1 に収める
                    // （角度は360度でひとまわりするので、端と端はつながる）
                    int index = ((bin % Bins) + Bins) % Bins;

                    // 他の線と重なった所は、濃い方を残す
                    if (value > ray[index])
                    {
                        ray[index] = value;
                        start[index] = begin;
                    }
                }
            }

            // ここから、画像の全ピクセルの透明度を決める
            float[] alpha = new float[size * size];

            float middle = (size - 1) * 0.5f;  // 画像の中心

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 中心を0、画面の端の中央を1とした位置
                    float dx = (x - middle) / middle;
                    float dy = (y - middle) / middle;

                    // distance ＝ 中心からの距離（画面の端の中央で1、四隅は約1.41）
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float angle = Mathf.Atan2(dy, dx);  // 中心から見た角度（-π〜π）

                    // 角度 → 配列の位置（隣の値となめらかにつなぐ）
                    float position =
                        (angle + Mathf.PI) / (2f * Mathf.PI) * Bins - 0.5f;

                    // position の前後2つの角度（a と b）を、blend の割合で混ぜる。
                    // （blend ＝ position の小数の部分。0なら a だけ、1なら b だけ）
                    int low = Mathf.FloorToInt(position);
                    float blend = position - low;

                    int a = ((low % Bins) + Bins) % Bins;  // 0〜Bins-1 に収める
                    int b = (a + 1) % Bins;

                    float strengthA = ray[a] * (1f - blend);
                    float strengthB = ray[b] * blend;

                    float strength = strengthA + strengthB;

                    // 線の始まりは、線がある側の値を採る
                    // （線のない隣の値と混ぜると、中心近くにうっすら線が出てしまう）
                    float begin =
                        strength > 1e-6f
                            ? (start[a] * strengthA + start[b] * strengthB) / strength
                            : 1f;

                    // 線の始まり（begin）から、距離 0.30 ぶんかけて、なめらかに濃くなる（0 → 1）
                    float grow = Mathf.Clamp01((distance - begin) / 0.30f);

                    grow = grow * grow * (3f - 2f * grow);  // なめらかな曲線にする

                    alpha[y * size + x] = strength * grow;
                }
            }

            return alpha;
        }


        // =================================================================
        // ■ 音
        // =================================================================
        //
        // マリオカート8のカウントダウン音を測って真似ている。
        //
        //   「3」「2」「1」… ラ（A4 = 440Hz）とその1オクターブ上を重ねた音を
        //                    約0.46秒。出だしは一瞬で立ち上がる
        //   「GO!」        … 1オクターブ高いラ（A5 = 880Hz）と、その上を重ねた
        //                    音を約1.25秒。最後はゆっくり消える
        //
        // =================================================================

        /// <summary>
        /// サンプルの配列から、1チャンネル（モノラル）のAudioClipを作る。
        /// </summary>
        /// <param name="name">AudioClip の名前</param>
        /// <param name="samples">音の波形（-1〜1 の数値を、時間順に並べたもの）</param>
        /// <returns>作ったAudioClip</returns>
        public static AudioClip MakeClip(string name, float[] samples)
        {
            // 引数 ＝ 名前、サンプルの数、チャンネル数（1 ＝ モノラル）、サンプリング周波数、
            // ストリーミングにするか（false ＝ しない。全部をメモリに置く）
            AudioClip clip =
                AudioClip.Create(
                    name,
                    samples.Length,
                    1,
                    SampleRate,
                    false);

            // 波形のデータを、先頭（0番目）から書き込む
            clip.SetData(samples, 0);

            return clip;
        }

        /// <summary>
        /// カウントダウン（3・2・1）の「ピッ」の波形を作る。
        /// </summary>
        /// <returns>音の波形（AudioClip にするには MakeClip を使う）</returns>
        public static float[] SynthCountdownBeep()
        {
            return
                SynthTone(
                    440f,     // 基本の音の高さ（Hz）。ラ（A4）
                    0.60f,    // 1オクターブ上の音の大きさ
                    0.46f,    // 全体の長さ（秒）
                    0.004f,   // 立ち上がりの長さ（秒）。ほぼ一瞬
                    0.045f);  // 最後に消えていく長さ（秒）。短い
        }

        /// <summary>
        /// GO! の高い「ピーッ」の波形を作る。
        /// </summary>
        /// <returns>音の波形（AudioClip にするには MakeClip を使う）</returns>
        public static float[] SynthGoTone()
        {
            return
                SynthTone(
                    880f,     // 基本の音の高さ（Hz）。1オクターブ高いラ（A5）
                    0.45f,    // 1オクターブ上の音の大きさ
                    1.25f,    // 全体の長さ（秒）
                    0.004f,   // 立ち上がりの長さ（秒）。ほぼ一瞬
                    0.50f);   // 最後に消えていく長さ（秒）。ゆっくり消える
        }

        /// <summary>
        /// 基本の音（サイン波）と、その1オクターブ上の音を重ねた、音の波形を作る。
        /// 始まりと終わりは、音量をなめらかに変える。
        /// </summary>
        /// <param name="frequency">基本の音の高さ（Hz）</param>
        /// <param name="octaveLevel">1オクターブ上の音の大きさ（基本の音を1として）</param>
        /// <param name="duration">全体の長さ（秒）</param>
        /// <param name="attack">立ち上がりの長さ（秒）。プチッというノイズを防ぐ</param>
        /// <param name="release">最後に消えていく長さ（秒）</param>
        /// <returns>音の波形（-1〜1 の数値を、時間順に並べたもの）</returns>
        private static float[] SynthTone(
            float frequency,
            float octaveLevel,
            float duration,
            float attack,
            float release)
        {
            // サンプルの数 ＝ 長さ（秒）× 1秒あたりのサンプル数
            int count = Mathf.CeilToInt(duration * SampleRate);

            float[] data = new float[count];

            // 2つの音を足しても割れないように、全体の大きさをそろえる
            float gain = 0.8f / (1f + octaveLevel);

            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;  // このサンプルの時刻（秒）

                // envelope ＝ 音量の変化（0〜1）。
                // 始まりと終わりを小さくして、プチッというノイズを防ぐ
                float envelope = 1f;

                // 立ち上がり：0 から 1 へ、まっすぐ増やす
                if (time < attack)
                {
                    envelope = time / attack;
                }

                // 最後の release 秒間は、1 から 0 へ減らす（remain ＝ 終わりまでの残り時間）
                float remain = duration - time;

                if (remain < release)
                {
                    float u = Mathf.Clamp01(remain / release);

                    // なめらかに0へ（始まりと終わりの傾きが0）
                    envelope *= u * u * (3f - 2f * u);
                }

                // 時間が長くなっても位相がずれないよう、doubleで計算する
                double phase = 2.0 * System.Math.PI * frequency * time;

                // 基本の音（sin）に、2倍の周波数（1オクターブ上）の音を、octaveLevel の割合で足す
                float wave =
                    (float)System.Math.Sin(phase) +
                    octaveLevel * (float)System.Math.Sin(phase * 2.0);

                data[i] = wave * gain * envelope;  // 大きさと音量の変化を掛ける
            }

            return data;
        }
    }
}