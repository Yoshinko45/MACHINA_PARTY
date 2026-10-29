using System;
using System.Collections.Generic;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レースのルール（順位の計算・終わり方の判定）
    // =====================================================================
    //
    // このファイルは Unity の機能を使わない、ふつうの C# だけで書いてある。
    // （車やUIには触らず、「誰が何位か」「いつレースが終わるか」だけを決める。
    // 　そのため、Unityを動かさなくても、テストで正しさを確かめられる）
    //
    //   LapProgress    … 1台ぶんの、周回とチェックポイントの数え方
    //   RacerProgress  … 1人ぶんの進み具合
    //   RaceRanking    … 進み具合から、順位を決める
    //   RaceFlow       … ゴールした人の記録と、レースの終了判定
    //   RaceTimeFormat … タイムを「1:23.456」の形の文字にする
    //   RaceOrdinal    … 順位に付ける「st / nd / rd / th」
    //
    // =====================================================================


    /// <summary>
    /// 1台ぶんの、周回とチェックポイントの数え方（LapCounter の中身）。
    ///
    /// 数え方のルール
    ///   ・チェックポイントは、1番から順番に通った時だけ数える（飛ばしたり、逆走したりしても数えない）
    ///   ・ゴールは、全部のチェックポイントを通った後の1回だけ数える。
    ///     スタート直後や、同じゴールを2回続けて通っても、周回は増えない
    ///   ・規定の周回数ぶんゴールを通ったら「ゴール完了」。その後の通過は全部無視する
    ///
    /// 「進んだ区間の数（Units）」は、順位を決めるための値。
    /// 1周 ＝ チェックポイント全部 ＋ ゴール1回 ＝ （チェックポイント数 ＋ 1）区間。
    /// </summary>
    public sealed class LapProgress
    {
        /// <summary>通過を調べた結果</summary>
        public enum GateResult
        {
            Ignored,       // 数えなかった（順番が違う・二重・ゴール完了後など）
            CheckPoint,    // チェックポイントを1つ数えた
            LapCompleted,  // 1周を終えた（まだ次の周がある）
            Finished       // 規定の周回を終えた（ゴール完了）
        }

        private int m_MaxLaps = 3;
        private int m_TotalCheckPoints = 5;
        private int m_LapsCompleted;
        private int m_CheckPointsThisLap;

        /// <summary>規定の周回数（1以上）</summary>
        public int MaxLaps { get { return m_MaxLaps; } }

        /// <summary>チェックポイントの数（0以上）</summary>
        public int TotalCheckPoints { get { return m_TotalCheckPoints; } }

        /// <summary>終えた周の数</summary>
        public int LapsCompleted { get { return m_LapsCompleted; } }

        /// <summary>今の周で、通ったチェックポイントの数</summary>
        public int CheckPointsThisLap { get { return m_CheckPointsThisLap; } }

        /// <summary>規定の周回を終えたか</summary>
        public bool IsFinished { get { return m_LapsCompleted >= m_MaxLaps; } }

        /// <summary>今走っている周（1から。ゴール完了後は最終周のまま）</summary>
        public int CurrentLap
        {
            get { return Math.Min(m_LapsCompleted + 1, m_MaxLaps); }
        }

        /// <summary>進んだ区間の数。順位を決めるのに使う（大きいほど先にいる）</summary>
        public int Units
        {
            get
            {
                return m_LapsCompleted * (m_TotalCheckPoints + 1) +
                       m_CheckPointsThisLap;
            }
        }

        /// <summary>
        /// 次に通るべきチェックポイントの番号（1から）。
        /// 全部通り終えて、次はゴールの時は 0。ゴール完了後も 0。
        /// </summary>
        public int NextCheckPoint
        {
            get
            {
                if (IsFinished || m_CheckPointsThisLap >= m_TotalCheckPoints)
                {
                    return 0;
                }

                return m_CheckPointsThisLap + 1;
            }
        }

        /// <summary>
        /// 周回数とチェックポイントの数を決める。
        /// 走っている途中で数が変わっても、数え直しはせず、はみ出した分だけ範囲に収める。
        /// </summary>
        /// <param name="maxLaps">規定の周回数（1未満は1にする）</param>
        /// <param name="totalCheckPoints">チェックポイントの数（0未満は0にする）</param>
        public void Configure(int maxLaps, int totalCheckPoints)
        {
            m_MaxLaps = Math.Max(1, maxLaps);
            m_TotalCheckPoints = Math.Max(0, totalCheckPoints);

            m_LapsCompleted = Math.Min(m_LapsCompleted, m_MaxLaps);
            m_CheckPointsThisLap = Math.Min(m_CheckPointsThisLap, m_TotalCheckPoints);
        }

        /// <summary>
        /// チェックポイントを通った。
        /// 次に通るべき番号と同じ時だけ数える。
        /// </summary>
        /// <param name="checkPointNumber">通ったチェックポイントの番号（1から）</param>
        public GateResult PassCheckPoint(int checkPointNumber)
        {
            if (IsFinished || checkPointNumber != NextCheckPoint || checkPointNumber <= 0)
            {
                return GateResult.Ignored;
            }

            m_CheckPointsThisLap++;

            return GateResult.CheckPoint;
        }

        /// <summary>
        /// ゴールを通った。
        /// 今の周のチェックポイントを全部通っている時だけ、1周として数える。
        /// </summary>
        public GateResult PassGoal()
        {
            if (IsFinished || m_CheckPointsThisLap < m_TotalCheckPoints)
            {
                return GateResult.Ignored;
            }

            m_LapsCompleted++;
            m_CheckPointsThisLap = 0;

            return IsFinished ? GateResult.Finished : GateResult.LapCompleted;
        }

        /// <summary>最初の状態（0周・チェックポイント未通過）に戻す</summary>
        public void Reset()
        {
            m_LapsCompleted = 0;
            m_CheckPointsThisLap = 0;
        }
    }


    /// <summary>
    /// 1人ぶんの進み具合。順位を決めるために使う。
    /// </summary>
    public struct RacerProgress
    {
        /// <summary>ゴール済み（規定の周回を走り切った）か</summary>
        public bool Finished;

        /// <summary>ゴールした順番（1から）。Finished が true の時だけ意味がある</summary>
        public int FinishOrder;

        /// <summary>
        /// 通り過ぎた区間の数。チェックポイントとゴールを通るたびに1つ増える。
        /// 大きいほど先に進んでいる。
        /// </summary>
        public int Units;

        /// <summary>
        /// 次の目標（チェックポイントかゴール）までの距離（メートル）。
        /// Units が同じ時に、小さい方が先にいるとみなす。
        /// </summary>
        public float DistanceToNext;
    }


    /// <summary>
    /// 進み具合から、順位を決める。
    /// </summary>
    public static class RaceRanking
    {
        /// <summary>
        /// 全員の順位を決める。
        ///
        /// 順位のつけ方
        ///   ① ゴール済みの人が上。ゴールした順番（FinishOrder）が早いほど上
        ///   ② ゴールしていない人は、Units が大きいほど上
        ///   ③ Units が同じなら、次の目標までの距離（DistanceToNext）が短いほど上
        ///   ④ それでも同じなら、配列の番号が小さい人が上（毎回同じ結果にするため）
        /// </summary>
        /// <param name="racers">全員の進み具合</param>
        /// <returns>
        /// 全員の順位（1から）。配列の並びは、引数 racers と同じ。
        /// 引数が null や空なら、空の配列
        /// </returns>
        public static int[] Rank(RacerProgress[] racers)
        {
            if (racers == null || racers.Length == 0)
            {
                return new int[0];
            }

            int count = racers.Length;

            // order ＝ 順位の高い順に並べた、配列の番号
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            // 人数は最大4人なので、単純な挿入ソートで十分（並びが安定するのも利点）
            for (int i = 1; i < count; i++)
            {
                int current = order[i];
                int j = i - 1;

                while (j >= 0 && IsAhead(racers, current, order[j]))
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = current;
            }

            int[] ranks = new int[count];

            for (int position = 0; position < count; position++)
            {
                ranks[order[position]] = position + 1;
            }

            return ranks;
        }

        /// <summary>
        /// a が b より上位か（先にいるか）を調べる。同じ扱いの時は false。
        /// </summary>
        private static bool IsAhead(RacerProgress[] racers, int a, int b)
        {
            RacerProgress x = racers[a];
            RacerProgress y = racers[b];

            // ゴール済みは、していない人より上
            if (x.Finished != y.Finished)
            {
                return x.Finished;
            }

            // どちらもゴール済み：早くゴールした方が上
            if (x.Finished)
            {
                return x.FinishOrder < y.FinishOrder;
            }

            // どちらもゴールしていない：進んだ区間が多い方が上
            if (x.Units != y.Units)
            {
                return x.Units > y.Units;
            }

            // 区間が同じ：次の目標に近い方が上（NaN は一番遠い扱い）
            float dx = SafeDistance(x.DistanceToNext);
            float dy = SafeDistance(y.DistanceToNext);

            return dx < dy;
        }

        private static float SafeDistance(float distance)
        {
            if (float.IsNaN(distance))
            {
                return float.MaxValue;
            }

            return distance;
        }
    }


    /// <summary>
    /// 1人がゴールした時に返す、その結果の説明。
    /// </summary>
    public struct FinishInfo
    {
        /// <summary>記録された（二重のゴールや、範囲外の番号の時は false）</summary>
        public bool Accepted;

        /// <summary>何番目にゴールしたか（1から）</summary>
        public int Order;

        /// <summary>
        /// この時点で、最下位が確定した（残りが1人だけになった）か。
        /// 2人以上で走っている時だけ true になる。
        /// </summary>
        public bool LastPlaceConfirmed;

        /// <summary>最下位が確定した人の番号（確定していなければ -1）</summary>
        public int LastPlaceIndex;

        /// <summary>この時点で、レースの終わりが決まった（順位が全部決まった）か</summary>
        public bool RaceDecided;
    }


    /// <summary>
    /// ゴールした人の記録と、レースをいつ終わらせるかの判定。
    ///
    /// 終わり方
    ///   ・「順位が決まったら終わる」設定（標準）：
    ///       人数 − 1 人がゴールした時点で、残りの1人は最下位に決まるので、レースを終える。
    ///       （4人なら3人がゴールした時／2人なら1人がゴールした時／1人なら、そのゴールで終わり）
    ///   ・その設定がオフの時：全員がゴールするまで続ける
    ///   ・どちらの場合も、「最初のゴールから○秒たったら終わる」設定を足せる（0 ＝ 使わない）
    /// </summary>
    public sealed class RaceFlow
    {
        /// <summary>レースが終わった理由</summary>
        public enum EndReason
        {
            None,         // まだ終わっていない
            AllFinished,  // 全員がゴールした
            RankDecided,  // 順位が全部決まった（残りは最下位）
            Timeout       // 最初のゴールから、決めた時間がたった
        }

        private readonly int m_PlayerCount;
        private readonly bool m_EndWhenRankDecided;
        private readonly float m_TimeoutAfterFirstFinish;

        private readonly bool[] m_Finished;
        private readonly int[] m_FinishOrder;
        private readonly float[] m_FinishTime;

        private int m_FinishedCount;
        private float m_FirstFinishTime = -1f;
        private int m_LastPlaceIndex = -1;
        private EndReason m_Reason = EndReason.None;

        /// <param name="playerCount">走る人数（1〜4。範囲外は丸める）</param>
        /// <param name="endWhenRankDecided">人数−1人がゴールしたらレースを終えるか</param>
        /// <param name="timeoutAfterFirstFinish">
        /// 最初のゴールから、この秒数がたったら終える（0以下 ＝ 使わない）
        /// </param>
        public RaceFlow(
            int playerCount,
            bool endWhenRankDecided,
            float timeoutAfterFirstFinish)
        {
            m_PlayerCount = Math.Max(1, Math.Min(4, playerCount));
            m_EndWhenRankDecided = endWhenRankDecided;
            m_TimeoutAfterFirstFinish = timeoutAfterFirstFinish;

            m_Finished = new bool[m_PlayerCount];
            m_FinishOrder = new int[m_PlayerCount];
            m_FinishTime = new float[m_PlayerCount];
        }

        /// <summary>走る人数</summary>
        public int PlayerCount { get { return m_PlayerCount; } }

        /// <summary>ゴールした人数</summary>
        public int FinishedCount { get { return m_FinishedCount; } }

        /// <summary>レースの終わりが決まったか</summary>
        public bool IsDecided { get { return m_Reason != EndReason.None; } }

        /// <summary>レースが終わった理由（終わっていなければ None）</summary>
        public EndReason Reason { get { return m_Reason; } }

        /// <summary>最下位が確定した人の番号（確定していなければ -1）</summary>
        public int LastPlaceIndex { get { return m_LastPlaceIndex; } }

        /// <summary>この人がゴールしたか</summary>
        public bool HasFinished(int index)
        {
            return IsValid(index) && m_Finished[index];
        }

        /// <summary>この人のゴール順（ゴールしていなければ 0）</summary>
        public int FinishOrder(int index)
        {
            return HasFinished(index) ? m_FinishOrder[index] : 0;
        }

        /// <summary>この人のゴールタイム（秒）。ゴールしていなければ 0</summary>
        public float FinishTime(int index)
        {
            return HasFinished(index) ? m_FinishTime[index] : 0f;
        }

        /// <summary>
        /// 1人がゴールしたことを記録する。
        /// すでにゴール済みの人や、範囲外の番号は、何も起こさない（Accepted が false）。
        /// レースが終わった後のゴールも記録するが、終わりの理由は変わらない。
        /// </summary>
        /// <param name="index">ゴールした人の番号（0から）</param>
        /// <param name="raceTime">スタートからの経過秒</param>
        public FinishInfo RegisterFinish(int index, float raceTime)
        {
            FinishInfo info = new FinishInfo();

            info.LastPlaceIndex = -1;

            if (!IsValid(index) || m_Finished[index])
            {
                info.Order = HasFinished(index) ? m_FinishOrder[index] : 0;
                info.RaceDecided = IsDecided;

                return info;
            }

            m_FinishedCount++;

            m_Finished[index] = true;
            m_FinishOrder[index] = m_FinishedCount;
            m_FinishTime[index] = raceTime;

            if (m_FinishedCount == 1)
            {
                m_FirstFinishTime = raceTime;
            }

            info.Accepted = true;
            info.Order = m_FinishedCount;

            // 残りが1人だけになった：その人の最下位が確定する（2人以上で走っている時だけ）
            if (m_PlayerCount >= 2 && m_FinishedCount == m_PlayerCount - 1)
            {
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    if (!m_Finished[i])
                    {
                        m_LastPlaceIndex = i;
                        break;
                    }
                }

                info.LastPlaceConfirmed = true;
                info.LastPlaceIndex = m_LastPlaceIndex;
            }

            // レースの終わりを判定する（すでに終わっている時は、理由を変えない）
            if (!IsDecided)
            {
                if (m_FinishedCount >= m_PlayerCount)
                {
                    m_Reason = EndReason.AllFinished;
                }
                else if (m_EndWhenRankDecided &&
                         m_FinishedCount >= m_PlayerCount - 1)
                {
                    m_Reason = EndReason.RankDecided;
                }
            }

            info.RaceDecided = IsDecided;

            return info;
        }

        /// <summary>
        /// 時間による終わりを調べる。毎フレーム呼んでよい。
        /// 「最初のゴールから○秒」の設定を使っていて、その時間がたった時に、
        /// レースの終わりを決める。
        /// </summary>
        /// <param name="raceTime">スタートからの経過秒</param>
        /// <returns>この呼び出しで、新しく終わりが決まったら true</returns>
        public bool Tick(float raceTime)
        {
            if (IsDecided ||
                m_TimeoutAfterFirstFinish <= 0f ||
                m_FirstFinishTime < 0f)
            {
                return false;
            }

            if (raceTime - m_FirstFinishTime >= m_TimeoutAfterFirstFinish)
            {
                m_Reason = EndReason.Timeout;

                return true;
            }

            return false;
        }

        private bool IsValid(int index)
        {
            return index >= 0 && index < m_PlayerCount;
        }
    }


    /// <summary>
    /// タイムを文字にする。
    /// </summary>
    public static class RaceTimeFormat
    {
        /// <summary>
        /// 秒を「分:秒.ミリ秒」の形にする（例：83.456 → 「1:23.456」）。
        /// 負の値・NaN・無限大は「-:--.---」にする。
        /// </summary>
        public static string Format(float seconds)
        {
            if (float.IsNaN(seconds) ||
                float.IsInfinity(seconds) ||
                seconds < 0f)
            {
                return "-:--.---";
            }

            // ミリ秒に丸めてから分けると、59.9996 が 「0:60.000」にならない
            long totalMs = (long)Math.Round(seconds * 1000.0);

            long minutes = totalMs / 60000;
            long secs = (totalMs / 1000) % 60;
            long ms = totalMs % 1000;

            return minutes + ":" + secs.ToString("00") + "." + ms.ToString("000");
        }

        /// <summary>
        /// 秒を「分:秒.1/100秒」の形にする（例：83.456 → 「1:23.46」）。画面の隅に出す短い形。
        /// 負の値・NaN・無限大は「0:00.00」にする。
        /// </summary>
        public static string FormatShort(float seconds)
        {
            if (float.IsNaN(seconds) ||
                float.IsInfinity(seconds) ||
                seconds < 0f)
            {
                return "0:00.00";
            }

            long totalCs = (long)Math.Round(seconds * 100.0);

            long minutes = totalCs / 6000;
            long secs = (totalCs / 100) % 60;
            long cs = totalCs % 100;

            return minutes + ":" + secs.ToString("00") + "." + cs.ToString("00");
        }
    }


    /// <summary>
    /// 順位に付ける、英語の序数の語尾。
    /// </summary>
    public static class RaceOrdinal
    {
        /// <summary>
        /// 1 → "st"、2 → "nd"、3 → "rd"、4 → "th"。11〜13 は "th"。
        /// </summary>
        public static string Suffix(int rank)
        {
            int lastTwo = Math.Abs(rank) % 100;

            if (lastTwo >= 11 && lastTwo <= 13)
            {
                return "th";
            }

            switch (Math.Abs(rank) % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }
    }
}
