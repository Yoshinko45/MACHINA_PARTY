using System;
using System.Collections.Generic;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レースに出る人の情報（シーンをまたいで受け渡す）
    // =====================================================================
    //
    // 車種選択画面で決めた「誰が・どの入力で・どの車種か」を、
    // レースの進行（RaceGameManager）へ渡すための入れ物。
    // static なので、シーンを読み込み直しても残る
    // （「もう一度走る」で同じ顔ぶれのまま始められる）。
    //
    // タイトルへ戻る時などは、Clear() で空にする。
    //
    // =====================================================================


    /// <summary>
    /// レースに出る1人ぶんの情報。
    /// </summary>
    [Serializable]
    public sealed class RacePlayerEntry
    {
        /// <summary>プレイヤー番号（1〜4）。車の色と、画面分割の位置はこの番号で決まる</summary>
        public int PlayerNumber = 1;

        /// <summary>
        /// 入力の番号（InputReader.ControlIndex と同じ）。
        /// 0 ＝ 自動判定／1 ＝ WASD／2 ＝ 矢印キー／10以上 ＝ ゲームパッド（10 + 何台目か）
        /// </summary>
        public int ControlIndex = 1;

        /// <summary>選んだ車種の番号（CarTypeCatalog の番号）</summary>
        public int CarTypeIndex = 0;

        /// <summary>デバイスの表示名（例：「WASD」「Gamepad 1」）。表示にだけ使う</summary>
        public string DeviceLabel = "";

        public RacePlayerEntry()
        {
        }

        public RacePlayerEntry(
            int playerNumber,
            int controlIndex,
            int carTypeIndex,
            string deviceLabel)
        {
            PlayerNumber = playerNumber;
            ControlIndex = controlIndex;
            CarTypeIndex = carTypeIndex;
            DeviceLabel = deviceLabel;
        }

        /// <summary>同じ内容の、別のオブジェクトを作る</summary>
        public RacePlayerEntry Clone()
        {
            return new RacePlayerEntry(
                PlayerNumber,
                ControlIndex,
                CarTypeIndex,
                DeviceLabel);
        }
    }


    /// <summary>
    /// レースに出る人の一覧（static。シーンをまたいで残る）。
    /// </summary>
    public static class RaceSession
    {
        /// <summary>レースに出られる最大人数</summary>
        public const int MaxPlayers = 4;

        // 今回のレースに出る人
        private static readonly List<RacePlayerEntry> s_Players =
            new List<RacePlayerEntry>();

        // 直前に決めた顔ぶれの記憶。「車種を変える」で選択画面へ戻った時に、
        // 前の参加者と車種を最初から入れておくために使う（Clear では消えない）
        private static readonly List<RacePlayerEntry> s_Last =
            new List<RacePlayerEntry>();

        /// <summary>出る人数</summary>
        public static int Count
        {
            get { return s_Players.Count; }
        }

        /// <summary>出る人が1人以上決まっているか</summary>
        public static bool HasPlayers
        {
            get { return s_Players.Count > 0; }
        }

        /// <summary>
        /// 出る人の一覧を、プレイヤー番号の小さい順に並べて返す（コピー）。
        /// 返した配列を書き換えても、中身には影響しない。
        /// </summary>
        public static RacePlayerEntry[] GetPlayers()
        {
            RacePlayerEntry[] copy = new RacePlayerEntry[s_Players.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = s_Players[i].Clone();
            }

            return copy;
        }

        /// <summary>
        /// 出る人の一覧を入れ替える。
        /// ・プレイヤー番号の小さい順に並べる
        /// ・同じプレイヤー番号が重なったら、後のものを捨てる
        /// ・1〜4 の範囲外の番号や、null は捨てる
        /// ・車種の番号は、車種の数の範囲に収める
        /// </summary>
        /// <param name="entries">出る人（null なら空にする）</param>
        public static void Set(IEnumerable<RacePlayerEntry> entries)
        {
            s_Players.Clear();

            if (entries == null)
            {
                return;
            }

            foreach (RacePlayerEntry entry in entries)
            {
                if (entry == null ||
                    entry.PlayerNumber < 1 ||
                    entry.PlayerNumber > MaxPlayers ||
                    Contains(entry.PlayerNumber))
                {
                    continue;
                }

                RacePlayerEntry copy = entry.Clone();

                copy.CarTypeIndex = CarTypeCatalog.Wrap(copy.CarTypeIndex);

                s_Players.Add(copy);
            }

            s_Players.Sort(
                delegate (RacePlayerEntry a, RacePlayerEntry b)
                {
                    return a.PlayerNumber.CompareTo(b.PlayerNumber);
                });

            // 空でなければ、直前の顔ぶれとして覚えておく
            if (s_Players.Count > 0)
            {
                s_Last.Clear();

                for (int i = 0; i < s_Players.Count; i++)
                {
                    s_Last.Add(s_Players[i].Clone());
                }
            }
        }

        /// <summary>
        /// 出る人を、全員いなくする（選択画面からやり直す時など）。
        /// 直前の顔ぶれの記憶は残る（GetLastPlayers で取れる）。
        /// </summary>
        public static void Clear()
        {
            s_Players.Clear();
        }

        /// <summary>
        /// 出る人も、直前の顔ぶれの記憶も、全部消す（タイトルから新しく始める時）。
        /// </summary>
        public static void ClearAll()
        {
            s_Players.Clear();
            s_Last.Clear();
        }

        /// <summary>
        /// 直前に決めた顔ぶれを返す（コピー）。無ければ空の配列。
        /// 選択画面に戻った時に、前の参加者と車種を最初から入れるために使う。
        /// </summary>
        public static RacePlayerEntry[] GetLastPlayers()
        {
            RacePlayerEntry[] copy = new RacePlayerEntry[s_Last.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = s_Last[i].Clone();
            }

            return copy;
        }

        private static bool Contains(int playerNumber)
        {
            for (int i = 0; i < s_Players.Count; i++)
            {
                if (s_Players[i].PlayerNumber == playerNumber)
                {
                    return true;
                }
            }

            return false;
        }
    }


    // =====================================================================
    // ■ 車種選択画面の中身（誰が参加して、どの車種で、準備できたか）
    // =====================================================================
    //
    // 画面の見た目やボタンの読み取りは CarSelectMenu が行い、
    // 「参加・車種の切り替え・準備完了・取り消し」のルールだけを、ここにまとめてある。
    // Unity の機能を使わないので、そのままテストできる。
    //
    // 枠は4つ（P1〜P4）。参加した順に、空いている一番若い枠に入る。
    // 同じ入力装置（キーボード・ゲームパッド）は、1つの枠にしか入れない。
    //
    // =====================================================================

    /// <summary>
    /// 車種選択画面の、4つの枠の状態。
    /// </summary>
    public sealed class CarSelectModel
    {
        /// <summary>1つの枠の状態</summary>
        public sealed class Slot
        {
            /// <summary>参加しているか</summary>
            public bool Joined;

            /// <summary>使っている入力装置の番号（InputReader.ControlIndex と同じ）</summary>
            public int ControlIndex;

            /// <summary>入力装置の表示名</summary>
            public string DeviceLabel = "";

            /// <summary>選んでいる車種の番号</summary>
            public int CarTypeIndex;

            /// <summary>「準備完了」を押したか</summary>
            public bool Ready;
        }

        /// <summary>枠の数</summary>
        public const int SlotCount = RaceSession.MaxPlayers;

        private readonly Slot[] m_Slots = new Slot[SlotCount];

        public CarSelectModel()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                m_Slots[i] = new Slot();
            }
        }

        /// <summary>枠の状態を取る（範囲外の番号は null）</summary>
        public Slot Get(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return null;
            }

            return m_Slots[slotIndex];
        }

        /// <summary>参加している人数</summary>
        public int JoinedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < SlotCount; i++)
                {
                    if (m_Slots[i].Joined) count++;
                }

                return count;
            }
        }

        /// <summary>
        /// 全員が準備完了か。1人以上参加していて、参加者の全員が準備完了なら true。
        /// </summary>
        public bool AllReady
        {
            get
            {
                int joined = 0;

                for (int i = 0; i < SlotCount; i++)
                {
                    if (!m_Slots[i].Joined) continue;

                    joined++;

                    if (!m_Slots[i].Ready) return false;
                }

                return joined > 0;
            }
        }

        /// <summary>この入力装置が入っている枠の番号。入っていなければ -1</summary>
        public int FindSlotByDevice(int controlIndex)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (m_Slots[i].Joined && m_Slots[i].ControlIndex == controlIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 参加する。空いている一番若い枠に入る。
        /// すでに参加している入力装置や、枠が満員の時は、入れない。
        /// </summary>
        /// <param name="controlIndex">入力装置の番号</param>
        /// <param name="deviceLabel">入力装置の表示名</param>
        /// <param name="carTypeIndex">最初に選ぶ車種の番号</param>
        /// <returns>入った枠の番号。入れなかったら -1</returns>
        public int TryJoin(int controlIndex, string deviceLabel, int carTypeIndex)
        {
            if (FindSlotByDevice(controlIndex) >= 0)
            {
                return -1;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (m_Slots[i].Joined) continue;

                Slot slot = m_Slots[i];

                slot.Joined = true;
                slot.ControlIndex = controlIndex;
                slot.DeviceLabel = deviceLabel ?? "";
                slot.CarTypeIndex = CarTypeCatalog.Wrap(carTypeIndex);
                slot.Ready = false;

                return i;
            }

            return -1;
        }

        /// <summary>
        /// 車種を切り替える（direction が +1 なら次、-1 なら前。端はつながる）。
        /// 参加していない枠や、準備完了の枠は変えられない。
        /// </summary>
        /// <returns>変えられたら true</returns>
        public bool Cycle(int slotIndex, int direction)
        {
            Slot slot = Get(slotIndex);

            if (slot == null || !slot.Joined || slot.Ready || direction == 0)
            {
                return false;
            }

            slot.CarTypeIndex =
                CarTypeCatalog.Wrap(slot.CarTypeIndex + (direction > 0 ? 1 : -1));

            return true;
        }

        /// <summary>
        /// 準備完了にする／戻す。参加していない枠は変えられない。
        /// </summary>
        /// <returns>変えられたら true</returns>
        public bool SetReady(int slotIndex, bool ready)
        {
            Slot slot = Get(slotIndex);

            if (slot == null || !slot.Joined || slot.Ready == ready)
            {
                return false;
            }

            slot.Ready = ready;

            return true;
        }

        /// <summary>
        /// 参加をやめる（枠を空ける）。
        /// </summary>
        /// <returns>やめられたら true</returns>
        public bool Leave(int slotIndex)
        {
            Slot slot = Get(slotIndex);

            if (slot == null || !slot.Joined)
            {
                return false;
            }

            slot.Joined = false;
            slot.ControlIndex = 0;
            slot.DeviceLabel = "";
            slot.CarTypeIndex = 0;
            slot.Ready = false;

            return true;
        }

        /// <summary>
        /// 取り消し操作。準備完了なら準備を解除し、そうでなければ参加をやめる。
        /// </summary>
        /// <returns>何か変わったら true</returns>
        public bool Cancel(int slotIndex)
        {
            Slot slot = Get(slotIndex);

            if (slot == null || !slot.Joined)
            {
                return false;
            }

            if (slot.Ready)
            {
                return SetReady(slotIndex, false);
            }

            return Leave(slotIndex);
        }

        /// <summary>
        /// 前回の顔ぶれを、最初から入れておく（準備完了にはしない）。
        /// 今ある参加者は消える。
        /// </summary>
        public void Preload(RacePlayerEntry[] entries)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Leave(i);
            }

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                RacePlayerEntry entry = entries[i];

                if (entry == null ||
                    entry.PlayerNumber < 1 ||
                    entry.PlayerNumber > SlotCount ||
                    FindSlotByDevice(entry.ControlIndex) >= 0)
                {
                    continue;
                }

                Slot slot = m_Slots[entry.PlayerNumber - 1];

                if (slot.Joined)
                {
                    continue;
                }

                slot.Joined = true;
                slot.ControlIndex = entry.ControlIndex;
                slot.DeviceLabel = entry.DeviceLabel ?? "";
                slot.CarTypeIndex = CarTypeCatalog.Wrap(entry.CarTypeIndex);
                slot.Ready = false;
            }
        }

        /// <summary>
        /// 参加している人を、レースに渡す形にする。
        /// プレイヤー番号は枠の番号（P1〜P4）のまま。
        /// </summary>
        public RacePlayerEntry[] ToEntries()
        {
            List<RacePlayerEntry> list = new List<RacePlayerEntry>();

            for (int i = 0; i < SlotCount; i++)
            {
                Slot slot = m_Slots[i];

                if (!slot.Joined) continue;

                list.Add(
                    new RacePlayerEntry(
                        i + 1,
                        slot.ControlIndex,
                        slot.CarTypeIndex,
                        slot.DeviceLabel));
            }

            return list.ToArray();
        }
    }
}
