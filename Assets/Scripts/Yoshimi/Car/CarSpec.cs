using System;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ 車の性能（車種）
    // =====================================================================
    //
    // 車のプレハブは1種類だけなので、車ごとの性能差は
    // 「5つの項目の点数」で決める（マリカーと同じ考え方）。
    //
    //   スピード      … 最高速度
    //   かそく        … 最高速度に届くまでの速さ
    //   おもさ        … ぶつかった時の強さ（重いほど相手を押せる）。車体の見た目も大きくなる
    //   まがりやすさ  … ハンドルの切れ
    //   すべりにくさ  … ドリフトで外へ流れにくい・ダート（Groundタグ）で遅くなりにくい
    //
    // 各項目は 1〜5 点。5項目の合計は必ず 15 点にする。
    // （どの車種も、強さの総量は同じ。得意なところがある代わりに、苦手なところがある）
    //
    // 点数から車への反映（掛け率）は CarTuning にまとめてある。
    // 「3点」がちょうどプレハブの値（掛け率 1.0）で、ここから上下に動かす。
    // このファイルも Unity の機能を使わない、ふつうの C#。
    //
    // =====================================================================


    /// <summary>
    /// 5項目の点数の組。
    /// </summary>
    public struct CarSpec
    {
        /// <summary>1項目の最低点</summary>
        public const int MinPoint = 1;

        /// <summary>1項目の最高点</summary>
        public const int MaxPoint = 5;

        /// <summary>5項目の合計点（全車種で同じ）</summary>
        public const int Budget = 15;

        /// <summary>項目の数</summary>
        public const int StatCount = 5;

        /// <summary>スピード（最高速度）</summary>
        public int Speed;

        /// <summary>かそく</summary>
        public int Accel;

        /// <summary>おもさ</summary>
        public int Weight;

        /// <summary>まがりやすさ</summary>
        public int Handling;

        /// <summary>すべりにくさ</summary>
        public int Grip;

        /// <summary>項目の名前（英語。画面の標準の文字）</summary>
        public static readonly string[] StatNamesEn =
            { "SPEED", "ACCEL", "WEIGHT", "HANDLING", "GRIP" };

        /// <summary>項目の名前（日本語。日本語が出せるフォントの時に使う）</summary>
        public static readonly string[] StatNamesJa =
            { "スピード", "かそく", "おもさ", "まがりやすさ", "すべりにくさ" };

        public CarSpec(int speed, int accel, int weight, int handling, int grip)
        {
            Speed = speed;
            Accel = accel;
            Weight = weight;
            Handling = handling;
            Grip = grip;
        }

        /// <summary>5項目の合計点</summary>
        public int Total
        {
            get { return Speed + Accel + Weight + Handling + Grip; }
        }

        /// <summary>
        /// 正しい点数の組か。
        /// どの項目も 1〜5 点の範囲で、合計がちょうど 15 点であること。
        /// </summary>
        public bool IsValid
        {
            get
            {
                for (int i = 0; i < StatCount; i++)
                {
                    int point = Get(i);

                    if (point < MinPoint || point > MaxPoint)
                    {
                        return false;
                    }
                }

                return Total == Budget;
            }
        }

        /// <summary>
        /// 番号（0〜4）で項目の点数を取る。
        /// 0 ＝ スピード／1 ＝ かそく／2 ＝ おもさ／3 ＝ まがりやすさ／4 ＝ すべりにくさ。
        /// 範囲外の番号は、基準の 3 点を返す。
        /// </summary>
        public int Get(int statIndex)
        {
            switch (statIndex)
            {
                case 0: return Speed;
                case 1: return Accel;
                case 2: return Weight;
                case 3: return Handling;
                case 4: return Grip;
                default: return 3;
            }
        }
    }


    /// <summary>
    /// 車種1つぶんの情報（名前・ひとこと説明・性能）。
    /// </summary>
    public sealed class CarTypeInfo
    {
        public readonly string NameEn;
        public readonly string NameJa;
        public readonly string TaglineEn;
        public readonly string TaglineJa;
        public readonly CarSpec Spec;

        public CarTypeInfo(
            string nameEn,
            string nameJa,
            string taglineEn,
            string taglineJa,
            CarSpec spec)
        {
            NameEn = nameEn;
            NameJa = nameJa;
            TaglineEn = taglineEn;
            TaglineJa = taglineJa;
            Spec = spec;
        }

        /// <summary>名前（日本語を使う設定ならその名前）</summary>
        public string Name(bool japanese)
        {
            return japanese ? NameJa : NameEn;
        }

        /// <summary>ひとこと説明（日本語を使う設定ならその説明）</summary>
        public string Tagline(bool japanese)
        {
            return japanese ? TaglineJa : TaglineEn;
        }
    }


    /// <summary>
    /// 選べる車種の一覧。
    /// 車種を増やしたい時は、All に1つ足す（合計は必ず 15 点にすること）。
    /// </summary>
    public static class CarTypeCatalog
    {
        //                                       スピード かそく おもさ まがり すべり
        public static readonly CarTypeInfo[] All = new CarTypeInfo[]
        {
            new CarTypeInfo(
                "BALANCE", "バランス",
                "Easy to drive", "クセがなく、走らせやすい",
                new CarSpec(3, 3, 3, 3, 3)),

            new CarTypeInfo(
                "SPEED", "スピード",
                "Fast on straights, slow to pick up", "直線は速いが、加速はゆっくり",
                new CarSpec(5, 2, 2, 2, 4)),

            new CarTypeInfo(
                "DASH", "ダッシュ",
                "Quick off the mark", "加速がするどい",
                new CarSpec(2, 5, 2, 3, 3)),

            new CarTypeInfo(
                "HEAVY", "ヘビー",
                "Heavy, wins every bump", "重くて、ぶつかり合いに強い",
                new CarSpec(4, 1, 5, 2, 3)),

            new CarTypeInfo(
                "DRIFT", "ドリフト",
                "Sharp turns, but slides a lot", "よく曲がるが、よく滑る",
                new CarSpec(3, 3, 2, 5, 2))
        };

        /// <summary>車種の数</summary>
        public static int Count
        {
            get { return All.Length; }
        }

        /// <summary>
        /// 番号で車種を取る。範囲外の番号は、前後をつなげて（循環させて）選ぶ。
        /// （-1 は最後の車種、Count は最初の車種）
        /// </summary>
        public static CarTypeInfo Get(int index)
        {
            return All[Wrap(index)];
        }

        /// <summary>
        /// 番号を、0 〜 Count-1 の範囲に収める（循環させる）。
        /// </summary>
        public static int Wrap(int index)
        {
            int count = All.Length;

            return ((index % count) + count) % count;
        }
    }


    /// <summary>
    /// 点数（1〜5）から、車の値に掛ける倍率を求める。
    ///
    /// 3点が 1.0 倍（プレハブの値そのまま）。点数ごとに、次のように変わる。
    ///
    ///   項目            1点    2点    3点    4点    5点    何に掛けるか
    ///   スピード        0.96   0.98   1.00   1.02   1.04   最高速度（maxSpeed）
    ///   かそく          0.60   0.80   1.00   1.20   1.40   アクセルの効き（accelResponse）
    ///   おもさ(質量)    0.60   0.80   1.00   1.20   1.40   Rigidbody の質量（mass）
    ///   おもさ(見た目)  0.90   0.95   1.00   1.05   1.10   車体モデルの大きさ
    ///   まがりやすさ    0.80   0.90   1.00   1.10   1.20   旋回の強さ（steerStrength）
    ///   すべりにくさ    1.50   1.25   1.00   0.75   0.50   ドリフト中の横滑り（driftSidewaysFriction）
    ///   すべりにくさ    0.80   0.90   1.00   1.10   1.20   ダートでの速度（groundSpeedMultiplier）
    ///
    /// スピードだけ差を小さくしてあるのは、最高速度の差が1周ごとに積み上がるため。
    /// ここを大きくすると、いつも同じ車種が勝つレースになりやすい。
    ///
    /// 「influence」（影響の強さ）で、差の大きさをまとめて調整できる。
    /// 0 なら全車同じ性能、1 が標準、2 なら差が2倍。
    /// </summary>
    public static class CarTuning
    {
        // 3点を 0 として、-2 〜 +2 に直した値
        private static float Offset(int point)
        {
            int clamped = Math.Max(CarSpec.MinPoint, Math.Min(CarSpec.MaxPoint, point));

            return clamped - 3;
        }

        /// <summary>最高速度への倍率</summary>
        public static float SpeedMultiplier(int point)
        {
            return 1f + 0.02f * Offset(point);
        }

        /// <summary>アクセルの効き（accelResponse）への倍率</summary>
        public static float AccelMultiplier(int point)
        {
            return 1f + 0.20f * Offset(point);
        }

        /// <summary>Rigidbody の質量への倍率</summary>
        public static float MassMultiplier(int point)
        {
            return 1f + 0.20f * Offset(point);
        }

        /// <summary>車体モデルの大きさ（見た目）への倍率</summary>
        public static float ModelScale(int point)
        {
            return 1f + 0.05f * Offset(point);
        }

        /// <summary>旋回の強さ（steerStrength）への倍率</summary>
        public static float HandlingMultiplier(int point)
        {
            return 1f + 0.10f * Offset(point);
        }

        /// <summary>ドリフト中の横滑り（driftSidewaysFriction）への倍率。点が高いほど小さい</summary>
        public static float SlideMultiplier(int point)
        {
            return 1f - 0.25f * Offset(point);
        }

        /// <summary>ダートでの速度（groundSpeedMultiplier）への倍率。点が高いほど大きい</summary>
        public static float DirtMultiplier(int point)
        {
            return 1f + 0.10f * Offset(point);
        }

        /// <summary>
        /// 倍率に、影響の強さを掛ける。
        /// 倍率 1.0 からの差を、influence 倍にする（influence が 0 なら必ず 1.0）。
        /// 結果が小さくなりすぎないよう、下限 0.1 を付ける。
        /// </summary>
        public static float Scale(float multiplier, float influence)
        {
            float result = 1f + (multiplier - 1f) * influence;

            return Math.Max(0.1f, result);
        }
    }
}
