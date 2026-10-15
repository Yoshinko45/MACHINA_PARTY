using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ ミニマップ用コース画像の加工
    // =====================================================
    //
    // Raycastで調べた「ここは道路」というマスク（0/1の配列）を
    // きれいな見た目のテクスチャに仕上げるためのクラス。
    //
    // ・Close  … 道路パーツの継ぎ目などの小さな隙間を埋める
    // ・Smooth … ギザギザや点ノイズを減らす
    // ・Open   … 柵・ポールなど細いものを消す
    // ・KeepConnectedToSeeds … 目印（チェックポイントなど）に
    // 　つながっている道路だけ残し、離れた障害物を消す
    // ・CropToRoad … 道路がある範囲だけに画像を切り詰める
    // ・SnapToRoadCenter … 点を道路の幅の真ん中に寄せる
    // ・BuildTexture … 道路の塗り＋白い縁取りのテクスチャを作る
    //
    // maskは「左下が(0,0)、1ピクセル＝ワールドの1マス」の
    // 配列（添字 = y * width + x）。
    //
    // RaceMinimap からは、次の順番で呼ばれる。
    //   Close → Smooth → Open → KeepConnectedToSeeds → CropToRoad → BuildTexture
    // （ゴールの目印の位置決めには、SnapToRoadCenter も使う）
    //
    // =====================================================

    /// <summary>
    /// ミニマップのコース画像を作るための、画像加工の道具箱。
    /// すべて static のメソッドで、状態は持たない。
    /// </summary>
    public static class MinimapCourseBuilder
    {
        // =====================================================
        // ■ 円形カーネルの相対座標一覧
        // =====================================================

        /// <summary>
        /// ピクセルを動かす範囲（半径 radius の丸）に入る、相対座標の一覧を作る。
        /// 膨張・収縮で「周りのどのピクセルを見るか」を決めるのに使う。
        /// </summary>
        /// <param name="radius">丸の半径（ピクセル）</param>
        /// <returns>丸の中の相対座標 (dx, dy) の一覧（中心の (0, 0) も含む）</returns>
        private static List<Vector2Int> MakeKernel(int radius)
        {
            List<Vector2Int> offsets = new List<Vector2Int>();

            // 中心からの距離の2乗が r*r + r 以下のものを、範囲に入れる。
            // （r*r だけだと、半径1では斜めが入らず十字形になってしまう。
            // 　r*r + r にすると斜めも入り、半径1で3×3の四角形、半径2で角の取れた丸になる）
            int limit = radius * radius + radius;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= limit)
                    {
                        offsets.Add(new Vector2Int(dx, dy));
                    }
                }
            }

            return offsets;
        }


        // =====================================================
        // ■ 膨張（道路を radius ピクセル太らせる）
        // =====================================================

        /// <summary>
        /// 膨張。道路のピクセルを、まわりに radius ピクセルぶん広げて太らせる。
        /// </summary>
        /// <param name="src">道路マスク（0＝道路でない、1＝道路）。書き換えない</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="radius">太らせる半径（ピクセル）。0以下なら太らせない</param>
        /// <returns>太らせた新しいマスク</returns>
        public static byte[] Dilate(byte[] src, int w, int h, int radius)
        {
            // 結果を入れる配列（元の src は書き換えない）
            byte[] dst = new byte[src.Length];

            // 半径0なら太らせる必要が無いので、そのままコピーして返す
            if (radius <= 0)
            {
                src.CopyTo(dst, 0);
                return dst;
            }

            // 道路のまわりのどこまでを塗るか（相対座標の一覧）
            List<Vector2Int> kernel = MakeKernel(radius);

            // 全ピクセルを調べ、道路を見つけたら、まわり（kernel の範囲）も道路にする
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x] == 0)
                    {
                        continue;
                    }

                    for (int k = 0; k < kernel.Count; k++)
                    {
                        int nx = x + kernel[k].x;
                        int ny = y + kernel[k].y;

                        // 画像の外は無視する
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                        {
                            continue;
                        }

                        dst[ny * w + nx] = 1;
                    }
                }
            }

            return dst;
        }


        // =====================================================
        // ■ 収縮（道路を radius ピクセル細らせる）
        // =====================================================
        //
        // 画像の外側は「道路あり」として扱う
        // （画像の端で道路が欠けないようにするため）
        //
        // =====================================================

        /// <summary>
        /// 収縮。道路のふちを、radius ピクセルぶん削って細らせる。
        /// </summary>
        /// <param name="src">道路マスク（0＝道路でない、1＝道路）。書き換えない</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="radius">細らせる半径（ピクセル）。0以下なら細らせない</param>
        /// <returns>細らせた新しいマスク</returns>
        public static byte[] Erode(byte[] src, int w, int h, int radius)
        {
            byte[] dst = new byte[src.Length];

            if (radius <= 0)
            {
                src.CopyTo(dst, 0);
                return dst;
            }

            List<Vector2Int> kernel = MakeKernel(radius);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x] == 0)
                    {
                        continue;
                    }

                    // まわり（kernel の範囲）に、道路でないピクセルが1つでもあれば、
                    // このピクセルは消す
                    bool keep = true;

                    for (int k = 0; k < kernel.Count; k++)
                    {
                        int nx = x + kernel[k].x;
                        int ny = y + kernel[k].y;

                        // 画像の外は調べない（＝道路ありとみなす。上の説明のとおり）
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                        {
                            continue;
                        }

                        if (src[ny * w + nx] == 0)
                        {
                            keep = false;
                            break;
                        }
                    }

                    if (keep)
                    {
                        dst[y * w + x] = 1;
                    }
                }
            }

            return dst;
        }


        // =====================================================
        // ■ 隙間埋め（膨張 → 収縮）
        // =====================================================
        //
        // 道路が複数のパーツに分かれていて継ぎ目に
        // 細い隙間がある場合や、Rayの取りこぼしによる
        // 小さな穴を埋める。道路全体の太さは変わらない。
        //
        // =====================================================

        /// <summary>
        /// 隙間埋め。道路の小さな隙間や穴を、radius ピクセルぶん埋める。
        /// </summary>
        /// <param name="mask">道路マスク。この配列を直接書き換える</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="radius">埋める隙間の大きさ（ピクセル）。0以下なら何もしない</param>
        public static void Close(byte[] mask, int w, int h, int radius)
        {
            if (radius <= 0)
            {
                return;
            }

            // 先に太らせて隙間をふさぎ、同じだけ細らせて、元の太さに戻す
            byte[] dilated = Dilate(mask, w, h, radius);
            byte[] closed = Erode(dilated, w, h, radius);

            // 結果を mask に書き戻す
            closed.CopyTo(mask, 0);
        }


        // =====================================================
        // ■ 平滑化（周囲の多数決）
        // =====================================================
        //
        // 3x3の範囲で「道路」が過半数なら道路、そうでなければ
        // 道路なしにする。孤立した点を消し、縁を滑らかにする。
        //
        // =====================================================

        /// <summary>
        /// 平滑化。まわりの多数決で、ギザギザや点ノイズを減らす。
        /// </summary>
        /// <param name="mask">道路マスク。この配列を直接書き換える</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="passes">繰り返す回数（0なら何もしない）</param>
        public static void Smooth(byte[] mask, int w, int h, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                // 多数決の結果を入れる配列
                // （途中の結果を次の判定に使わないよう、mask とは別に用意する）
                byte[] result = new byte[mask.Length];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // このピクセルと、まわり8個（合わせて3×3）のうち、
                        // 道路のピクセルが何個あるか数える
                        int count = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                                {
                                    continue;
                                }

                                count += mask[ny * w + nx];
                            }
                        }

                        // 9個のうち5個以上（過半数）が道路なら、道路にする
                        result[y * w + x] = (byte)(count >= 5 ? 1 : 0);
                    }
                }

                result.CopyTo(mask, 0);
            }
        }


        // =====================================================
        // ■ 細いものを消す（収縮 → 膨張）
        // =====================================================
        //
        // 幅が radius×2 ピクセル以下の細い部分を消す。
        // 柵・ポール・看板・道路脇の細い飾りなどが対象。
        // 道路本体のように十分太い部分は、元の形のまま残る。
        //
        // （radius 1 → 幅2以下、2 → 幅4以下、3 → 幅6以下）
        //
        // =====================================================

        /// <summary>
        /// 細いもの（柵・ポールなど）を消す。
        /// </summary>
        /// <param name="mask">道路マスク。この配列を直接書き換える</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="radius">消す細さ（幅が radius×2 以下の部分）。0以下なら何もしない</param>
        public static void Open(byte[] mask, int w, int h, int radius)
        {
            if (radius <= 0)
            {
                return;
            }

            // 先に細らせて、細いものを消し、同じだけ太らせて、残った太い部分を元の太さに戻す
            byte[] eroded = Erode(mask, w, h, radius);
            byte[] opened = Dilate(eroded, w, h, radius);

            opened.CopyTo(mask, 0);
        }


        // =====================================================
        // ■ 目印につながっている道路だけを残す
        // =====================================================
        //
        // seeds（チェックポイント・ゴール・車の位置など、
        // 確実に道路の上にあるピクセル）から道路をたどり、
        // たどり着けない道路（コースから離れて浮いている
        // 障害物や装飾）を消す。
        //
        // gapRadius … この半径ぶん太らせた状態でつながりを調べる。
        // 　　　　　　 道路が小さな隙間で途切れていても同じ道路とみなす
        // 　　　　　　 （消えるのは元の道路ピクセルだけで、形は変えない）
        // searchRadius … seedが道路から少しズレていた時に、
        // 　　　　　　 この範囲で一番近い道路ピクセルを探す
        //
        // 戻り値は、道路に乗っていた目印の数。
        // 0なら何も変更しない（目印が1つも道路に乗っていないのに
        // 消してしまい、コースが空になるのを防ぐため）。
        //
        // =====================================================

        /// <summary>
        /// 目印（seeds）につながっている道路だけを残し、つながっていない道路を消す。
        /// </summary>
        /// <param name="mask">道路マスク。この配列を直接書き換える</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="seeds">確実に道路の上にあるはずの位置（ピクセル座標）の一覧</param>
        /// <param name="gapRadius">つなぐ隙間の大きさ（道路を太らせる半径。ピクセル）</param>
        /// <param name="searchRadius">目印の近くで、道路を探す範囲（ピクセル）</param>
        /// <returns>道路の上にあった目印の数（0なら何も消していない）</returns>
        public static int KeepConnectedToSeeds(
            byte[] mask,
            int w,
            int h,
            List<Vector2Int> seeds,
            int gapRadius,
            int searchRadius)
        {
            // 目印が1つも無ければ、何も消さずに終わる
            if (seeds == null || seeds.Count == 0)
            {
                return 0;
            }

            // つながり判定用のマスク（隙間ぶんだけ太らせる）
            byte[] link = Dilate(mask, w, h, gapRadius);

            // visited ＝ たどり着いたことが分かったピクセル
            // queue ＝ これから調べるピクセルの順番待ち（ピクセルの添字を入れる）
            bool[] visited = new bool[mask.Length];
            int[] queue = new int[mask.Length];

            int validSeeds = 0;

            // 目印を1つずつ取り出して、そこから道路をたどっていく
            for (int s = 0; s < seeds.Count; s++)
            {
                // 目印の位置（かその近く）にある道路のピクセルを、たどり始める場所にする
                int start = FindNearestRoadPixel(mask, w, h, seeds[s], searchRadius);

                // 探す範囲に道路が無い目印は使わない（コースから離れた所にある目印など）
                if (start < 0)
                {
                    continue;
                }

                validSeeds++;

                // もう別の目印からたどり着いている場所なら、もう一度たどる必要は無い
                if (visited[start])
                {
                    continue;
                }

                // 幅優先探索で、つながっている範囲（8方向）を塗りつぶす。
                // （目印に近い所から順に、順番待ち（queue）を使って広げていく方法）
                int head = 0;  // 次に取り出す位置
                int tail = 0;  // 順番待ちの末尾（ここに追加していく）

                queue[tail++] = start;
                visited[start] = true;

                while (head < tail)
                {
                    // 順番待ちから1つ取り出して、そのまわり8方向を調べる
                    int p = queue[head++];

                    // 添字 p を、x（列）と y（行）に戻す
                    int px = p % w;
                    int py = p / w;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }

                            int nx = px + dx;
                            int ny = py + dy;

                            if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                            {
                                continue;
                            }

                            int n = ny * w + nx;

                            // 調べ済みの場所、または、（隙間をまたいでも）道路でない場所は飛ばす
                            if (visited[n] || link[n] == 0)
                            {
                                continue;
                            }

                            // 新しくたどり着いたので、印を付けて、順番待ちに加える
                            visited[n] = true;
                            queue[tail++] = n;
                        }
                    }
                }
            }

            // 道路の上にある目印が1つも無ければ、何も消さずに終わる
            // （目印がすべてズレているのに消すと、コースが空になってしまうため）
            if (validSeeds == 0)
            {
                return 0;
            }

            // たどり着けなかった道路を消す
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] != 0 && !visited[i])
                {
                    mask[i] = 0;
                }
            }

            return validSeeds;
        }

        /// <summary>
        /// seed の位置か、その近くで一番近い道路ピクセルを探す。
        /// </summary>
        /// <param name="mask">道路マスク</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="seed">探す中心の位置（ピクセル座標）</param>
        /// <param name="searchRadius">探す範囲（seed から上下左右へ、このピクセル数まで）</param>
        /// <returns>見つかった道路ピクセルの添字（y * w + x）。見つからなければ -1</returns>
        private static int FindNearestRoadPixel(
            byte[] mask,
            int w,
            int h,
            Vector2Int seed,
            int searchRadius)
        {
            int best = -1;
            int bestDistance = int.MaxValue;

            // seed を中心にした正方形の範囲を調べて、一番近い道路ピクセルを探す
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    int x = seed.x + dx;
                    int y = seed.y + dy;

                    if (x < 0 || x >= w || y < 0 || y >= h)
                    {
                        continue;
                    }

                    if (mask[y * w + x] == 0)
                    {
                        continue;
                    }

                    // 距離の2乗（大小を比べるだけなので、平方根は求めない）
                    int distance = dx * dx + dy * dy;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = y * w + x;
                    }
                }
            }

            return best;
        }


        // =====================================================
        // ■ 道路がある範囲に切り詰める
        // =====================================================
        //
        // 道路のピクセルをすべて含む最小の四角形に、margin ピクセル
        // の余白を足した範囲を切り出して、新しいmaskとして返す
        // （元の画像の内側に収まる範囲まで）。
        //
        // offsetX / offsetY … 切り出した範囲の左下が、元の画像で
        // 　　　　　　　　　　 何ピクセル目にあたるか
        // newW / newH … 切り出した後の大きさ
        //
        // 道路が1ピクセルも無い時は何もせず、nullを返す。
        //
        // =====================================================

        /// <summary>
        /// 道路がある範囲だけを切り出す。
        /// </summary>
        /// <param name="mask">道路マスク。この配列は書き換えない</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="margin">道路のまわりに残す余白（ピクセル）</param>
        /// <param name="offsetX">切り出した範囲の左端が、元の画像の何ピクセル目か</param>
        /// <param name="offsetY">切り出した範囲の下端が、元の画像の何ピクセル目か</param>
        /// <param name="newW">切り出した後の幅（ピクセル）</param>
        /// <param name="newH">切り出した後の高さ（ピクセル）</param>
        /// <returns>切り出した新しいマスク。道路が1ピクセルも無い時は null</returns>
        public static byte[] CropToRoad(
            byte[] mask,
            int w,
            int h,
            int margin,
            out int offsetX,
            out int offsetY,
            out int newW,
            out int newH)
        {
            // 道路のピクセルがある範囲（x と y の最小・最大）。
            // 最初は「まだ1つも見つかっていない」状態にしておく
            int minX = w;
            int minY = h;
            int maxX = -1;
            int maxY = -1;

            // 全ピクセルを調べて、道路がある範囲を求める
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x] == 0)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            // 道路が1ピクセルも無かった（範囲が1度も更新されなかった）ので、何もしない
            if (maxX < 0)
            {
                offsetX = 0;
                offsetY = 0;
                newW = w;
                newH = h;

                return null;
            }

            // 道路の範囲に余白を足す（元の画像の外に出ないよう、0 と w-1 / h-1 で止める）
            int x0 = Mathf.Max(0, minX - margin);
            int y0 = Mathf.Max(0, minY - margin);
            int x1 = Mathf.Min(w - 1, maxX + margin);
            int y1 = Mathf.Min(h - 1, maxY + margin);

            offsetX = x0;
            offsetY = y0;
            newW = x1 - x0 + 1;
            newH = y1 - y0 + 1;

            // 切り出した範囲を、新しい配列にコピーする（x0, y0 ＝ 元の画像での左下の位置）
            byte[] cropped = new byte[newW * newH];

            for (int y = 0; y < newH; y++)
            {
                for (int x = 0; x < newW; x++)
                {
                    cropped[y * newW + x] = mask[(y + y0) * w + (x + x0)];
                }
            }

            return cropped;
        }


        // =====================================================
        // ■ 道路の中心線に寄せる
        // =====================================================
        //
        // 点（x, y）を、道路の幅の真ん中に寄せる。
        // ゴールのトリガーの中心などが、コースの外側や端に
        // ズレていても、目印をコースの上に出すために使う。
        //
        // 1. searchRadius ピクセルの範囲で、一番近い道路を探す
        // 2. そこから「道路の縁から遠い方」へ登って、幅の真ん中まで動く
        // 　（道路に沿った位置は、ほとんど変わらない）
        //
        // x, y … ピクセル座標（小数でよい。ピクセルの中心は +0.5）
        // 近くに道路が無ければ false を返し、snappedX/Y には元の値を入れる。
        //
        // =====================================================

        /// <summary>
        /// 点（x, y）を、一番近い道路の、幅の真ん中に寄せる。
        /// </summary>
        /// <param name="mask">道路マスク</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="x">寄せる点のX（ピクセル座標。小数でよい）</param>
        /// <param name="y">寄せる点のY（ピクセル座標。小数でよい）</param>
        /// <param name="searchRadius">一番近い道路を探す範囲（ピクセル）</param>
        /// <param name="snappedX">寄せた後のX（ピクセル座標）。寄せられなければ x のまま</param>
        /// <param name="snappedY">寄せた後のY（ピクセル座標）。寄せられなければ y のまま</param>
        /// <returns>寄せられたら true。近くに道路が無ければ false</returns>
        public static bool SnapToRoadCenter(
            byte[] mask,
            int w,
            int h,
            float x,
            float y,
            int searchRadius,
            out float snappedX,
            out float snappedY)
        {
            snappedX = x;
            snappedY = y;

            if (mask == null || w <= 0 || h <= 0 || mask.Length < w * h)
            {
                return false;
            }

            // 画像から探す範囲より遠く離れている（または数値でない）なら、探さない
            float reach = searchRadius + 1f;

            if (!(x >= -reach && x <= w + reach && y >= -reach && y <= h + reach))
            {
                return false;
            }

            // 点がいるピクセルの番号
            int centerX = Mathf.FloorToInt(x);
            int centerY = Mathf.FloorToInt(y);

            // 1. 一番近い道路ピクセルを探す
            //    start ＝ 見つかった道路ピクセルの添字（まだ無い間は -1）
            //    bestDistance ＝ 今までで一番近い距離の2乗
            //    limit ＝ 探す範囲（半径 searchRadius）の2乗
            int start = -1;
            float bestDistance = float.MaxValue;
            float limit = (searchRadius + 0.5f) * (searchRadius + 0.5f);

            // 画像の外は調べない（探す範囲が画像より大きくても無駄に回らない）
            int fromX = Mathf.Max(0, centerX - searchRadius);
            int toX = Mathf.Min(w - 1, centerX + searchRadius);
            int fromY = Mathf.Max(0, centerY - searchRadius);
            int toY = Mathf.Min(h - 1, centerY + searchRadius);

            for (int j = fromY; j <= toY; j++)
            {
                for (int i = fromX; i <= toX; i++)
                {
                    if (mask[j * w + i] == 0)
                    {
                        continue;
                    }

                    // ピクセルの中心（+0.5）と、点との距離の2乗を求める
                    float dx = (i + 0.5f) - x;
                    float dy = (j + 0.5f) - y;

                    float distance = dx * dx + dy * dy;

                    // 探す範囲（丸）の外は使わない
                    if (distance > limit)
                    {
                        continue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        start = j * w + i;
                    }
                }
            }

            if (start < 0)
            {
                return false;
            }

            // 2. 縁から遠い方へ登る（隣に今より縁から遠いピクセルが無くなったら終わり）
            // clearance ＝ 各ピクセルの「道路の縁（道路でない所）からの遠さ」。
            // 道路の真ん中ほど大きな値になる
            int[] clearance = ComputeClearance(mask, w, h);

            // まわり8方向の動き（stepX と stepY を組にして使う）。
            // 右・左・上・下を先に調べる（同じ遠さなら、斜めよりまっすぐ進む）
            int[] stepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] stepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

            // 今いるピクセルの添字。start（一番近い道路）から登り始める
            int current = start;

            // 1歩ずつ、縁からの遠さが増える隣へ動く（登れなくなるか、上限に達したら終わり）
            for (int step = 0; step < MaxClimbSteps; step++)
            {
                int cx = current % w;
                int cy = current / w;

                // next ＝ 次に動く先（動ける先が無い間は、今の場所のまま）
                int next = current;
                int nextClearance = clearance[current];

                for (int k = 0; k < stepX.Length; k++)
                {
                    int nx = cx + stepX[k];
                    int ny = cy + stepY[k];

                    if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    {
                        continue;
                    }

                    int n = ny * w + nx;

                    // 道路の上で、今の候補より縁から遠いピクセルがあれば、次の候補にする
                    if (mask[n] != 0 && clearance[n] > nextClearance)
                    {
                        nextClearance = clearance[n];
                        next = n;
                    }
                }

                // どの隣も今より遠くなければ、ここが一番真ん中（山の頂上）なので、登るのをやめる
                if (next == current)
                {
                    break;
                }

                current = next;
            }

            // ピクセルの番号を、ピクセルの中心の座標（+0.5）に直して返す
            snappedX = (current % w) + 0.5f;
            snappedY = (current / w) + 0.5f;

            return true;
        }

        // 登る回数の上限（広い場所で延々と動き続けないように）
        private const int MaxClimbSteps = 128;

        // =====================================================
        // 各ピクセルから、一番近い「道路でないピクセル」までの距離
        // =====================================================
        //
        // 縦横の1歩を3、斜めの1歩を4と数える簡易的な距離変換
        // （実際の距離の約3倍の整数）。
        // 道路でないピクセルは0。画像の外は「道路でない」扱い。
        //
        // =====================================================

        /// <summary>
        /// 各ピクセルについて、一番近い「道路でないピクセル」までの距離を求める。
        /// </summary>
        /// <param name="mask">道路マスク</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <returns>距離の表（実際の距離の約3倍の整数。道路でない所は0）</returns>
        private static int[] ComputeClearance(byte[] mask, int w, int h)
        {
            // 「まだ距離が分かっていない」ことを表す、十分に大きな値（約2.7億）
            const int Far = 1 << 28;

            // d ＝ 距離の表。最初は、道路なら Far、道路でない所は0にしておく
            int[] d = new int[mask.Length];

            for (int i = 0; i < d.Length; i++)
            {
                d[i] = mask[i] != 0 ? Far : 0;
            }

            // 1回目：左下から右上へ進む。すでに距離が決まった「左・下・左下・右下」の隣から
            // 距離を伝える（縦横の隣は+3、斜めの隣は+4）
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;

                    if (d[i] == 0)
                    {
                        continue;
                    }

                    // 4つの隣から伝わってきた距離（隣の距離 +3 か +4）のうち、一番小さいものを採る
                    int best = d[i];

                    best = Mathf.Min(best, ClearanceAt(d, w, h, x - 1, y) + 3);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x, y - 1) + 3);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x - 1, y - 1) + 4);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x + 1, y - 1) + 4);

                    d[i] = best;
                }
            }

            // 2回目：右上から左下へ戻る。今度は「右・上・右上・左上」の隣から伝える。
            // この2回で、全方向からの近さが反映される
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;

                    if (d[i] == 0)
                    {
                        continue;
                    }

                    int best = d[i];

                    best = Mathf.Min(best, ClearanceAt(d, w, h, x + 1, y) + 3);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x, y + 1) + 3);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x + 1, y + 1) + 4);
                    best = Mathf.Min(best, ClearanceAt(d, w, h, x - 1, y + 1) + 4);

                    d[i] = best;
                }
            }

            return d;
        }

        /// <summary>
        /// 距離の表 d の (x, y) の値を返す。画像の外は 0（道路でない）として扱う。
        /// </summary>
        /// <param name="d">距離の表</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="x">調べる位置のX</param>
        /// <param name="y">調べる位置のY</param>
        /// <returns>その位置の距離。画像の外なら 0</returns>
        private static int ClearanceAt(int[] d, int w, int h, int x, int y)
        {
            if (x < 0 || x >= w || y < 0 || y >= h)
            {
                return 0;
            }

            return d[y * w + x];
        }


        // =====================================================
        // ■ 道路ピクセル数
        // =====================================================

        /// <summary>
        /// 道路のピクセル（0でないもの）の数を数える。
        /// </summary>
        /// <param name="mask">道路マスク</param>
        /// <returns>道路のピクセル数</returns>
        public static int CountPixels(byte[] mask)
        {
            int count = 0;

            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }


        // =====================================================
        // ■ テクスチャ生成
        // =====================================================
        //
        // 道路の中身 … fill色
        // 道路の外周 … outline色（outlinePx ピクセルの太さ）
        // それ以外   … 透明
        //
        // 透明ピクセルのRGBも縁取りと同じ色にしておく
        // （補間で縁が黒ずんで見えるのを防ぐため）
        //
        // =====================================================

        /// <summary>
        /// 道路マスクから、ミニマップに表示するコース画像（道路の塗り＋縁取り）を作る。
        /// </summary>
        /// <param name="mask">道路マスク</param>
        /// <param name="w">画像の幅（ピクセル）</param>
        /// <param name="h">画像の高さ（ピクセル）</param>
        /// <param name="fill">道路の中身の色</param>
        /// <param name="outline">縁取りの色</param>
        /// <param name="outlinePx">縁取りの太さ（ピクセル）。0なら縁取りなし</param>
        /// <returns>作ったテクスチャ（背景は透明）。不要なら呼び出し側で Destroy する</returns>
        public static Texture2D BuildTexture(
            byte[] mask,
            int w,
            int h,
            Color fill,
            Color outline,
            int outlinePx)
        {
            // outer ＝ 道路を、縁取りの太さぶん太らせた形（道路＋縁取りを合わせた範囲）。
            // 縁取りが0なら、道路のまま
            byte[] outer =
                outlinePx > 0
                    ? Dilate(mask, w, h, outlinePx)
                    : mask;

            // Color（0〜1の小数）を、テクスチャ用の Color32（0〜255の整数）に変換する
            Color32 fillColor = fill;
            Color32 outlineColor = outline;

            // 透明ピクセルの色（アルファ0）。
            // RGBは縁取りと同じにしておく（拡大縮小の補間で、縁が黒ずんで見えるのを防ぐ）
            Color32 clearColor =
                new Color32(outlineColor.r, outlineColor.g, outlineColor.b, 0);

            Color32[] pixels = new Color32[w * h];

            // ピクセルごとに、道路の中なら塗りの色、縁取りの範囲なら縁取りの色、
            // それ以外は透明にする
            for (int i = 0; i < pixels.Length; i++)
            {
                if (mask[i] != 0)
                {
                    pixels[i] = fillColor;
                }
                else if (outer[i] != 0)
                {
                    pixels[i] = outlineColor;
                }
                else
                {
                    pixels[i] = clearColor;
                }
            }

            // RGBA32 のテクスチャを作る（false ＝ ミップマップ［縮小用の小さい画像］は作らない）
            Texture2D texture =
                new Texture2D(w, h, TextureFormat.RGBA32, false);

            // テクスチャの設定
            //   name ＝ エディタ上で見分けるための名前
            //   filterMode ＝ 拡大縮小の時に、なめらかに補間する（Bilinear）
            //   wrapMode ＝ 端の外側は端の色を延ばす（反対側の色が縁ににじまないようにする）
            texture.name = "RaceMinimapCourse";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // ピクセルを書き込んで、画面に使える状態にする
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return texture;
        }
    }
}