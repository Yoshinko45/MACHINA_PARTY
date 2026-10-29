using System;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ 配置の計算（スタート位置の並べ方・ミニマップの置き場所）
    // =====================================================================
    //
    // このファイルは Unity の機能を使わない、ふつうの C# だけで書いてある。
    // 「どこに置くか」という計算だけをここに分けてあるので、テストで確かめられる。
    //
    //   RaceGrid          … スタート位置の並べ方（車を何台、どう並べるか）
    //   MinimapSpot       … ミニマップを置く場所の種類
    //   MinimapRect       … ミニマップの置き方（anchor と大きさ）
    //   RaceMinimapLayout … 人数ごとの、ミニマップの位置と大きさ
    //
    // =====================================================================


    /// <summary>
    /// スタート位置の並べ方。
    ///
    /// 基準になる位置（スタート地点）から見て、「右へ何メートル」「後ろへ何メートル」を返す。
    /// 並べ方は、左右2列。
    ///
    ///   1人 … 基準の位置にそのまま（真ん中）
    ///   2人 … 前の列に、左右に1台ずつ。右の車は少し後ろ（互い違い）
    ///   3人 … 前の列に2台、2列目の左に1台
    ///   4人 … 2列×2台
    ///
    ///   前 →         P1 (左)        P2 (右。少し後ろ)
    ///                P3 (左。1列後ろ) P4 (右。1列後ろで少し後ろ)
    /// </summary>
    public static class RaceGrid
    {
        /// <summary>
        /// 1台ぶんの位置を返す。
        /// </summary>
        /// <param name="slotIndex">何台目か（0から。P1 が 0）</param>
        /// <param name="playerCount">走る人数</param>
        /// <param name="halfWidth">左右の車の、真ん中からの距離（メートル）</param>
        /// <param name="rowSpacing">前後の列の間隔（メートル）</param>
        /// <param name="stagger">右の車を後ろへずらす量（メートル）</param>
        /// <param name="right">基準から見て、右へ何メートルか（左はマイナス）</param>
        /// <param name="back">基準から見て、後ろへ何メートルか（前はマイナス）</param>
        public static void Slot(
            int slotIndex,
            int playerCount,
            float halfWidth,
            float rowSpacing,
            float stagger,
            out float right,
            out float back)
        {
            // 1人だけの時は、基準の位置にそのまま置く
            if (playerCount <= 1)
            {
                right = 0f;
                back = 0f;

                return;
            }

            int slot = Math.Max(0, slotIndex);

            int column = slot % 2;
            int row = slot / 2;

            right = column == 0 ? -halfWidth : halfWidth;
            back = row * rowSpacing + (column == 1 ? stagger : 0f);
        }
    }


    /// <summary>
    /// ミニマップを置く場所の種類。
    /// </summary>
    public enum MinimapSpot
    {
        /// <summary>今の位置のまま（シーンで置いた位置・大きさを変えない）</summary>
        Keep,

        /// <summary>画面の真ん中</summary>
        Center,

        /// <summary>
        /// 画面分割で、どのカメラにも映らない空き枠（3人プレイの右下など）の真ん中。
        /// 空き枠が無い時は、画面の真ん中に置く。
        /// </summary>
        EmptySlot
    }


    /// <summary>
    /// ミニマップの置き方（Unity の RectTransform に渡す値）。
    ///
    /// 位置も大きさも「画面全体を 0〜1 とした割合」で表す。画面や Canvas の大きさが変わっても、
    /// 同じ場所に同じ割合で収まる（ピクセルで決めないので、表示の準備が終わる前に計算しても狂わない）。
    ///
    /// 左下の anchor と右上の anchor で、置く範囲そのものを表す（RectTransform の大きさは 0 にする）。
    /// 中のコース画像は、RaceMinimap が、この範囲にちょうど収まる大きさにする。
    /// </summary>
    public struct MinimapRect
    {
        /// <summary>左下の位置（画面の割合 0〜1）</summary>
        public float AnchorMinX;
        public float AnchorMinY;

        /// <summary>右上の位置（画面の割合 0〜1）</summary>
        public float AnchorMaxX;
        public float AnchorMaxY;
    }


    /// <summary>
    /// ミニマップの位置と大きさを決める。
    /// </summary>
    public static class RaceMinimapLayout
    {
        /// <summary>空き枠を使う時の、枠に対するミニマップの割合の標準値（残りは余白）</summary>
        public const float EmptySlotFill = 0.92f;

        /// <summary>
        /// 画面の真ん中に置く時の、ミニマップの高さの標準値（画面の高さに対する割合）。
        /// 0.45 ＝ 画面の高さの 45%（1080 の高さなら約 486 ピクセル）。
        /// </summary>
        public const float CenterHeightRate = 0.45f;

        /// <summary>画面の縦横比が分からない時に使う値（16:9）</summary>
        private const float DefaultAspect = 16f / 9f;

        /// <summary>
        /// 置く場所の種類から、ミニマップの置き方を求める。
        /// </summary>
        /// <param name="spot">置く場所の種類</param>
        /// <param name="emptyX">空き枠の左端（画面の割合 0〜1）</param>
        /// <param name="emptyY">空き枠の下端（画面の割合 0〜1）</param>
        /// <param name="emptyWidth">空き枠の幅（画面の割合。空き枠が無ければ 0）</param>
        /// <param name="emptyHeight">空き枠の高さ（画面の割合。空き枠が無ければ 0）</param>
        /// <param name="centerHeightRate">真ん中に置く時の高さ（画面の高さに対する割合。0.45 ＝ 45%）。
        /// 幅は、画面の上で正方形になるように決める</param>
        /// <param name="screenAspect">画面の縦横比（幅 ÷ 高さ。例：16:9 なら約 1.78）。0 以下なら 16:9 とみなす</param>
        /// <param name="emptySlotFill">空き枠のうち、ミニマップが使う割合（1 ＝ 枠いっぱい／0.92 ＝ 少し余白）</param>
        /// <param name="rect">求めた置き方</param>
        /// <returns>位置を変える時は true。Keep（変えない）の時は false</returns>
        public static bool Compute(
            MinimapSpot spot,
            float emptyX,
            float emptyY,
            float emptyWidth,
            float emptyHeight,
            float centerHeightRate,
            float screenAspect,
            float emptySlotFill,
            out MinimapRect rect)
        {
            // まず「画面の真ん中に置く」値を入れておく
            rect = CenterRect(centerHeightRate, screenAspect);

            if (spot == MinimapSpot.Keep)
            {
                return false;
            }

            if (spot == MinimapSpot.EmptySlot && emptyWidth > 0f && emptyHeight > 0f)
            {
                // 割合は 0.1〜1 の範囲に収める（0 や負の値で、ミニマップが消えないように）
                float fill = Math.Max(0.1f, Math.Min(1f, emptySlotFill));

                float centerX = emptyX + emptyWidth * 0.5f;
                float centerY = emptyY + emptyHeight * 0.5f;

                float halfWidth = emptyWidth * 0.5f * fill;
                float halfHeight = emptyHeight * 0.5f * fill;

                // 枠の中心から、割合ぶんだけ広げた範囲を anchor にする（RectTransform の大きさは 0）
                rect.AnchorMinX = centerX - halfWidth;
                rect.AnchorMinY = centerY - halfHeight;
                rect.AnchorMaxX = centerX + halfWidth;
                rect.AnchorMaxY = centerY + halfHeight;

                return true;
            }

            // 真ん中（空き枠が無い時の EmptySlot も、ここ）
            return true;
        }

        /// <summary>
        /// 画面の真ん中に、画面の上で正方形になるように置く範囲を求める。
        /// 高さは「画面の高さ × rate」。幅の割合は、縦横比で割って合わせる。
        /// 縦長の画面などで画面からはみ出す時は、はみ出さない大きさまで小さくする。
        /// </summary>
        private static MinimapRect CenterRect(float centerHeightRate, float screenAspect)
        {
            // 0.05〜1 の範囲に収める（0 や負の値で、ミニマップが消えないように）
            float rate = Math.Max(0.05f, Math.Min(1f, centerHeightRate));

            float aspect = screenAspect > 0.1f ? screenAspect : DefaultAspect;

            float halfHeight = rate * 0.5f;
            float halfWidth = halfHeight / aspect;

            // 画面からはみ出す時は、縦横の比を保ったまま小さくする
            float over = Math.Max(halfWidth, halfHeight);

            if (over > 0.5f)
            {
                float shrink = 0.5f / over;

                halfWidth *= shrink;
                halfHeight *= shrink;
            }

            MinimapRect rect = new MinimapRect();

            rect.AnchorMinX = 0.5f - halfWidth;
            rect.AnchorMinY = 0.5f - halfHeight;
            rect.AnchorMaxX = 0.5f + halfWidth;
            rect.AnchorMaxY = 0.5f + halfHeight;

            return rect;
        }

        /// <summary>
        /// 人数に合う場所の種類を選ぶ。
        /// </summary>
        /// <param name="playerCount">走る人数</param>
        /// <param name="oneP">1人の時</param>
        /// <param name="twoP">2人の時</param>
        /// <param name="threeP">3人の時</param>
        /// <param name="fourP">4人の時</param>
        public static MinimapSpot ForPlayerCount(
            int playerCount,
            MinimapSpot oneP,
            MinimapSpot twoP,
            MinimapSpot threeP,
            MinimapSpot fourP)
        {
            if (playerCount <= 1) return oneP;
            if (playerCount == 2) return twoP;
            if (playerCount == 3) return threeP;

            return fourP;
        }
    }
}