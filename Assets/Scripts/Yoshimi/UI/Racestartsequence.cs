using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Unityエディタの中だけで使う機能（下の右クリックメニュー用）。
// ビルドした製品には含まれない
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レース開始前の演出（マリカー風）
    // =====================================================================
    //
    // 再生すると、次の順番で進む。
    //
    //   ① 暗転から明転
    //   ② カメラ演出：車の左前の低い位置から、車の後ろへ回り込みながら
    //      上昇し、いつもの追従カメラの位置に着く
    //   ③ 信号が上から降りてくる
    //   ④ カウントダウン「3」「2」「1」（1つ出るたびに赤いランプが点き、
    //      電子音が鳴る。このタイミングで周回・順位・ミニマップが現れる）
    //   ⑤ 「GO!」… 信号が青になり、最後の高い音が鳴り、車の操作が可能になる
    //      （画面が少し広がり、スピード線が走る）
    //
    // 演出中は、シーンにある全ての CarMovement を止めておく。
    // （CarManager.DisableControl と同じ「CarMovementを無効にする」方法。
    // 　再び有効にした時にCarMovementが速度などを0に戻すので、
    // 　止まった状態から走り出せる）
    //
    // ---------------------------------------------------------------------
    // ■ 使い方
    // ---------------------------------------------------------------------
    //
    // Hierarchy の右クリック → Race → Start Sequence で、レースのシーンに1つ置く。
    // （または空のGameObjectにこのコンポーネントを付ける）
    //
    // 車・追従カメラ（RaceCameraFollow）・HUD は自動で見つけるので、
    // 他のオブジェクトを設定する必要はありません。既存のスクリプトも
    // 書き換えません。
    //
    // 見つけるもの
    //   ・CarMovement            … 演出中は操作できないようにする
    //   ・RaceCameraFollow       … カメラ演出で一時的に動かす。終わったら元に戻す
    //                              （分割画面なら、各カメラが自分の車の周りを回る）
    //   ・LapUI / RankUI / RaceMinimap … 「3」が出るタイミングで表示する
    //
    // ---------------------------------------------------------------------
    // ■ 必要なファイル（2つを同じフォルダに入れる）
    // ---------------------------------------------------------------------
    //
    //   RaceStartSequence.cs     … この演出の本体（このファイル）
    //   StartSequenceAssets.cs   … 信号の画像・スピード線・電子音を、コードで作る部品
    //
    // 画像ファイルや音ファイルは要りません。
    // 数字と GO! の文字には TextMeshPro を使います。
    //
    // ---------------------------------------------------------------------
    // ■ 時間割（標準設定。Inspector で変えられます）
    // ---------------------------------------------------------------------
    //
    //   0.0秒        暗転から明るくなり始める（0.6秒で明転）
    //   0.0〜2.4秒   カメラ演出
    //   2.6秒ごろ    信号が上から降りてくる
    //   4.0秒        「3」  赤ランプ1つ・ピッ（周回・順位・ミニマップが現れる）
    //   5.0秒        「2」  赤ランプ2つ・ピッ
    //   6.0秒        「1」  赤ランプ3つ・ピッ
    //   7.0秒        「GO!」 青ランプ・高い音・車が動ける（画角が一瞬広がり、スピード線）
    //   8.2秒        演出が終わる
    //
    // カメラ演出の最中に Enter（ゲームパッドはスタートボタン）を押すと、
    // カメラ演出を飛ばせます（何度も試す時のため。Inspector で切れます）。
    //
    // ---------------------------------------------------------------------
    // ■ 他のスクリプトから使う場合
    // ---------------------------------------------------------------------
    //
    //   m_PlayOnStart を OFF にして、車やカメラを作り終えたあとで Begin() を呼ぶ。
    //   HasRaceStarted … GO以降は true
    //   RaceTime       … GOからの経過秒（タイマー表示などに）
    //   m_OnRaceStarted … GOの瞬間に呼ばれるイベント（BGM開始・タイマー開始など）
    //
    // ※ レース全体の進行を管理するスクリプトが車の CarMovement を
    //   有効にする処理（CarManager.EnableControl など）を、演出中に
    //   呼ばないようにしてください。呼ばれても、このスクリプトが無効に
    //   戻して GO! の瞬間に有効にしますが、Console に警告が出ます。
    //
    // ※ 演出の途中でエラーが出た場合は、Console にエラーを出したうえで、
    //   演出をやめて車を動けるように戻します
    //   （演出の不具合で、レースが始められなくなることはありません）。
    //
    // =====================================================================


    // =====================================================================
    // ■ 演出の時間割（Unityに依存しない計算だけ。検証しやすいよう切り出してある）
    // =====================================================================

    /// <summary>
    /// 演出の時間割。設定の値から、各出来事の時刻を決める
    /// （信号が降りる・数字が出る・GO!・終わり）。
    /// 時刻は、すべて「演出が始まってからの秒数」。
    /// Unityの画面には触らない計算だけなので、検証しやすい。
    /// </summary>
    public sealed class StartSequenceTimeline
    {
        // 信号が降りてくるまでの間（カメラ演出が終わってから）
        public const float SignalDelay = 0.15f;

        // 信号が降りてくる時間・戻っていく時間
        public const float SignalSlideIn = 0.55f;
        public const float SignalSlideOut = 0.45f;

        // GOから信号が戻り始めるまでの間
        public const float SignalLeaveDelay = 0.5f;

        // カメラ演出の終わり（カメラ演出なしなら0）
        public readonly float IntroEnd;

        // 信号が降り始める時刻
        public readonly float SignalIn;

        // 「3」「2」「1」が出る時刻（要素数＝数字の数）
        public readonly float[] CountAt;

        // 数字1つの表示時間（次の数字が出るまでの間隔）
        public readonly float CountInterval;

        // GO! の時刻（車が動けるようになる時刻）
        public readonly float GoAt;

        // 信号が戻り始める時刻
        public readonly float SignalOut;

        // 演出全体の終わり
        public readonly float End;

        /// <summary>
        /// 設定の値から、時間割を決める。範囲外の値は、下限（または上限）にそろえる。
        /// </summary>
        /// <param name="playIntro">カメラ演出をするか</param>
        /// <param name="introDuration">カメラ演出の長さ（秒）</param>
        /// <param name="holdAfterIntro">カメラ演出が終わってから、最初の数字までの間（秒）</param>
        /// <param name="countFrom">カウントダウンを始める数字（3 なら 3・2・1）</param>
        /// <param name="countInterval">数字が出る間隔（秒）</param>
        /// <param name="goDuration">GO! が出てから、演出が終わるまでの最低の長さ（秒）</param>
        public StartSequenceTimeline(
            bool playIntro,
            float introDuration,
            float holdAfterIntro,
            int countFrom,
            float countInterval,
            float goDuration)
        {
            // 数字の数は、1〜9 にそろえる
            int count = Mathf.Clamp(countFrom, 1, 9);

            // 短すぎる値や、マイナスの値は、下限にそろえる（演出が壊れないように）
            countInterval = Mathf.Max(0.2f, countInterval);
            holdAfterIntro = Mathf.Max(0f, holdAfterIntro);
            introDuration = Mathf.Max(0.1f, introDuration);
            goDuration = Mathf.Max(0.5f, goDuration);

            CountInterval = countInterval;
            IntroEnd = playIntro ? introDuration : 0f;

            // first ＝ 最初の数字（「3」など）が出る時刻
            float first = IntroEnd + holdAfterIntro;

            // 信号が降り始める時刻。カメラ演出が終わった少し後（SignalDelay）にする。
            // ただし、それだと最初の数字までに降り切れない時は、降り切れる時刻まで早める
            // （0秒より前にはならない）
            SignalIn =
                Mathf.Min(
                    IntroEnd + SignalDelay,
                    Mathf.Max(0f, first - SignalSlideIn));

            // CountAt[i] ＝ i番目の数字が出る時刻（最初の数字から、間隔ごとに並ぶ）
            CountAt = new float[count];

            for (int i = 0; i < count; i++)
            {
                CountAt[i] = first + i * countInterval;
            }

            // GO! は、最後の数字の次の時刻。信号は、GO! の少し後に戻り始める
            GoAt = first + count * countInterval;
            SignalOut = GoAt + SignalLeaveDelay;

            // 演出の終わり ＝ GO! から goDuration 秒後と、信号が戻り終わる時刻の、遅い方
            End =
                Mathf.Max(
                    GoAt + goDuration,
                    SignalOut + SignalSlideOut);
        }
    }


    // =====================================================================
    // ■ 演出の動き（Unityに依存しない計算だけ）
    // =====================================================================

    /// <summary>
    /// 演出の「動き」（透明度・大きさ・信号の位置・カメラの経路）を決める計算。
    /// Unityの画面には触らない計算だけなので、検証しやすい。
    /// </summary>
    public static class StartSequenceMath
    {
        // ---- 数字（3・2・1）----
        public const float NumeralFadeIn = 0.12f;      // 現れるまで
        public const float NumeralFadeOut = 0.22f;     // 消えるまで
        public const float NumeralStartScale = 0.55f;  // 出始めの大きさ
        public const float NumeralGrow = 0.12f;        // 表示中にゆっくり大きくなる量

        // ---- GO! ----
        public const float GoPopTime = 0.20f;          // 大きい状態から縮んで定位置に来るまで
        public const float GoHoldTime = 0.55f;         // そのまま表示
        public const float GoFadeTime = 0.35f;         // 消えるまで
        public const float GoStartScale = 2.5f;        // 出始めの大きさ
        public const float GoStartAlpha = 0.25f;       // 出始めの濃さ
        public const float GoGrow = 0.15f;             // 表示中にゆっくり大きくなる量

        /// <summary>
        /// GO! の文字が出始めてから、消えるまでの合計の時間（秒）。
        /// </summary>
        public static float GoTotalTime
        {
            get { return GoPopTime + GoHoldTime + GoFadeTime; }
        }

        // ---- 動きの形 ----

        /// <summary>
        /// 最初は速く、だんだんゆっくりになる動き（イージング）。
        /// </summary>
        /// <param name="t">進み具合（0〜1。範囲外は、0か1にそろえる）</param>
        /// <returns>動きの割合（0〜1）</returns>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);

            float u = 1f - t;

            return 1f - u * u * u;
        }

        /// <summary>
        /// 最初はゆっくり、だんだん速くなる動き（イージング）。
        /// </summary>
        /// <param name="t">進み具合（0〜1。範囲外は、0か1にそろえる）</param>
        /// <returns>動きの割合（0〜1）</returns>
        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);

            return t * t * t;
        }

        /// <summary>
        /// 行き過ぎてから戻る動き（信号が降りてきた時の「ぽよん」）。
        /// 終わりに近づくと、1を少し超えてから、1に戻る。
        /// </summary>
        /// <param name="t">進み具合（0〜1。範囲外は、0か1にそろえる）</param>
        /// <returns>動きの割合（途中で、1を少し超える）</returns>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);

            // c1 ＝ 行き過ぎる量を決める定数（1.70158 で、約10%行き過ぎる。
            // アニメーションで、よく使われる値）
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            float u = t - 1f;

            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>
        /// 0 → 1 に素早く立ち上がり、なめらかに 0 へ戻る山形の動き。
        /// GO! の画角の広がりや、スピード線の濃さに使う。
        /// </summary>
        /// <param name="seconds">山が始まってからの秒数</param>
        /// <param name="duration">山の全体の長さ（秒）</param>
        /// <returns>山の高さ（0〜1）。時間の外は 0</returns>
        public static float Pulse(float seconds, float duration)
        {
            // 時間の外（始まる前・終わった後）は 0
            if (duration <= 0f || seconds < 0f || seconds >= duration)
            {
                return 0f;
            }

            // attack ＝ 全体の長さのうち、立ち上がりに使う割合（15%）
            const float attack = 0.15f;

            // u ＝ 全体の何割まで進んだか（0〜1）
            float u = seconds / duration;

            // 立ち上がり：0 → 1 へ、素早く上がる（EaseOutCubic）
            if (u < attack)
            {
                return EaseOutCubic(u / attack);
            }

            // 下り：d ＝ 下りの進み具合（0〜1）。1 → 0 へ、なめらかに下がる（smoothstep）
            float d = (u - attack) / (1f - attack);

            return 1f - d * d * (3f - 2f * d);
        }

        // ---- 数字 ----
        /// <summary>
        /// 数字（3・2・1）の、透明度と大きさを求める。
        /// 出る時に小さい所から素早く大きくなり、しばらく表示して、少し大きくなりながら消える。
        /// </summary>
        /// <param name="seconds">その数字が出てからの秒数</param>
        /// <param name="interval">次の数字が出るまでの間隔（秒）</param>
        /// <param name="alpha">（出力）透明度（0〜1）</param>
        /// <param name="scale">（出力）大きさの倍率（1 ＝ 定位置の大きさ）</param>
        public static void Numeral(
            float seconds,
            float interval,
            out float alpha,
            out float scale)
        {
            // 表示する時間の外なら、見えない状態（透明）にして終わる
            if (seconds < 0f || seconds >= interval)
            {
                alpha = 0f;
                scale = 1f;
                return;
            }

            // 透明度 a の計算：
            //   出てすぐは、0 → 1 へ増やす（NumeralFadeIn 秒かけて）
            //   次の数字が出る前の NumeralFadeOut 秒間は、1 → 0 へ減らす
            //   両方に当てはまる時は、小さい方を使う
            float a = 1f;

            if (seconds < NumeralFadeIn)
            {
                a = seconds / NumeralFadeIn;
            }

            float b = (interval - seconds) / NumeralFadeOut;

            if (b < a)
            {
                a = b;
            }

            alpha = Mathf.Clamp01(a);

            // 大きさの計算：
            //   出てすぐは、小さい所（NumeralStartScale）から、素早く 1 まで大きくする
            //   そのあとは、次の数字が出るまで、ゆっくり大きくする（NumeralGrow ぶん）
            if (seconds < NumeralFadeIn)
            {
                scale =
                    Mathf.Lerp(
                        NumeralStartScale,
                        1f,
                        EaseOutCubic(seconds / NumeralFadeIn));
            }
            else
            {
                // span ＝ ゆっくり大きくなる時間（0で割らないように、最低でも 0.0001）
                float span = Mathf.Max(0.0001f, interval - NumeralFadeIn);

                scale =
                    1f +
                    NumeralGrow *
                    Mathf.Clamp01((seconds - NumeralFadeIn) / span);
            }
        }

        // ---- GO! ----
        /// <summary>
        /// GO! の文字の、透明度と大きさを求める。
        /// 画面いっぱいの大きさ・薄い状態から、縮みながら定位置へ来て、
        /// しばらく表示して、少し大きくなりながら消える。
        /// </summary>
        /// <param name="seconds">GO! が出てからの秒数</param>
        /// <param name="alpha">（出力）透明度（0〜1）</param>
        /// <param name="scale">（出力）大きさの倍率（1 ＝ 定位置の大きさ）</param>
        public static void Go(
            float seconds,
            out float alpha,
            out float scale)
        {
            float total = GoTotalTime;

            // 表示する時間の外なら、見えない状態にして終わる
            if (seconds < 0f || seconds >= total)
            {
                alpha = 0f;
                scale = 1f;
                return;
            }

            // 前半：大きく薄い状態（GoStartScale・GoStartAlpha）から、縮みながら濃くなって、
            // 定位置（大きさ1・濃さ1）へ来る
            if (seconds < GoPopTime)
            {
                float u = seconds / GoPopTime;

                alpha = Mathf.Lerp(GoStartAlpha, 1f, u);
                scale = Mathf.Lerp(GoStartScale, 1f, EaseOutCubic(u));
            }
            else
            {
                // 後半：しばらくそのまま表示して（GoHoldTime）、そのあと消える（GoFadeTime）。
                // u ＝ 後半の進み具合（0〜1）。後半の間、少しずつ大きくなる（GoGrow ぶん）
                float u =
                    Mathf.Clamp01(
                        (seconds - GoPopTime) /
                        (GoHoldTime + GoFadeTime));

                scale = 1f + GoGrow * u;

                // fadeStart ＝ 消え始める時刻。それまでは濃さ1で、そのあと 1 → 0 へ減らす
                float fadeStart = GoPopTime + GoHoldTime;

                alpha =
                    seconds < fadeStart
                        ? 1f
                        : 1f - (seconds - fadeStart) / GoFadeTime;
            }

            alpha = Mathf.Clamp01(alpha);
        }

        // ---- 信号 ----
        /// <summary>
        /// 信号の縦の位置の割合を求める（0 ＝ 画面の上に隠れている、1 ＝ 定位置）。
        /// 降りてくる時は、少し行き過ぎる（1を超える）ことがある。
        /// </summary>
        /// <param name="clock">演出が始まってからの秒数</param>
        /// <param name="arriveAt">信号が降り始める時刻</param>
        /// <param name="leaveAt">信号が戻り始める時刻</param>
        /// <returns>位置の割合（0 ＝ 隠れている、1 ＝ 定位置）</returns>
        public static float SignalPresence(
            float clock,
            float arriveAt,
            float leaveAt)
        {
            // 降りてくる動き（EaseOutBack ＝ 少し行き過ぎてから、定位置へ戻る）
            float p =
                EaseOutBack(
                    (clock - arriveAt) /
                    StartSequenceTimeline.SignalSlideIn);

            // 戻り始めたら、上へ戻していく（EaseInCubic ＝ だんだん速く）。
            // b ＝ 戻りの進み具合（0〜1）
            if (clock > leaveAt)
            {
                float b =
                    EaseInCubic(
                        (clock - leaveAt) /
                        StartSequenceTimeline.SignalSlideOut);

                p *= 1f - b;
            }

            return p;
        }

        // ---- カメラ演出 ----
        /// <summary>
        /// カメラ演出の、カメラの位置と向きを求める。
        /// 追従カメラ（RaceCameraFollow）が最終的に落ち着く位置・向きを終点にして、
        /// 「車の左前の低い位置」から「車の後ろ」へ、車の周りを回りながら距離と高さを
        /// 広げていく経路の、u（0〜1）における位置と向きを返す。
        /// 終点は RaceCameraFollow の SetTarget と同じ式で求めているので、u = 1 のときは
        /// 追従カメラの定位置とぴったり一致する（演出が終わって追従カメラへ引き継いでも、
        /// 画面が飛ばない）。
        /// </summary>
        /// <param name="targetPosition">車の位置</param>
        /// <param name="targetYaw">車のY角度（度）</param>
        /// <param name="chaseOffset">追従カメラの、車からの相対位置（Offset）</param>
        /// <param name="lookAtHeight">追従カメラが見つめる高さ（Look At Height）</param>
        /// <param name="followYaw">追従カメラが、車の向きに合わせて回り込むか</param>
        /// <param name="startAngle">始点の角度（度）。0＝正面、マイナス＝左前、プラス＝右前</param>
        /// <param name="startDistanceRatio">始点の距離（追従カメラの水平距離への倍率）</param>
        /// <param name="startHeightRatio">始点の高さ（追従カメラの高さへの倍率）</param>
        /// <param name="startLookRatio">始点で見つめる高さ（追従カメラの見る高さへの倍率）</param>
        /// <param name="u">経路の進み具合（0 ＝ 始点、1 ＝ 終点）</param>
        /// <param name="position">（出力）カメラの位置</param>
        /// <param name="rotation">（出力）カメラの向き</param>
        public static void IntroPose(
            Vector3 targetPosition,
            float targetYaw,
            Vector3 chaseOffset,
            float lookAtHeight,
            bool followYaw,
            float startAngle,
            float startDistanceRatio,
            float startHeightRatio,
            float startLookRatio,
            float u,
            out Vector3 position,
            out Quaternion rotation)
        {
            // carYaw ＝ 車の向き（Y軸まわりの回転だけ）
            Quaternion carYaw = Quaternion.Euler(0f, targetYaw, 0f);

            // chaseYaw ＝ 追従カメラの相対位置（chaseOffset）に掛ける回転。
            // 車の向きに合わせて回り込む設定なら、車の向き。そうでなければ、回転なし
            Quaternion chaseYaw =
                followYaw
                    ? carYaw
                    : Quaternion.identity;

            // 追従カメラの定位置を「車から見た向き」で表す
            Vector3 endLocal =
                Quaternion.Inverse(carYaw) *
                (chaseYaw * chaseOffset);

            // 終点（追従カメラの定位置）を、車を中心にした「距離・高さ・角度」で表す。
            // endRadius ＝ 終点の、車からの水平の距離
            float endRadius =
                Mathf.Sqrt(
                    endLocal.x * endLocal.x +
                    endLocal.z * endLocal.z);

            float endHeight = endLocal.y;  // 終点の高さ

            // 車の正面が0度、右が90度、真後ろが180度
            float endAngle =
                Mathf.Atan2(endLocal.x, endLocal.z) *
                Mathf.Rad2Deg;

            // 始点の側から、車の後ろへ回り込む向きに、終点の角度をそろえる。
            //   a0 ＝ 始点の角度、a1 ＝ 終点の角度
            //   左前（マイナス）から始める時は、角度が減る向きに回る（車の左側を通る）
            //   右前（プラス）から始める時は、角度が増える向きに回る（車の右側を通る）
            // Repeat ＝ 0〜360 の範囲に収める（回る量を、1周以内にする）
            float a0 = startAngle;
            float a1;

            if (a0 < 0f)
            {
                a1 = a0 - Mathf.Repeat(a0 - endAngle, 360f);
            }
            else
            {
                a1 = a0 + Mathf.Repeat(endAngle - a0, 360f);
            }

            // 始点の距離・高さ・見つめる高さ（終点の値に、倍率を掛けて求める）
            float r0 = endRadius * startDistanceRatio;
            float h0 = endHeight * startHeightRatio;
            float look0 = lookAtHeight * startLookRatio;

            // 経路の進み具合 u での、角度・距離・高さ・見つめる高さ。
            // LerpUnclamped ＝ u が 0〜1 の外でも、そのまま計算する
            // （カーブが行き過ぎても、おかしくならない）
            float angle = Mathf.LerpUnclamped(a0, a1, u);
            float radius = Mathf.LerpUnclamped(r0, endRadius, u);
            float height = Mathf.LerpUnclamped(h0, endHeight, u);
            float lookHeight = Mathf.LerpUnclamped(look0, lookAtHeight, u);

            float rad = angle * Mathf.Deg2Rad;

            // local ＝ 車から見た、カメラの位置（車の正面が +Z、右が +X）。
            // 角度と距離を、sin・cos で、X・Z に直す
            Vector3 local =
                new Vector3(
                    radius * Mathf.Sin(rad),
                    height,
                    radius * Mathf.Cos(rad));

            // 車の向きを掛けて、ワールドの位置にする（車の位置に足す）
            position = targetPosition + carYaw * local;

            // 見つめる点 ＝ 車の位置から、lookHeight だけ上
            Vector3 lookPoint =
                targetPosition +
                new Vector3(0f, lookHeight, 0f);

            Vector3 toTarget = lookPoint - position;

            // カメラが見る点と重なってしまった時だけ、車の正面を向く
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = carYaw * Vector3.forward;
            }

            // 見つめる点の方向を向く（上方向は、ワールドの上）
            rotation =
                Quaternion.LookRotation(
                    toTarget.normalized,
                    Vector3.up);
        }
    }


    // =====================================================================
    // ■ 本体
    // =====================================================================

    /// <summary>
    /// レース開始前の演出（マリカー風）の本体。
    /// 暗転 → カメラ演出 → 信号 → カウントダウン → GO! の順に進めて、
    /// GO! の瞬間に、車を動けるようにする。
    /// 使い方や時間割は、このファイルの先頭の説明を参照。
    /// </summary>
    [DisallowMultipleComponent]  // 同じオブジェクトに2つ付けられない
    [DefaultExecutionOrder(100)]  // 他より後に実行する（追従カメラの後で、カメラを動かすため）
    [AddComponentMenu("Race/Race Start Sequence")]  // Add Component メニューでの場所
    public class RaceStartSequence : MonoBehaviour
    {
        // =================================================================
        // ■ 開始
        // =================================================================

        [Header("開始")]

        [Tooltip("ON：シーンの再生と同時に演出を始めます。\n" +
            "OFF：車やカメラを自分で作る場合など、他のスクリプトから Begin() を呼んだ時に始めます")]
        public bool m_PlayOnStart = true;


        // =================================================================
        // ■ 全体のタイミング（秒）
        // =================================================================

        [Header("全体のタイミング（秒）")]

        [Tooltip("最初の暗転（真っ暗な画面から明るくなるまでの時間）。0で暗転なし")]
        [Min(0f)]
        public float m_FadeInDuration = 0.6f;

        [Tooltip("OFFにすると、カメラ演出を飛ばして、追従カメラのまま信号とカウントダウンだけ行います")]
        public bool m_PlayCameraIntro = true;

        [Tooltip("カメラ演出の長さ")]
        [Min(0.1f)]
        public float m_IntroDuration = 2.4f;

        [Tooltip("カメラ演出が終わってから、「3」が出るまでの間（この間に信号が降りてきます）")]
        [Min(0f)]
        public float m_HoldAfterIntro = 1.6f;

        [Tooltip("カウントダウンを何から始めるか（3なら 3→2→1→GO!）")]
        [Range(1, 9)]
        public int m_CountdownFrom = 3;

        [Tooltip("数字が変わる間隔。マリカーは約1秒")]
        [Min(0.2f)]
        public float m_CountInterval = 1.0f;

        [Tooltip("GO! が出てから、演出全体が終わるまでの最低の長さ（信号が上へ戻る時間もここに含まれます）。\n" +
            "GO! の文字・スピード線・画角の変化が残っていれば、それが終わるまで待ちます")]
        [Min(0.5f)]
        public float m_GoDuration = 1.2f;


        // =================================================================
        // ■ カメラ演出
        // =================================================================

        [Header("カメラ演出")]

        [Tooltip("最初にカメラを置く角度（度）。車の正面が0で、マイナス＝左前、プラス＝右前。\n" +
            "そこから車の横を通って、車の後ろへ回り込みます")]
        [Range(-170f, 170f)]
        public float m_IntroStartAngle = -40f;

        [Tooltip("最初のカメラの、車からの距離。追従カメラの距離に対する倍率です（0.5＝半分の距離まで寄る）")]
        [Range(0.2f, 1.5f)]
        public float m_IntroStartDistance = 0.55f;

        [Tooltip("最初のカメラの高さ。追従カメラの高さに対する倍率です（小さいほど低い位置から見上げる）")]
        [Range(0f, 1f)]
        public float m_IntroStartHeight = 0.12f;

        [Tooltip("最初に見つめる高さ。追従カメラが見る高さ（Look At Height）に対する倍率です")]
        [Range(0f, 1.5f)]
        public float m_IntroStartLookHeight = 0.8f;

        [Tooltip("カメラの進み方。横軸＝演出の進み具合（0〜1）、縦軸＝経路のどこまで進んだか（0〜1）。\n" +
            "初期値は、動き出しが速く、最後にゆっくり落ち着く形です")]
        public AnimationCurve m_IntroCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0.5f, 0.5f),
                new Keyframe(1f, 1f, 0f, 0f));


        // =================================================================
        // ■ 信号・数字の見た目
        // =================================================================

        [Header("信号・数字の見た目")]

        [Tooltip("信号を表示するか")]
        public bool m_ShowSignal = true;

        [Tooltip("信号の横位置（0＝左端、0.5＝中央、1＝右端）。マリカー風にしたい時は0.8くらい")]
        [Range(0f, 1f)]
        public float m_SignalPosition = 0.5f;

        [Tooltip("数字とGO!の文字に使うフォント。空ならTextMeshProの標準フォントを使います")]
        public TMP_FontAsset m_Font;

        [Tooltip("数字（3・2・1）の色")]
        public Color m_NumberColor = new Color(1f, 0.78f, 0.10f, 1f);

        [Tooltip("GO! の色")]
        public Color m_GoColor = new Color(1f, 0.62f, 0.05f, 1f);

        [Tooltip("文字の縁取りの色")]
        public Color m_OutlineColor = Color.white;

        [Tooltip("文字の縁取りの太さ")]
        [Range(0f, 1f)]
        public float m_OutlineWidth = 0.3f;

        [Tooltip("数字の大きさ（画面の高さが1080の時の文字サイズ。GO! はこの1.3倍）")]
        [Min(50f)]
        public float m_NumberSize = 240f;

        [Tooltip("文字の傾き（度）。プラスで左に傾きます")]
        [Range(-20f, 20f)]
        public float m_NumberTilt = 6f;

        [Tooltip("文字を出す高さ。画面の中央からの距離を、画面の高さに対する割合で指定します（プラスで上）")]
        [Range(-0.3f, 0.3f)]
        public float m_NumberHeight = 0.08f;

        [Tooltip("演出用の画面を表示する前後の順番（大きいほど手前）。他のCanvasより手前に出したい時に調整します")]
        public int m_CanvasSortingOrder = 500;


        // =================================================================
        // ■ HUD
        // =================================================================

        [Header("HUD（周回・順位・ミニマップなど）")]

        [Tooltip("演出中はHUDを隠し、「3」が出るタイミングで表示します")]
        public bool m_FadeInHud = true;

        [Tooltip("HUDとして扱うコンポーネントの名前。シーンにこの名前のコンポーネントがあれば、\n" +
            "そのGameObjectを隠して、あとから表示します")]
        public string[] m_HudComponentNames = new string[] { "LapUI", "RankUI", "RaceMinimap" };

        [Tooltip("上の名前では見つからないHUDがあれば、ここに直接入れてください")]
        public GameObject[] m_ExtraHudObjects;

        [Tooltip("HUDが現れるまでの時間")]
        [Min(0.05f)]
        public float m_HudFadeDuration = 0.5f;


        // =================================================================
        // ■ サウンド
        // =================================================================

        [Header("サウンド")]

        [Tooltip("カウントダウンの音とGOの音を鳴らすか")]
        public bool m_PlaySounds = true;

        [Tooltip("音量")]
        [Range(0f, 1f)]
        public float m_Volume = 0.7f;

        [Tooltip("カウントダウン（3・2・1）で鳴らす音。空なら、コードで作った「ピッ」を使います")]
        public AudioClip m_CountdownClip;

        [Tooltip("GO! で鳴らす音。空なら、コードで作った高い「ピーッ」を使います")]
        public AudioClip m_GoClip;


        // =================================================================
        // ■ GOの演出
        // =================================================================

        [Header("GOの演出")]

        [Tooltip("GO! の瞬間にカメラの画角を一瞬だけ広げる量（度）。0で無効")]
        [Range(0f, 30f)]
        public float m_GoFovKick = 8f;

        [Tooltip("画角が広がって元に戻るまでの時間")]
        [Min(0.1f)]
        public float m_GoFovKickDuration = 0.8f;

        [Tooltip("GO! の瞬間にスピード線を走らせるか")]
        public bool m_GoSpeedLines = true;


        // =================================================================
        // ■ スキップ（開発用）
        // =================================================================

        [Header("スキップ（開発用）")]

        [Tooltip("カメラ演出の途中でキーを押すと、カメラ演出を飛ばしてカウントダウンへ進みます")]
        public bool m_AllowSkip = true;

        [Tooltip("スキップに使うキー")]
        public Key m_SkipKey = Key.Enter;

        [Tooltip("ゲームパッドのスタートボタンでもスキップする")]
        public bool m_SkipWithGamepadStart = true;


        // =================================================================
        // ■ イベント
        // =================================================================

        [Header("イベント")]

        [Tooltip("カウントダウンが始まった（「3」が出た）瞬間")]
        public UnityEvent m_OnCountdownStarted = new UnityEvent();

        [Tooltip("GO! の瞬間（車が動けるようになった瞬間）。BGMの開始やタイマーの開始に")]
        public UnityEvent m_OnRaceStarted = new UnityEvent();


        // =================================================================
        // ■ 外から見える状態
        // =================================================================

        /// <summary>演出の最中（車の操作がロックされている間を含む）</summary>
        public bool IsRunning
        {
            get { return m_State == State.Running; }
        }

        /// <summary>GO! を過ぎた（レースが始まった）</summary>
        public bool HasRaceStarted
        {
            get { return m_Released; }
        }

        /// <summary>GO! からの経過秒（GO! の前は0）</summary>
        public float RaceTime
        {
            get
            {
                return
                    m_Released
                        ? Mathf.Max(0f, Time.time - m_RaceStartTime)
                        : 0f;
            }
        }


        // =================================================================
        // ■ 内部で使うクラス
        // =================================================================

        private enum State
        {
            Idle,     // 動いていない（演出の前・後）
            Running   // 演出の最中
        }

        // 操作を止めた車1台ぶんの記録（元に戻すため）
        private sealed class CarLock
        {
            public CarMovement movement;     // 操作を止める対象
            public bool movementWasEnabled;  // 止める前は、有効だったか

            // 演出中に、他のスクリプトが有効にしようとした（GO! の時に有効にする）
            public bool enabledByOthers;

            public Rigidbody body;                    // 車の Rigidbody（無ければ null）
            public RigidbodyConstraints constraints;  // 固定する前の、動きの制限
            public bool locked;                       // いま、ロックしているか
        }

        // 演出で動かすカメラ1台ぶんの記録
        private sealed class CameraRig
        {
            public RaceCameraFollow follow;  // 追従カメラ（RaceCameraFollow）
            public Camera camera;            // そのカメラ
            public bool followWasEnabled;    // 演出の前は、追従カメラが有効だったか
            public bool tookOver;            // 演出が、カメラを動かしている最中か
            public float baseFov;            // 元の画角（度）

            // 画角を動かしたか（動かした時だけ元に戻す）
            public bool fovChanged;
        }

        // 演出中に隠すHUD1つぶんの記録
        private sealed class HudEntry
        {
            public GameObject target;    // 隠すHUDのオブジェクト
            public CanvasGroup group;    // 透明度を動かす CanvasGroup
            public float originalAlpha;  // 元の透明度（元に戻す時に使う）
        }

        // 画面（分割画面なら、1つの区画）ぶんの表示物
        private sealed class Panel
        {
            public RectTransform root;        // 区画の親（カメラの表示範囲に合わせる）
            public Camera camera;             // この区画のカメラ（null ＝ 全画面）

            public bool hasRect;              // 表示範囲を、合わせたことがあるか
            public Rect lastRect;             // 前回合わせた、カメラの表示範囲
            public float scale = 1f;          // 信号・文字の倍率（区画の大きさに合わせる）

            public RectTransform burstRect;   // スピード線の RectTransform
            public Image burst;               // スピード線の画像

            public RectTransform signalRect;  // 信号の RectTransform（降りる動きを付ける）
            public Image[] lights;            // ランプ（点灯の色を付ける）
            public Image[] glows;             // ランプの光彩

            public RectTransform numberRect;  // 数字の RectTransform
            public CanvasGroup numberGroup;   // 数字の透明度（CanvasGroup）
            public TextMeshProUGUI number;    // 数字の文字
            public int shownNumber = -1;      // いま文字に入っている数字（-1 ＝ まだ無い）

            public RectTransform goRect;      // GO! の RectTransform
            public CanvasGroup goGroup;       // GO! の透明度（CanvasGroup）
        }


        // =================================================================
        // ■ 定数
        // =================================================================

        // 1フレームで進める時間の上限（読み込み直後の長いフレームで演出が飛ばないように）
        private const float MaxFrameStep = 0.1f;

        // 開始直後にスキップ入力を受け付けない時間（シーン切り替え時のキー押しっぱなし対策）
        private const float SkipGuardTime = 0.4f;

        // 信号の大きさ（画面の高さが1080の時の大きさ）
        private const float LampSize = 88f;       // ランプ（光る丸）の直径
        private const float BezelSize = 114f;     // ランプの縁（暗い丸）の直径
        private const float GlowSize = 300f;      // ランプの光彩の直径
        private const float LampPitch = 124f;     // ランプの間隔（中心から中心まで）
        private const float BodyPadding = 44f;    // 本体の幅に足す余白
        private const float BodyHeight = 170f;    // 本体の高さ
        private const float PoleWidth = 20f;      // つるす棒の太さ
        private const float PoleHeight = 1400f;   // つるす棒の長さ

        // 信号の位置（上の端からの距離）
        private const float SignalShownY = -34f;              // 降りてきた時（定位置）
        private const float SignalHiddenY = BodyHeight + 40f;  // 隠れている時（画面の上）

        // スピード線
        private const float BurstDuration = 0.9f;   // スピード線が出ている時間（秒）
        private const float BurstMaxAlpha = 0.85f;  // いちばん濃い時の濃さ
        private const float BurstGrow = 0.35f;      // 出ている間に広がる量（大きさ 1 → 1.35）

        // 色（消えているランプ・赤ランプ・青［緑］ランプ／信号の本体・本体の縁取り・つるす棒／
        // ランプの縁・スピード線）
        private static readonly Color LampOff = new Color(0.20f, 0.20f, 0.23f, 1f);
        private static readonly Color LampRed = new Color(1.00f, 0.16f, 0.10f, 1f);
        private static readonly Color LampGreen = new Color(0.25f, 1.00f, 0.42f, 1f);
        private static readonly Color BodyColor = new Color(0.07f, 0.07f, 0.09f, 0.96f);
        private static readonly Color RimColor = new Color(0.72f, 0.72f, 0.78f, 1f);
        private static readonly Color PoleColor = new Color(0.30f, 0.30f, 0.34f, 1f);
        private static readonly Color BezelColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        private static readonly Color SpeedLineColor = new Color(0.88f, 0.96f, 1.00f, 1f);


        // =================================================================
        // ■ 内部変数
        // =================================================================

        private State m_State = State.Idle;  // いまの状態

        // 演出の経過時間
        private float m_Clock;

        private StartSequenceTimeline m_Timeline;  // 今回の時間割（Begin で作る）

        // 演出全体が終わる時刻（GO! 後の文字・スピード線・画角の変化が終わるまで含む）
        private float m_EndTime;

        // 演出中に他のスクリプトが CarMovement を有効にしたことを、1回だけ知らせるため
        private bool m_WarnedLockBreak;

        // 次に鳴らす数字のインデックス
        private int m_NextBeep;

        private bool m_CountdownStarted;  // 「3」が出たか（開始イベントを1回だけ呼ぶため）
        private bool m_Released;          // GO! を過ぎて、車を動けるようにしたか
        private bool m_CamerasHandedOff;  // カメラ演出が終わって、追従カメラへ引き継いだか
        private float m_RaceStartTime;    // GO! の時刻（Time.time）

        private readonly List<CarLock> m_Cars = new List<CarLock>();      // 操作を止めた車
        private readonly List<CameraRig> m_Rigs = new List<CameraRig>();  // 動かすカメラ
        private readonly List<HudEntry> m_Huds = new List<HudEntry>();    // 隠すHUD
        private readonly List<Panel> m_Panels = new List<Panel>();        // 画面の区画

        // 演出用の画面
        private GameObject m_UiObject;  // 演出用の画面（Canvas）のオブジェクト
        private Image m_FadeImage;      // 暗転の黒い画像

        private Sprite m_CircleSprite;  // 丸（ランプ・ランプの縁）の画像
        private Sprite m_GlowSprite;    // ランプの光彩の画像
        private Sprite m_BodySprite;    // 信号の本体（角丸四角）の画像
        private Sprite m_LinesSprite;   // スピード線の画像

        // 音
        private GameObject m_AudioObject;      // 音を鳴らすオブジェクト
        private AudioSource m_Audio;           // 音を鳴らす AudioSource
        private AudioClip m_BeepClip;          // カウントダウンで鳴らす音
        private AudioClip m_GoToneClip;        // GO! で鳴らす音
        private AudioClip m_GeneratedBeep;     // コードで作った「ピッ」
        private AudioClip m_GeneratedGoTone;   // コードで作った GO! の音


        // =================================================================
        // ■ Unityのイベント
        // =================================================================

        /// <summary>
        /// 最初のフレームの前に、1回呼ばれる。m_PlayOnStart がONなら、演出を始める。
        /// </summary>
        private void Start()
        {
            if (m_PlayOnStart)
            {
                Begin();
            }
        }

        /// <summary>
        /// 無効になった時に呼ばれる。演出の最中なら、中断して、車を動けるように戻す。
        /// </summary>
        private void OnDisable()
        {
            // 演出の途中で止められても、車が動かないままにならないようにする
            if (m_State == State.Running)
            {
                Abort();
            }
        }

        /// <summary>
        /// 削除された時に呼ばれる。演出の最中なら中断し、音とコードで作った音を片付ける。
        /// </summary>
        private void OnDestroy()
        {
            if (m_State == State.Running)
            {
                Abort();
            }

            DestroyAudio();
            DestroyGeneratedClips();
        }

        /// <summary>
        /// 毎フレーム、演出を進める。
        /// 途中でエラーが出たら、演出をやめて、車を動けるように戻す（FailSafe）。
        /// </summary>
        private void Update()
        {
            if (m_State != State.Running)
            {
                return;
            }

            try
            {
                // 1フレームで進める時間には、上限を付ける（長いフレームがあっても、
                // 演出が飛ばないように）
                float dt = Mathf.Min(Time.deltaTime, MaxFrameStep);

                // スキップの操作（キー・ゲームパッドのスタートボタン）を調べる
                HandleSkipInput();

                // 他のスクリプトに、車を有効にされていたら、無効に戻す
                EnforceCarLock();

                // 演出の時計を進めて、その時刻にすべき処理（音・イベント・画面など）を行う
                m_Clock += dt;

                Advance();

                // 終わりの時刻になったら、演出を終える。
                // ただし、イベントの処理の中で中断された時（Running でない時）は、何もしない
                if (m_State == State.Running && m_Clock >= m_EndTime)
                {
                    Finish();
                }
            }
            catch (System.Exception e)
            {
                FailSafe(e);
            }
        }

        /// <summary>
        /// 毎フレーム、Update のあとに呼ばれる。カメラの位置と画角を決める。
        /// カメラは、追従カメラ（RaceCameraFollow）の LateUpdate のあとで動かす
        /// （[DefaultExecutionOrder(100)] で、実行の順番を後ろにしてある）。
        /// </summary>
        private void LateUpdate()
        {
            if (m_State != State.Running)
            {
                return;
            }

            try
            {
                // Update のあとで、他のスクリプトに有効にされた分も、ここで無効に戻す
                EnforceCarLock();

                // カメラ演出のカメラの位置・向きと、GO! の画角の変化を反映する
                ApplyIntroCameras();
                ApplyFovKick();
            }
            catch (System.Exception e)
            {
                FailSafe(e);
            }
        }


        // =================================================================
        // ■ 開始・中断
        // =================================================================

        /// <summary>
        /// 演出を最初から始める。車の操作ロック・カメラ演出・信号とカウントダウンを行い、
        /// GO! で車を動けるようにする。
        /// （m_PlayOnStart がONなら、再生と同時に自動で呼ばれる）
        /// </summary>
        public void Begin()
        {
            // すでに演出中なら、何もしない（二重に始めない）
            if (m_State == State.Running)
            {
                return;
            }

            // 設定の値から、時間割を決める
            m_Timeline =
                new StartSequenceTimeline(
                    m_PlayCameraIntro,
                    m_IntroDuration,
                    m_HoldAfterIntro,
                    m_CountdownFrom,
                    m_CountInterval,
                    m_GoDuration);

            // 演出の終わりの時刻 ＝ 時間割の終わりと、GO! の後の効果（文字・スピード線・画角）が
            // 終わる時刻の、遅い方
            m_EndTime =
                Mathf.Max(
                    m_Timeline.End,
                    m_Timeline.GoAt + GoEffectsDuration());

            // 時計と、進み具合の記録を、最初の状態に戻す
            m_Clock = 0f;
            m_NextBeep = 0;
            m_CountdownStarted = false;
            m_Released = false;
            m_RaceStartTime = 0f;
            m_WarnedLockBreak = false;

            try
            {
                // 車 → カメラ → HUD の順に、見つけて押さえる
                CollectCars();
                LockCars();

                CollectRigs();
                TakeOverCameras();

                CollectHud();
                SetHudAlpha(0f);

                // 演出用の画面と、音を用意する
                BuildUi();
                BuildAudio();

                m_State = State.Running;

                // 最初の1フレーム目から、暗転やカメラの位置が正しくなるようにする
                ApplyUi();
                ApplyHud();
                ApplyIntroCameras();
            }
            catch (System.Exception e)
            {
                // 準備の途中で失敗しても、車が動けないままにならないようにする
                FailSafe(e);
            }
        }

        /// <summary>
        /// カメラ演出を飛ばして、カウントダウンへ進む（カメラ演出の最中のみ有効）
        /// </summary>
        public void SkipIntro()
        {
            // 演出中でなければ、何もしない
            if (m_State != State.Running || m_Timeline == null)
            {
                return;
            }

            // カメラ演出の最中なら、時計をカメラ演出の終わりまで進める
            if (m_Clock < m_Timeline.IntroEnd)
            {
                m_Clock = m_Timeline.IntroEnd;
            }
        }

        /// <summary>
        /// 演出を途中で止めて、すぐに車を動けるようにし、カメラ・HUDを元に戻す
        /// （イベントは呼ばれません）
        /// </summary>
        public void Abort()
        {
            if (m_State != State.Running)
            {
                return;
            }

            // 先に状態を戻しておく（OnDisableなどからの再入を防ぐ）
            m_State = State.Idle;

            // 鳴っている音を止める
            if (m_Audio != null)
            {
                m_Audio.Stop();
            }

            Restore();
        }

        /// <summary>
        /// 最後まで進んだ時の後片付けをする（車・カメラ・HUD・画面を、元に戻す）。
        /// </summary>
        private void Finish()
        {
            m_State = State.Idle;

            Restore();
        }

        /// <summary>
        /// 演出の途中（または準備）でエラーが出た時の後始末をする。
        /// 演出が壊れても、車が動けないままにならないよう、押さえていたものを全部元に戻して、
        /// 演出をやめる（レースは始められる）。
        /// </summary>
        /// <param name="error">出たエラー（Console に表示する）</param>
        private void FailSafe(System.Exception error)
        {
            Debug.LogError(
                "RaceStartSequence : 演出でエラーが出たため、演出をやめて車を動けるようにしました。\n" +
                error);

            m_State = State.Idle;

            // 鳴っている音を止める
            if (m_Audio != null)
            {
                m_Audio.Stop();
            }

            Restore();
        }

        /// <summary>
        /// GO! のあとに続く効果（文字・スピード線・画角の変化）が、全部終わるまでの長さを求める。
        /// これが終わる前に片付けてしまうと、途中でぷつっと消えてしまう。
        /// </summary>
        /// <returns>GO! からの長さ（秒）</returns>
        private float GoEffectsDuration()
        {
            float duration = StartSequenceMath.GoTotalTime;  // GO! の文字が消えるまで

            // スピード線を出す設定なら、それが消えるまで
            if (m_GoSpeedLines)
            {
                duration = Mathf.Max(duration, BurstDuration);
            }

            // 画角を広げる設定なら、元に戻るまで
            if (m_GoFovKick > 0f)
            {
                duration = Mathf.Max(duration, m_GoFovKickDuration);
            }

            return duration;
        }

        /// <summary>
        /// 車・カメラ・HUD・画角・演出用の画面を、元の状態へ戻す。
        /// </summary>
        private void Restore()
        {
            // 1つが失敗しても、残りは必ず行う（車を先に動けるようにする）
            RunStep(UnlockCars);
            RunStep(HandOffCameras);
            RunStep(RestoreFov);
            RunStep(RestoreHud);
            RunStep(DestroyUi);
        }

        /// <summary>
        /// 後片付けの1つの処理を実行する。
        /// エラーが出ても、Console に出すだけで止めない（残りの処理を続けるため）。
        /// </summary>
        /// <param name="step">実行する処理</param>
        private static void RunStep(System.Action step)
        {
            try
            {
                step();
            }
            catch (System.Exception e)
            {
                Debug.LogError("RaceStartSequence : 後片付けでエラーが出ました\n" + e);
            }
        }

        /// <summary>
        /// UnityEvent を呼ぶ。他のスクリプトが登録した処理でエラーが出ても、演出は止めない。
        /// </summary>
        /// <param name="unityEvent">呼ぶイベント（null なら、何もしない）</param>
        private static void InvokeEvent(UnityEvent unityEvent)
        {
            if (unityEvent == null)
            {
                return;
            }

            try
            {
                unityEvent.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError("RaceStartSequence : イベントの処理でエラーが出ました\n" + e);
            }
        }


        // =================================================================
        // ■ 1フレームぶんの進行
        // =================================================================

        /// <summary>
        /// 1フレームぶんの進行。時計の時刻に合わせて、その時刻に起こる出来事
        /// （カメラの引き継ぎ・「3」・音・GO!）を処理して、画面とHUDを更新する。
        /// </summary>
        private void Advance()
        {
            StartSequenceTimeline t = m_Timeline;

            // カメラ演出が終わったら、追従カメラへ引き継ぐ
            if (!m_CamerasHandedOff && m_Clock >= t.IntroEnd)
            {
                HandOffCameras();
            }

            // 「3」が出た瞬間
            if (!m_CountdownStarted && m_Clock >= t.CountAt[0])
            {
                m_CountdownStarted = true;

                InvokeEvent(m_OnCountdownStarted);
            }

            // 数字が出る瞬間の音。
            //   due ＝ 時刻を過ぎた数字の数（m_NextBeep ＝ 鳴らし済みの数）
            //   長いフレームで複数の数字をまたいでも、鳴らすのは1回だけ
            int due = m_NextBeep;

            while (due < t.CountAt.Length && m_Clock >= t.CountAt[due])
            {
                due++;
            }

            if (due > m_NextBeep)
            {
                m_NextBeep = due;

                PlaySound(m_BeepClip);
            }

            // GO!（時計が、GO! の時刻に来た）：
            //   車を動けるようにして、音を鳴らし、イベント（m_OnRaceStarted）を呼ぶ
            if (!m_Released && m_Clock >= t.GoAt)
            {
                m_Released = true;
                m_RaceStartTime = Time.time;  // RaceTime（GO! からの経過秒）の基準

                UnlockCars();

                PlaySound(m_GoToneClip);

                InvokeEvent(m_OnRaceStarted);
            }

            // 画面（暗転・信号・数字・GO!）と、HUDの表示を、今の時刻に合わせる
            ApplyUi();
            ApplyHud();
        }

        /// <summary>
        /// 開発用：キー（標準は Enter）か、ゲームパッドのスタートボタンで、カメラ演出を飛ばす。
        /// 押されたかどうかを調べて、押されていれば SkipIntro を呼ぶ。
        /// </summary>
        private void HandleSkipInput()
        {
            if (!m_AllowSkip || m_Timeline == null)
            {
                return;
            }

            // カメラ演出の最中だけ。開始直後は誤爆防止のため受け付けない
            if (m_Clock < SkipGuardTime || m_Clock >= m_Timeline.IntroEnd)
            {
                return;
            }

            bool pressed = false;

            Keyboard keyboard = Keyboard.current;

            // Key.None（未設定）のキーを読もうとすると例外になるので、読まない
            if (keyboard != null &&
                m_SkipKey != Key.None &&
                keyboard[m_SkipKey].wasPressedThisFrame)
            {
                pressed = true;
            }

            // キーが押されていなければ、接続されているゲームパッドのスタートボタンを調べる
            // （どのゲームパッドでもよい）
            if (!pressed && m_SkipWithGamepadStart)
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    Gamepad pad = Gamepad.all[i];

                    if (pad != null && pad.startButton.wasPressedThisFrame)
                    {
                        pressed = true;
                        break;
                    }
                }
            }

            if (pressed)
            {
                SkipIntro();
            }
        }


        // =================================================================
        // ■ 車の操作ロック
        // =================================================================
        //
        // CarMovement を無効にして、入力を受け付けないようにする。
        // CarMovement は無効になっている間は物理を動かさないので、
        // 車が坂やわずかな揺れで勝手に動かないように、
        // Rigidbody の「水平方向の移動」と「回転」も固定しておく
        // （縦方向は固定しないので、地面への着地は普通に行われる）。
        //
        // 解除する時は、固定を元に戻してから CarMovement を有効にする。
        // CarMovement は有効になる時に速度やドリフトの状態を0に戻すので、
        // 止まった状態から走り出す。
        //
        // =================================================================

        /// <summary>
        /// シーンにある全ての車（CarMovement）を見つけて、操作を止める対象のリストに入れる。
        /// 無効になっているオブジェクトは、対象にしない。
        /// </summary>
        private void CollectCars()
        {
            m_Cars.Clear();

            // シーンにある CarMovement を、全部探す（順番は、気にしない）
            CarMovement[] found =
                FindObjectsByType<CarMovement>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null)
                {
                    continue;
                }

                CarLock carLock = new CarLock();

                carLock.movement = found[i];

                m_Cars.Add(carLock);
            }

            // 車が1台も見つからない時は、警告だけ出して続ける（ロックする対象が無いだけ）
            if (m_Cars.Count == 0)
            {
                Debug.LogWarning(
                    "RaceStartSequence : CarMovement が見つかりません。" +
                    "車の操作ロックは行いません");
            }
        }

        /// <summary>
        /// 見つけた車の操作を止める。
        /// CarMovement を無効にして、Rigidbody の水平方向の動きと回転を固定する。
        /// 元に戻す時のために、元の状態を記録しておく。
        /// </summary>
        private void LockCars()
        {
            for (int i = 0; i < m_Cars.Count; i++)
            {
                CarLock carLock = m_Cars[i];

                CarMovement movement = carLock.movement;

                if (movement == null)
                {
                    continue;
                }

                // 元の状態を記録してから、操作（CarMovement）を無効にする
                carLock.movementWasEnabled = movement.enabled;
                movement.enabled = false;

                // Rigidbody があれば、動きを固定する（無ければ、操作を止めるだけ）
                Rigidbody body = movement.GetComponent<Rigidbody>();

                carLock.body = body;

                if (body != null)
                {
                    carLock.constraints = body.constraints;  // 固定する前の制限を、記録する

                    // 元の制限に、水平方向（X・Z）の移動と回転の固定を足す。
                    // 縦方向（Y）は固定しないので、着地は普通に行われる
                    body.constraints =
                        carLock.constraints |
                        RigidbodyConstraints.FreezePositionX |
                        RigidbodyConstraints.FreezePositionZ |
                        RigidbodyConstraints.FreezeRotation;

                    // 横方向の勢いと回転を止める（落下中の縦方向の速度は残す）。
                    // 物理で動かさない設定（Is Kinematic）のRigidbodyには速度を代入できない
                    if (!body.isKinematic)
                    {
                        Vector3 velocity = body.linearVelocity;

                        body.linearVelocity =
                            new Vector3(0f, velocity.y, 0f);

                        body.angularVelocity = Vector3.zero;
                    }
                }

                // ロックした印を付ける（UnlockCars は、印のある車だけを戻す）
                carLock.locked = true;
            }
        }

        /// <summary>
        /// 演出中に、他のスクリプト（CarManager.EnableControl など）が CarMovement を
        /// 有効にしても、GO! まで動けないように、無効へ戻す。
        /// 戻した車には印を付けて、GO! の瞬間に有効にする。
        /// </summary>
        private void EnforceCarLock()
        {
            for (int i = 0; i < m_Cars.Count; i++)
            {
                CarLock carLock = m_Cars[i];

                // ロック中の車のうち、有効にされてしまったものだけを対象にする
                if (!carLock.locked ||
                    carLock.movement == null ||
                    !carLock.movement.enabled)
                {
                    continue;
                }

                carLock.movement.enabled = false;
                carLock.enabledByOthers = true;

                // 警告は、1回の演出につき1回だけ出す（毎フレーム出さない）
                if (!m_WarnedLockBreak)
                {
                    m_WarnedLockBreak = true;

                    Debug.LogWarning(
                        "RaceStartSequence : 演出中に CarMovement が有効にされたため、" +
                        "無効に戻しました（GO! の瞬間に有効にします）");
                }
            }
        }

        /// <summary>
        /// 車の操作ロックを解除して、動けるようにする。
        /// 固定していた動きの制限を元に戻してから、CarMovement を有効にする。
        /// </summary>
        private void UnlockCars()
        {
            for (int i = 0; i < m_Cars.Count; i++)
            {
                CarLock carLock = m_Cars[i];

                if (!carLock.locked)
                {
                    continue;
                }

                // 解除済みにする（あとで Restore から呼ばれても、二重に戻さない）
                carLock.locked = false;

                // Rigidbody の動きの制限を、固定する前の状態に戻す
                if (carLock.body != null)
                {
                    carLock.body.constraints = carLock.constraints;
                }

                // CarMovement を有効にする。対象は、ロックする前に有効だった車と、
                // 演出中に他から有効にされた車（もともと無効だった車は、無効のまま）
                if (carLock.movement != null &&
                    (carLock.movementWasEnabled || carLock.enabledByOthers))
                {
                    carLock.movement.enabled = true;
                }

                // Rigidbody を起こして、物理の計算をすぐに再開させる
                if (carLock.body != null)
                {
                    carLock.body.WakeUp();
                }
            }
        }


        // =================================================================
        // ■ カメラ
        // =================================================================
        //
        // 演出の間だけ RaceCameraFollow を無効にして、このスクリプトが
        // カメラの位置・向きを決める。終わったら、追従カメラ自身の
        // SetTarget で定位置へ合わせてから有効に戻す
        // （追従カメラの内部の状態も一緒に整う）。
        //
        // =================================================================

        /// <summary>
        /// 演出で動かすカメラ（RaceCameraFollow が付いたカメラ）を見つけて、リストに入れる。
        /// 有効で、追従する車（m_Target）が決まっているものだけが対象。
        /// </summary>
        private void CollectRigs()
        {
            m_Rigs.Clear();

            // シーンにある RaceCameraFollow を、全部探す（無効なオブジェクトは除く）
            RaceCameraFollow[] found =
                FindObjectsByType<RaceCameraFollow>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                RaceCameraFollow follow = found[i];

                // 無効なものと、追従する車が決まっていないものは、対象にしない
                if (follow == null || !follow.enabled || follow.m_Target == null)
                {
                    continue;
                }

                // 同じオブジェクトの Camera を使う（Camera が無ければ、対象にしない）
                Camera camera = follow.GetComponent<Camera>();

                if (camera == null)
                {
                    continue;
                }

                CameraRig rig = new CameraRig();

                rig.follow = follow;
                rig.camera = camera;
                rig.baseFov = camera.fieldOfView;  // 元の画角（GO! で広げた後に戻す）

                m_Rigs.Add(rig);
            }

            // 対象のカメラが無い時は、警告だけ出して続ける
            // （カメラ演出なしで、信号とカウントダウンだけ行う）
            if (m_Rigs.Count == 0)
            {
                Debug.LogWarning(
                    "RaceStartSequence : 追従対象のある RaceCameraFollow が見つかりません。" +
                    "カメラ演出は行わず、信号とカウントダウンだけ行います");
            }
        }

        /// <summary>
        /// カメラ演出のために、追従カメラ（RaceCameraFollow）を無効にして、
        /// このスクリプトがカメラを動かせるようにする。
        /// カメラ演出をしない設定なら、何もしない（追従カメラのまま）。
        /// </summary>
        private void TakeOverCameras()
        {
            // いったん「引き継ぎ済み（このスクリプトが動かしているカメラは無い）」にしておく。
            // 動かすカメラが1台でもあれば、下で false に戻す
            m_CamerasHandedOff = true;

            // カメラ演出をしない設定なら、追従カメラのまま使う
            if (!m_PlayCameraIntro)
            {
                return;
            }

            for (int i = 0; i < m_Rigs.Count; i++)
            {
                CameraRig rig = m_Rigs[i];

                // 元の状態を記録してから、追従カメラを無効にする
                // （追従カメラが、毎フレームカメラの位置を上書きしないように）
                rig.followWasEnabled = rig.follow.enabled;
                rig.follow.enabled = false;
                rig.tookOver = true;

                m_CamerasHandedOff = false;
            }
        }

        /// <summary>
        /// カメラ演出のカメラの位置・向きを、今の時刻に合わせて決める。
        /// カメラ演出の最中だけ動く（追従カメラへ引き継いだ後は、何もしない）。
        /// </summary>
        private void ApplyIntroCameras()
        {
            if (m_CamerasHandedOff || m_Timeline == null)
            {
                return;
            }

            // u ＝ カメラ演出の進み具合（0〜1）。
            // カメラ演出の終わりの時刻に、1 になる
            float u =
                Mathf.Clamp01(
                    m_Clock /
                    Mathf.Max(0.0001f, m_Timeline.IntroEnd));

            // eased ＝ 動きのカーブ（m_IntroCurve）を通した、経路の進み具合（0〜1）
            float eased = EvaluateIntroCurve(u);

            for (int i = 0; i < m_Rigs.Count; i++)
            {
                CameraRig rig = m_Rigs[i];

                // このスクリプトが動かしているカメラだけを対象にする
                if (!rig.tookOver || rig.follow == null || rig.camera == null)
                {
                    continue;
                }

                Transform target = rig.follow.m_Target;

                if (target == null)
                {
                    continue;
                }

                Vector3 position;
                Quaternion rotation;

                // 車の位置・向きと、追従カメラの設定（Offset など）から、
                // カメラの位置と向きを求める
                StartSequenceMath.IntroPose(
                    target.position,
                    target.eulerAngles.y,
                    rig.follow.m_Offset,
                    rig.follow.m_LookAtHeight,
                    rig.follow.m_FollowTargetYaw,
                    m_IntroStartAngle,
                    m_IntroStartDistance,
                    m_IntroStartHeight,
                    m_IntroStartLookHeight,
                    eased,
                    out position,
                    out rotation);

                // 求めた位置と向きを、カメラに反映する
                rig.camera.transform.position = position;
                rig.camera.transform.rotation = rotation;
            }
        }

        /// <summary>
        /// カメラ演出の進み具合に、動きのカーブ（m_IntroCurve）を通す。
        /// カーブが設定されていない時は、なめらかに加減速する標準のカーブ（SmoothStep）を使う。
        /// </summary>
        /// <param name="u">進み具合（0〜1）</param>
        /// <returns>カーブを通した、経路の進み具合（0〜1）</returns>
        private float EvaluateIntroCurve(float u)
        {
            // 最後は、必ず 1 にする（終点の位置に、ぴったり合わせるため）
            if (u >= 1f)
            {
                return 1f;
            }

            // カーブが無い時は、SmoothStep（最初と最後がゆっくりな動き）を使う
            if (m_IntroCurve == null || m_IntroCurve.length == 0)
            {
                return Mathf.SmoothStep(0f, 1f, u);
            }

            return m_IntroCurve.Evaluate(u);
        }

        /// <summary>
        /// カメラを、追従カメラ（RaceCameraFollow）へ引き継ぐ。
        /// カメラ演出で動かしていたカメラを、追従カメラの定位置へ合わせてから、
        /// 追従カメラを元の状態へ戻す。
        /// </summary>
        private void HandOffCameras()
        {
            for (int i = 0; i < m_Rigs.Count; i++)
            {
                CameraRig rig = m_Rigs[i];

                // このスクリプトが動かしていないカメラは、何もしない
                if (!rig.tookOver)
                {
                    continue;
                }

                // 引き継ぎ済みにする（二重に引き継がない）
                rig.tookOver = false;

                if (rig.follow == null)
                {
                    continue;
                }

                // 追従カメラ自身の式（SetTarget）で、定位置へ合わせる。
                // これで、追従カメラの内部の状態も整い、引き継いだ時に画面が飛ばない
                if (rig.follow.m_Target != null)
                {
                    try
                    {
                        rig.follow.SetTarget(rig.follow.m_Target);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("RaceStartSequence : 追従カメラの位置合わせでエラーが出ました\n" + e);
                    }
                }

                // 追従カメラを、演出の前の状態（有効・無効）に戻す
                rig.follow.enabled = rig.followWasEnabled;
            }

            m_CamerasHandedOff = true;
        }

        /// <summary>
        /// GO! の瞬間に、カメラの画角を一瞬だけ広げて、元に戻す。
        /// 広げる量と時間は、m_GoFovKick と m_GoFovKickDuration で決まる。
        /// </summary>
        private void ApplyFovKick()
        {
            // GO! の前や、広げない設定（0）の時は、何もしない
            if (!m_Released || m_GoFovKick <= 0f || m_Timeline == null)
            {
                return;
            }

            // kick ＝ 画角の広がり具合（0〜1）。
            // GO! の瞬間に素早く広がって、なめらかに戻る（Pulse）
            float kick =
                StartSequenceMath.Pulse(
                    m_Clock - m_Timeline.GoAt,
                    m_GoFovKickDuration);

            for (int i = 0; i < m_Rigs.Count; i++)
            {
                CameraRig rig = m_Rigs[i];

                if (rig.camera == null)
                {
                    continue;
                }

                // 元の画角に、広げる量（度）× 広がり具合を足す
                rig.camera.fieldOfView = rig.baseFov + m_GoFovKick * kick;
                rig.fovChanged = true;  // 動かした印（RestoreFov が、元に戻す対象になる）
            }
        }

        /// <summary>
        /// 画角を動かしたカメラだけ、元の画角へ戻す。
        /// </summary>
        private void RestoreFov()
        {
            for (int i = 0; i < m_Rigs.Count; i++)
            {
                CameraRig rig = m_Rigs[i];

                if (!rig.fovChanged || rig.camera == null)
                {
                    continue;
                }

                rig.fovChanged = false;
                rig.camera.fieldOfView = rig.baseFov;
            }
        }


        // =================================================================
        // ■ HUD
        // =================================================================
        //
        // 周回・順位・ミニマップなどを、演出中は隠しておき、
        // 「3」が出るタイミングで現れるようにする。
        // 各HUDのGameObjectに CanvasGroup を付けて、透明度で表示を切り替える。
        // （既存のスクリプトには触らない）
        //
        // =================================================================

        /// <summary>
        /// 演出中に隠すHUDを見つけて、リストに入れる。
        /// 見つけ方は2通り（コンポーネントの名前で探す／Inspector で直接指定する）。
        /// 各HUDには、透明度を動かすための CanvasGroup を付ける（無ければ追加する）。
        /// </summary>
        private void CollectHud()
        {
            m_Huds.Clear();

            // HUDを隠さない設定なら、何もしない
            if (!m_FadeInHud)
            {
                return;
            }

            // targets ＝ 隠すHUDの候補（同じオブジェクトは、1つだけ入れる）
            List<GameObject> targets = new List<GameObject>();

            // コンポーネントの名前（m_HudComponentNames）で見つける。
            // シーンの全コンポーネントの型名を調べて、一致したものの GameObject を候補に入れる
            if (m_HudComponentNames != null && m_HudComponentNames.Length > 0)
            {
                MonoBehaviour[] all =
                    FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);

                for (int i = 0; i < all.Length; i++)
                {
                    MonoBehaviour behaviour = all[i];

                    if (behaviour == null)
                    {
                        continue;
                    }

                    string typeName = behaviour.GetType().Name;

                    for (int n = 0; n < m_HudComponentNames.Length; n++)
                    {
                        if (m_HudComponentNames[n] == typeName)
                        {
                            AddUnique(targets, behaviour.gameObject);
                            break;
                        }
                    }
                }
            }

            // Inspector で直接指定されたもの（m_ExtraHudObjects）
            if (m_ExtraHudObjects != null)
            {
                for (int i = 0; i < m_ExtraHudObjects.Length; i++)
                {
                    if (m_ExtraHudObjects[i] != null)
                    {
                        AddUnique(targets, m_ExtraHudObjects[i]);
                    }
                }
            }

            // 別のHUDの中に入っているものは、親と一緒に隠れるので対象から外す
            // （両方を透明にすると、二重にかかって暗くなってしまう）
            for (int i = 0; i < targets.Count; i++)
            {
                bool insideOther = false;

                for (int k = 0; k < targets.Count; k++)
                {
                    if (k != i &&
                        targets[i].transform.IsChildOf(targets[k].transform))
                    {
                        insideOther = true;
                        break;
                    }
                }

                if (insideOther)
                {
                    continue;
                }

                HudEntry entry = new HudEntry();

                entry.target = targets[i];

                // 透明度を動かすための CanvasGroup を取る（無ければ追加する）
                CanvasGroup group = targets[i].GetComponent<CanvasGroup>();

                if (group == null)
                {
                    group = targets[i].AddComponent<CanvasGroup>();
                }

                entry.group = group;
                entry.originalAlpha = group.alpha;  // 元の透明度（表示を戻す時の基準）

                m_Huds.Add(entry);
            }
        }

        /// <summary>
        /// リストに、まだ入っていないオブジェクトだけを追加する。
        /// </summary>
        /// <param name="list">追加先のリスト</param>
        /// <param name="item">追加するオブジェクト</param>
        private static void AddUnique(List<GameObject> list, GameObject item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// 全HUDの透明度を、元の透明度に対する割合で設定する。
        /// </summary>
        /// <param name="ratio">割合（0 ＝ 完全に隠す、1 ＝ 元の状態）</param>
        private void SetHudAlpha(float ratio)
        {
            for (int i = 0; i < m_Huds.Count; i++)
            {
                HudEntry entry = m_Huds[i];

                if (entry.group == null)
                {
                    continue;
                }

                entry.group.alpha = entry.originalAlpha * ratio;
            }
        }

        /// <summary>
        /// HUDの透明度を、今の時刻に合わせる。
        /// 「3」が出た時刻から、m_HudFadeDuration 秒かけて、少しずつ現れる。
        /// </summary>
        private void ApplyHud()
        {
            if (m_Huds.Count == 0 || m_Timeline == null)
            {
                return;
            }

            // ratio ＝ 現れ具合（0〜1）。「3」が出る前は 0（隠れている）で、
            // そのあと時間とともに 1（元の状態）まで増える
            float ratio =
                Mathf.Clamp01(
                    (m_Clock - m_Timeline.CountAt[0]) /
                    Mathf.Max(0.0001f, m_HudFadeDuration));

            SetHudAlpha(ratio);
        }

        /// <summary>
        /// HUDの透明度を、元の状態に戻す。
        /// </summary>
        private void RestoreHud()
        {
            SetHudAlpha(1f);
        }


        // =================================================================
        // ■ 音
        // =================================================================

        /// <summary>
        /// カウントダウンと GO! の音を用意する。
        /// 音を鳴らす AudioSource を（まだ無ければ）作る。
        /// 音は、Inspector で指定されたものを使い、指定が無ければ、コードで作った音を使う。
        /// </summary>
        private void BuildAudio()
        {
            // まず、音を空にする（音を鳴らさない設定なら、そのまま終わる）
            m_BeepClip = null;
            m_GoToneClip = null;

            if (!m_PlaySounds)
            {
                return;
            }

            // 音を鳴らす AudioSource を、まだ作っていなければ作る
            if (m_Audio == null)
            {
                m_AudioObject = new GameObject("RaceStartSequence Audio");
                m_AudioObject.transform.SetParent(transform, false);

                m_Audio = m_AudioObject.AddComponent<AudioSource>();
                m_Audio.playOnAwake = false;  // シーンの開始と同時には鳴らさない
                m_Audio.loop = false;         // くり返さない
                m_Audio.spatialBlend = 0f;    // 0 ＝ 2Dの音（位置に関係なく、同じ音量）
            }

            // 自分で用意した音があればそれを使い、無ければコードで作った音を使う
            if (m_CountdownClip != null)
            {
                m_BeepClip = m_CountdownClip;
            }
            else
            {
                // コードで作った音は、1回だけ作って、使い回す
                if (m_GeneratedBeep == null)
                {
                    m_GeneratedBeep =
                        StartSequenceAssets.MakeClip(
                            "RaceStart Beep",
                            StartSequenceAssets.SynthCountdownBeep());
                }

                m_BeepClip = m_GeneratedBeep;
            }

            if (m_GoClip != null)
            {
                m_GoToneClip = m_GoClip;
            }
            else
            {
                if (m_GeneratedGoTone == null)
                {
                    m_GeneratedGoTone =
                        StartSequenceAssets.MakeClip(
                            "RaceStart Go",
                            StartSequenceAssets.SynthGoTone());
                }

                m_GoToneClip = m_GeneratedGoTone;
            }
        }

        /// <summary>
        /// 音を1回鳴らす（音量は m_Volume）。
        /// 鳴らせない時（AudioSource や音が無い）は、何もしない。
        /// </summary>
        /// <param name="clip">鳴らす音</param>
        private void PlaySound(AudioClip clip)
        {
            if (m_Audio == null || clip == null)
            {
                return;
            }

            m_Audio.PlayOneShot(clip, m_Volume);
        }

        /// <summary>
        /// 音を鳴らすオブジェクト（AudioSource）を片付ける。
        /// </summary>
        private void DestroyAudio()
        {
            if (m_AudioObject != null)
            {
                Destroy(m_AudioObject);
            }

            m_AudioObject = null;
            m_Audio = null;
        }

        /// <summary>
        /// コードで作った音のデータ（AudioClip）を片付ける。
        /// （Inspector で指定された音は、片付けない）
        /// </summary>
        private void DestroyGeneratedClips()
        {
            if (m_GeneratedBeep != null)
            {
                Destroy(m_GeneratedBeep);
            }

            if (m_GeneratedGoTone != null)
            {
                Destroy(m_GeneratedGoTone);
            }

            m_GeneratedBeep = null;
            m_GeneratedGoTone = null;
        }


        // =================================================================
        // ■ 画面（暗転・信号・数字・GO!・スピード線）
        // =================================================================
        //
        // 専用のCanvas（Screen Space - Overlay）をコードで作る。
        // 既存のCanvasや、画面の設定には依存しない。
        //
        // 分割画面のときは、カメラの表示範囲（Camera.rect）ごとに
        // 信号と数字を1組ずつ出す。
        //
        // =================================================================

        /// <summary>
        /// 演出用の画面を作る。
        /// 専用の Canvas（Screen Space - Overlay）を作り、その中に、
        /// 区画ごとの表示物（スピード線・信号・数字・GO!）と、
        /// 一番手前の暗転（黒い画像）を置く。
        /// </summary>
        private void BuildUi()
        {
            DestroyUi();

            int lampCount = m_Timeline.CountAt.Length;

            // ---- スプライト（画像）----
            // 画像は、コードで作る（画像ファイルは要らない）。
            // 作るのは、丸・光彩・信号の本体（角丸四角）・スピード線の4つ
            // bodyWidth ＝ 信号の本体の幅（ランプの数に合わせて広がる）
            float bodyWidth = LampPitch * lampCount + BodyPadding;

            m_CircleSprite =
                StartSequenceAssets.CreateSprite(
                    "RaceStart Circle",
                    128,
                    128,
                    StartSequenceAssets.CircleAlpha(128));

            m_GlowSprite =
                StartSequenceAssets.CreateSprite(
                    "RaceStart Glow",
                    128,
                    128,
                    StartSequenceAssets.GlowAlpha(128));

            // 本体の画像は、整数のピクセルの大きさで作る（角の丸みの半径は 48px）
            int bodyW = Mathf.RoundToInt(bodyWidth);
            int bodyH = Mathf.RoundToInt(BodyHeight);

            m_BodySprite =
                StartSequenceAssets.CreateSprite(
                    "RaceStart Body",
                    bodyW,
                    bodyH,
                    StartSequenceAssets.RoundedRectAlpha(bodyW, bodyH, 48f));

            m_LinesSprite =
                StartSequenceAssets.CreateSprite(
                    "RaceStart SpeedLines",
                    512,
                    512,
                    StartSequenceAssets.SpeedLinesAlpha(512, 7));

            // ---- キャンバス ----
            m_UiObject =
                new GameObject(
                    "RaceStartSequence UI",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));

            m_UiObject.transform.SetParent(transform, false);

            Canvas canvas = m_UiObject.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;  // 画面全体に重ねて表示する
            canvas.sortingOrder = m_CanvasSortingOrder;         // 他の Canvas との前後の順番

            // 画面の大きさが変わっても、同じ見た目になるようにする。
            // 基準の解像度は 1920×1080。幅と高さの中間（0.5）で、拡大縮小の倍率を決める
            CanvasScaler scaler = m_UiObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = m_UiObject.GetComponent<RectTransform>();

            // フォントの用意ができているか確認する
            if (m_Font == null && !HasDefaultFont())
            {
                Debug.LogWarning(
                    "RaceStartSequence : TextMeshPro の標準フォントが見つかりません。" +
                    "数字が表示されない場合は、メニューの Window > TextMeshPro > " +
                    "Import TMP Essential Resources を実行するか、" +
                    "Inspector の Font にフォントを設定してください");
            }

            // ---- 区画ごとの表示物（カメラ1台につき1組。カメラが無ければ全画面で1組）----
            int panelCount = Mathf.Max(1, m_Rigs.Count);

            for (int i = 0; i < panelCount; i++)
            {
                // この区画のカメラ。カメラが無い時（カメラ演出なし）は null ＝ 全画面
                Camera camera = i < m_Rigs.Count ? m_Rigs[i].camera : null;

                m_Panels.Add(BuildPanel(canvasRect, camera, i, lampCount));
            }

            // ---- 暗転（一番手前）----
            // 暗転の黒い画像（画面いっぱい）。濃さを 1 → 0 へ動かして、明るくする（ApplyUi）
            m_FadeImage =
                CreateImage(
                    "Fade",
                    canvasRect,
                    null,
                    Color.black);

            Stretch(m_FadeImage.rectTransform);
        }

        /// <summary>
        /// TextMeshPro の標準フォントがあるか。
        /// TMP Settings 自体が無い時は、例外になるので、「無い」扱いにする。
        /// </summary>
        /// <returns>標準フォントがあれば true</returns>
        private static bool HasDefaultFont()
        {
            try
            {
                return TMP_Settings.defaultFontAsset != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 1つの区画ぶんの表示物を作る。
        /// 奥から、スピード線 → 信号（棒・縁取り・本体・ランプ）→ 数字 → GO! の順に重ねる。
        /// 画面が分割されている時は、カメラ1台につき1区画を作る。
        /// </summary>
        /// <param name="parent">親（演出用の Canvas）</param>
        /// <param name="camera">この区画のカメラ（null ＝ 全画面）</param>
        /// <param name="index">区画の番号（0から。オブジェクトの名前に使う）</param>
        /// <param name="lampCount">ランプの数（カウントダウンの数字の数と同じ）</param>
        /// <returns>作った区画</returns>
        private Panel BuildPanel(
            RectTransform parent,
            Camera camera,
            int index,
            int lampCount)
        {
            Panel panel = new Panel();

            panel.camera = camera;

            // 区画の親。カメラの表示範囲に合わせて、位置と大きさを変える（UpdatePanelLayout）。
            // 分割画面でなければ、画面いっぱいのまま
            panel.root = CreateRect("Viewport " + (index + 1), parent);

            Stretch(panel.root);

            // ---- スピード線（一番奥）----
            // 最初は透明にしておく（GO! の時に、濃さを動かして出す）
            panel.burst =
                CreateImage(
                    "SpeedLines",
                    panel.root,
                    m_LinesSprite,
                    new Color(
                        SpeedLineColor.r,
                        SpeedLineColor.g,
                        SpeedLineColor.b,
                        0f));

            panel.burstRect = panel.burst.rectTransform;

            Stretch(panel.burstRect);

            // ---- 信号 ----
            float bodyWidth = LampPitch * lampCount + BodyPadding;

            // 信号は、画面の上の端（横位置は m_SignalPosition）から、ぶら下げる。
            // 最初は、画面の外（上）に隠しておく（SignalHiddenY）
            panel.signalRect = CreateRect("Signal", panel.root);

            panel.signalRect.anchorMin = new Vector2(m_SignalPosition, 1f);
            panel.signalRect.anchorMax = new Vector2(m_SignalPosition, 1f);
            panel.signalRect.pivot = new Vector2(0.5f, 1f);
            panel.signalRect.sizeDelta = new Vector2(bodyWidth, BodyHeight);
            panel.signalRect.anchoredPosition = new Vector2(0f, SignalHiddenY);

            // 吊り下げている棒
            Image pole =
                CreateImage(
                    "Pole",
                    panel.signalRect,
                    null,
                    PoleColor);

            // 棒は、本体の上の端の中央から、上へ伸ばす
            // （棒の下の端を、本体の上の端に合わせる）
            pole.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            pole.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            pole.rectTransform.pivot = new Vector2(0.5f, 0f);
            pole.rectTransform.sizeDelta = new Vector2(PoleWidth, PoleHeight);
            pole.rectTransform.anchoredPosition = Vector2.zero;

            // 縁取り → 本体
            Image rim =
                CreateImage(
                    "Rim",
                    panel.signalRect,
                    m_BodySprite,
                    RimColor);

            Stretch(rim.rectTransform);

            // 縁取りは、本体より6pxずつ大きくする
            rim.rectTransform.offsetMin = new Vector2(-6f, -6f);
            rim.rectTransform.offsetMax = new Vector2(6f, 6f);

            Image body =
                CreateImage(
                    "Body",
                    panel.signalRect,
                    m_BodySprite,
                    BodyColor);

            Stretch(body.rectTransform);

            // ランプ（数字の数だけ、横に並べる）
            panel.lights = new Image[lampCount];
            panel.glows = new Image[lampCount];

            for (int i = 0; i < lampCount; i++)
            {
                // x ＝ i番目のランプの横位置（本体の中央が 0。ランプ全体が、中央に並ぶ）
                float x = (i - (lampCount - 1) * 0.5f) * LampPitch;

                RectTransform lamp = CreateRect("Lamp " + (i + 1), panel.signalRect);

                lamp.anchorMin = new Vector2(0.5f, 0.5f);
                lamp.anchorMax = new Vector2(0.5f, 0.5f);
                lamp.pivot = new Vector2(0.5f, 0.5f);
                lamp.sizeDelta = new Vector2(BezelSize, BezelSize);
                lamp.anchoredPosition = new Vector2(x, 0f);

                // 奥から：ランプの縁 → 光彩 → ランプ
                Image bezel =
                    CreateImage("Bezel", lamp, m_CircleSprite, BezelColor);

                CenterSize(bezel.rectTransform, BezelSize);

                // 光彩は、最初は透明（点灯した時に、濃さを付ける）
                Image glow =
                    CreateImage(
                        "Glow",
                        lamp,
                        m_GlowSprite,
                        new Color(LampRed.r, LampRed.g, LampRed.b, 0f));

                CenterSize(glow.rectTransform, GlowSize);

                Image light =
                    CreateImage("Light", lamp, m_CircleSprite, LampOff);

                CenterSize(light.rectTransform, LampSize);

                panel.glows[i] = glow;
                panel.lights[i] = light;
            }

            // ---- 数字 ----
            // 数字の文字（最初は空）。濃さは CanvasGroup で動かす
            panel.number =
                CreateText(
                    "Number",
                    panel.root,
                    "",
                    m_NumberSize,
                    m_NumberColor,
                    out panel.numberGroup);

            panel.numberRect = panel.number.rectTransform;

            // ---- GO! ----
            // 「GO!」の文字（数字の1.3倍の大きさ）
            TextMeshProUGUI go =
                CreateText(
                    "Go",
                    panel.root,
                    "GO!",
                    m_NumberSize * 1.3f,
                    m_GoColor,
                    out panel.goGroup);

            panel.goRect = go.rectTransform;

            return panel;
        }

        /// <summary>
        /// 区画の位置・大きさを、カメラの表示範囲（Camera.rect）に合わせる。
        /// これで、分割画面でも、各カメラの画面の中に、信号と数字が出る。
        /// 表示範囲が前回と同じなら、何もしない。
        /// </summary>
        /// <param name="panel">合わせる区画</param>
        private void UpdatePanelLayout(Panel panel)
        {
            // カメラが無い区画は、全画面のまま（合わせる必要がない）
            if (panel.camera == null)
            {
                return;
            }

            // rect ＝ カメラの表示範囲（画面全体を 0〜1 とした割合）
            Rect rect = panel.camera.rect;

            // 前回と同じ表示範囲なら、やり直さない（毎フレーム呼ばれるため）
            if (panel.hasRect &&
                panel.lastRect.x == rect.x &&
                panel.lastRect.y == rect.y &&
                panel.lastRect.width == rect.width &&
                panel.lastRect.height == rect.height)
            {
                return;
            }

            panel.hasRect = true;
            panel.lastRect = rect;

            // 区画を、表示範囲にぴったり合わせる（左下と右上を、アンカーにして、余白は 0）
            panel.root.anchorMin = new Vector2(rect.x, rect.y);
            panel.root.anchorMax = new Vector2(rect.x + rect.width, rect.y + rect.height);
            panel.root.offsetMin = Vector2.zero;
            panel.root.offsetMax = Vector2.zero;

            // 区画が小さい（分割画面）ときは、信号と文字も小さくする
            panel.scale = Mathf.Clamp(rect.height, 0.45f, 1f);
        }

        /// <summary>
        /// 今の時刻に合わせて、画面の表示物
        /// （暗転・ランプ・数字・GO!・スピード線・信号の位置）を更新する。毎フレーム呼ばれる。
        /// </summary>
        private void ApplyUi()
        {
            if (m_UiObject == null || m_Timeline == null)
            {
                return;
            }

            StartSequenceTimeline t = m_Timeline;

            float clock = m_Clock;

            int lampCount = t.CountAt.Length;

            // ---- 暗転 ----
            if (m_FadeImage != null)
            {
                // fade ＝ 黒い画像の濃さ。1（真っ暗）から、m_FadeInDuration 秒かけて 0 へ。
                // 0秒の設定なら、暗転なし
                float fade =
                    m_FadeInDuration > 0f
                        ? 1f - Mathf.Clamp01(clock / m_FadeInDuration)
                        : 0f;

                SetAlpha(m_FadeImage, fade);

                m_FadeImage.enabled = fade > 0.001f;  // ほぼ透明になったら、描画をやめる
            }

            // ---- 点いているランプの数・青かどうか ----
            int lit = 0;  // lit ＝ 点いている赤ランプの数

            for (int i = 0; i < lampCount; i++)
            {
                if (clock >= t.CountAt[i])
                {
                    lit = i + 1;
                }
            }

            bool green = clock >= t.GoAt;  // GO! 以降は、全部のランプが青（緑）

            // ---- 今出ている数字 ----
            int number = 0;         // 今出ている数字（0 ＝ 出ていない）
            float numberTime = 0f;  // その数字が出てからの秒数

            // CountAt[i] から CountInterval 秒の間が、i番目の数字を出す時間。
            // 数字は、lampCount（最初）から、1 ずつ減っていく（3 → 2 → 1）
            for (int i = 0; i < lampCount; i++)
            {
                if (clock >= t.CountAt[i] &&
                    clock < t.CountAt[i] + t.CountInterval)
                {
                    number = lampCount - i;
                    numberTime = clock - t.CountAt[i];
                    break;
                }
            }

            float numberAlpha;
            float numberScale;

            // 数字の透明度と大きさを求める
            StartSequenceMath.Numeral(
                numberTime,
                t.CountInterval,
                out numberAlpha,
                out numberScale);

            // 数字が出ていない時は、見えなくする
            if (number == 0)
            {
                numberAlpha = 0f;
            }

            // ---- GO! ----
            float goAlpha;
            float goScale;

            // GO! が出てからの秒数（GO! の前はマイナス ＝ 見えない）
            StartSequenceMath.Go(clock - t.GoAt, out goAlpha, out goScale);

            // ---- スピード線 ----
            float burstTime = clock - t.GoAt;  // GO! からの秒数

            // burstAlpha ＝ スピード線の濃さ（設定がOFFなら 0）。
            // GO! の瞬間に立ち上がって、消える（Pulse）
            float burstAlpha =
                m_GoSpeedLines
                    ? StartSequenceMath.Pulse(burstTime, BurstDuration) * BurstMaxAlpha
                    : 0f;

            // burstScale ＝ スピード線の大きさ。GO! からの時間とともに、
            // 1 から（1 ＋ BurstGrow）へ広がる
            float burstScale =
                1f + BurstGrow * Mathf.Clamp01(burstTime / BurstDuration);

            // ---- 信号の位置 ----
            // presence ＝ 信号の位置の割合（0 ＝ 隠れている、1 ＝ 定位置）。
            // 信号を表示しない設定なら 0
            float presence =
                m_ShowSignal
                    ? StartSequenceMath.SignalPresence(clock, t.SignalIn, t.SignalOut)
                    : 0f;

            // signalY ＝ 信号の縦の位置（隠れている位置と、定位置の間）。
            // 割合が 1 を超えた時は、定位置より少し下へ、行き過ぎる
            float signalY =
                Mathf.LerpUnclamped(SignalHiddenY, SignalShownY, presence);

            bool signalVisible = presence > 0.001f;  // 隠れている間は、非表示にする

            // ---- 区画ごとに反映 ----
            for (int p = 0; p < m_Panels.Count; p++)
            {
                Panel panel = m_Panels[p];

                // 区画の位置・大きさを、カメラの表示範囲に合わせる（範囲が変わった時だけ）
                UpdatePanelLayout(panel);

                // スピード線
                SetAlpha(panel.burst, burstAlpha);

                panel.burst.enabled = burstAlpha > 0.001f;  // 見えない間は、描画しない
                panel.burstRect.localScale = new Vector3(burstScale, burstScale, 1f);

                // 信号
                // 表示・非表示が変わった時だけ、切り替える
                if (panel.signalRect.gameObject.activeSelf != signalVisible)
                {
                    panel.signalRect.gameObject.SetActive(signalVisible);
                }

                // 縦の位置を動かす。大きさは、区画の倍率に合わせる（分割画面では小さくする）
                panel.signalRect.anchoredPosition = new Vector2(0f, signalY);
                panel.signalRect.localScale = new Vector3(panel.scale, panel.scale, 1f);

                // ランプの色を決める：
                //   GO! 以降 ＝ 全部、青（緑）
                //   それより前 ＝ 点いている数（lit）の分だけ赤、残りは消えた色
                // 光彩（glow）は、ランプと同じ色。光っている時だけ濃さを付ける（消えている時は 0）
                for (int i = 0; i < panel.lights.Length; i++)
                {
                    Color lamp = LampOff;
                    float glow = 0f;

                    if (green)
                    {
                        lamp = LampGreen;
                        glow = 0.55f;
                    }
                    else if (i < lit)
                    {
                        lamp = LampRed;
                        glow = 0.5f;
                    }

                    panel.lights[i].color = lamp;

                    panel.glows[i].color = new Color(lamp.r, lamp.g, lamp.b, glow);
                }

                // 数字
                // 数字が変わった時だけ、文字を書き換える（毎フレームは、無駄な処理になる）
                if (number > 0 && panel.shownNumber != number)
                {
                    panel.shownNumber = number;
                    panel.number.text = number.ToString();
                }

                // 透明度と大きさを反映する（大きさ ＝ 区画の倍率 × 数字の動き）
                panel.numberGroup.alpha = numberAlpha;

                float s = panel.scale * numberScale;

                panel.numberRect.localScale = new Vector3(s, s, 1f);

                // GO!
                panel.goGroup.alpha = goAlpha;

                float g = panel.scale * goScale;

                panel.goRect.localScale = new Vector3(g, g, 1f);
            }
        }


        // =================================================================
        // ■ 画面を作るための小さな部品
        // =================================================================

        /// <summary>
        /// RectTransform だけを持つ、空のオブジェクトを作る。
        /// </summary>
        /// <param name="name">オブジェクトの名前</param>
        /// <param name="parent">親</param>
        /// <returns>作った RectTransform</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));

            RectTransform rect = gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);  // false ＝ 親から見た位置のまま付ける

            return rect;
        }

        /// <summary>
        /// Image（画像）のオブジェクトを作る。
        /// 操作の邪魔にならないよう、クリックなどの判定は付けない。
        /// </summary>
        /// <param name="name">オブジェクトの名前</param>
        /// <param name="parent">親</param>
        /// <param name="sprite">表示する画像（null ＝ 色だけの四角）</param>
        /// <param name="color">色（透明度も含む）</param>
        /// <returns>作った Image</returns>
        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color)
        {
            GameObject gameObject =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image));

            gameObject.GetComponent<RectTransform>().SetParent(parent, false);

            Image image = gameObject.GetComponent<Image>();

            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;  // クリックなどの判定を受けない

            return image;
        }

        /// <summary>
        /// 文字（TextMeshPro）のオブジェクトを作る。
        /// 区画の中央から少し上（m_NumberHeight）に、決まった傾きで置く。
        /// 濃さは CanvasGroup で 0〜1 に動かす（最初は 0 ＝ 見えない）。
        /// </summary>
        /// <param name="name">オブジェクトの名前</param>
        /// <param name="parent">親</param>
        /// <param name="text">表示する文字</param>
        /// <param name="size">文字の大きさ</param>
        /// <param name="color">文字の色</param>
        /// <param name="group">（出力）濃さを動かす CanvasGroup</param>
        /// <returns>作った文字</returns>
        private TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            string text,
            float size,
            Color color,
            out CanvasGroup group)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));

            RectTransform rect = gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);

            // 区画の中央から、指定の高さに置く
            Vector2 anchor = new Vector2(0.5f, 0.5f + m_NumberHeight);

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2400f, 1000f);  // 文字が収まる、十分に大きい枠
            // 文字を傾ける（Z軸まわりの回転。プラスで左に傾く）
            rect.localRotation = Quaternion.Euler(0f, 0f, m_NumberTilt);

            // 濃さ（0〜1）は CanvasGroup で動かす（最初は 0 ＝ 見えない）。
            // 操作は受けない設定にして、クリックの邪魔をしない
            group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // 文字の見た目を決める（太字・斜体・中央ぞろえ・折り返さない・縁取り付き）
            TextMeshProUGUI label = gameObject.AddComponent<TextMeshProUGUI>();

            // フォントが指定されていれば、それを使う（無ければ、TextMeshPro の標準フォント）
            if (m_Font != null)
            {
                label.font = m_Font;
            }

            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold | FontStyles.Italic;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.outlineWidth = m_OutlineWidth;
            label.outlineColor = m_OutlineColor;

            return label;
        }

        /// <summary>
        /// 親いっぱいに広げる（上下左右の余白を 0 にする）。
        /// </summary>
        /// <param name="rect">広げる RectTransform</param>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 親の中央に、指定の大きさ（正方形）で置く。
        /// </summary>
        /// <param name="rect">置く RectTransform</param>
        /// <param name="size">一辺の長さ</param>
        private static void CenterSize(RectTransform rect, float size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 画像の透明度だけを変える（色は、そのまま）。
        /// </summary>
        /// <param name="image">変える画像</param>
        /// <param name="alpha">透明度（0 ＝ 透明、1 ＝ 不透明）</param>
        private static void SetAlpha(Image image, float alpha)
        {
            Color color = image.color;

            color.a = alpha;

            image.color = color;
        }

        /// <summary>
        /// 演出用の画面（Canvas）と、コードで作ったスプライトを片付ける。
        /// </summary>
        private void DestroyUi()
        {
            if (m_UiObject != null)
            {
                Destroy(m_UiObject);
            }

            m_UiObject = null;
            m_FadeImage = null;

            m_Panels.Clear();

            DestroySprite(ref m_CircleSprite);
            DestroySprite(ref m_GlowSprite);
            DestroySprite(ref m_BodySprite);
            DestroySprite(ref m_LinesSprite);
        }

        /// <summary>
        /// スプライトと、その元の Texture を片付けて、null にする。
        /// （コードで作った画像は、自動では片付けられないので、ここで消す）
        /// </summary>
        /// <param name="sprite">片付けるスプライト（null になる）</param>
        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite != null)
            {
                if (sprite.texture != null)
                {
                    Destroy(sprite.texture);
                }

                Destroy(sprite);
            }

            sprite = null;
        }
    }


// ここから下は、Unityエディタの中だけで使う（ビルドした製品には含まれない）
#if UNITY_EDITOR

    // =====================================================================
    // ■ Hierarchy の右クリックメニュー
    // =====================================================================
    //
    // Race → Start Sequence で、レースのシーンに演出用のGameObjectを1つ置く。
    //
    // =====================================================================

    /// <summary>
    /// Hierarchy の右クリックメニューから、演出用のオブジェクトを置くための、
    /// エディタ専用のクラス。
    /// </summary>
    public static class RaceStartSequenceMenu
    {
        /// <summary>
        /// メニュー「GameObject &gt; Race &gt; Start Sequence」の処理。
        /// シーンに、演出用のオブジェクトを1つ作って選択する。
        /// すでにあれば、新しくは作らず、それを選択する。
        /// </summary>
        /// <param name="command">Unity が渡す呼び出し情報（この処理では使わない）</param>
        [MenuItem("GameObject/Race/Start Sequence", false, 10)]
        private static void Create(MenuCommand command)
        {
            // すでにシーンにあれば、新しく作らず、それを選択する
            RaceStartSequence existing =
                Object.FindFirstObjectByType<RaceStartSequence>();

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;

                EditorUtility.DisplayDialog(
                    "Race Start Sequence",
                    "このシーンには、すでに Race Start Sequence があります。\n" +
                    "新しくは作らず、今あるものを選択しました。",
                    "OK");

                return;
            }

            GameObject gameObject = new GameObject("RaceStartSequence");

            // 元に戻す（Ctrl+Z）で、作ったオブジェクトを消せるようにする
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "Create Race Start Sequence");

            // コンポーネントを付ける（これも、元に戻せる）
            Undo.AddComponent<RaceStartSequence>(gameObject);

            Selection.activeGameObject = gameObject;
        }
    }

#endif
}