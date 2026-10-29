using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// 周回（ラップ）とチェックポイントの通過状況を管理するクラス。車1台に1つ付ける。
    ///
    /// 数え方のルール（実際の数え方は LapProgress に書いてある）
    /// ・チェックポイントは、1番から順番に通った時だけ数える。
    /// ・全部のチェックポイントを通った後に、ゴールを通ると1周になる。
    ///   スタート直後や、同じゴールを続けて通っても、周回は増えない。
    /// ・規定の周回数（maxLaps）のゴールを通ったら「ゴール完了」（IsFinished）。
    ///   その後の通過は全部無視する。
    ///
    /// 呼び出し元は、CheckPoint（チェックポイント）と GoalTrigger（ゴール）。
    /// </summary>
    public class LapCounter : MonoBehaviour
    {
        // --- 公開フィールド（インスペクタで設定可能） ---

        [Header("ラップ設定")]
        public int maxLaps = 3;        // 最大ラップ数（例：3周でゴール）
        public int currentLap = 1;     // 現在のラップ数（1から開始。ゴール完了後は maxLaps のまま）

        [Header("チェックポイント数")]
        public int totalCheckPoints = 5; // コース全体のチェックポイント数

        [Header("（現在は使っていません）")]
        [Tooltip("以前は「Z座標がこの値になるまで、ゴール通過を無効にする」ために使っていました。\n" +
            "今は、チェックポイントを順番に全部通らないと周回が増えない作りなので、この値は使いません。\n" +
            "（既存のシーンの設定値を壊さないよう、項目だけ残してあります）")]
        public float firstLapEnableZ = 100f;

        // --- 内部状態（プライベート） ---

        // 周回とチェックポイントの数え方（Unityに依存しない部分）
        private readonly LapProgress m_Progress = new LapProgress();

        // --- イベント（外部から購読可能） ---

        /// <summary>
        /// 新しい周に入ったとき（と、リセットしたとき）に呼ばれるイベント。
        /// 引数には新しいラップ数（currentLap）が渡される。
        /// ゴール完了のときは呼ばれない（代わりに OnFinished が呼ばれる）。
        /// </summary>
        public System.Action<int> OnLapUpdated;

        /// <summary>
        /// 規定の周回を走り切った（ゴール完了した）ときに、1回だけ呼ばれるイベント。
        /// 引数にはこの LapCounter が渡される。
        /// </summary>
        public System.Action<LapCounter> OnFinished;

        // --- 外から見える状態 ---

        /// <summary>規定の周回を走り切ったか</summary>
        public bool IsFinished
        {
            get
            {
                SyncSettings();

                return m_Progress.IsFinished;
            }
        }

        /// <summary>終えた周の数</summary>
        public int LapsCompleted
        {
            get
            {
                SyncSettings();

                return m_Progress.LapsCompleted;
            }
        }

        /// <summary>
        /// 進んだ区間の数（チェックポイントとゴールを通るたびに1つ増える）。
        /// 順位を決めるために使う。大きいほど先に進んでいる。
        /// </summary>
        public int ProgressUnits
        {
            get
            {
                SyncSettings();

                return m_Progress.Units;
            }
        }

        /// <summary>
        /// 次に通るべきチェックポイントの番号（1から）。
        /// 全部通り終えて次はゴールの時や、ゴール完了後は 0。
        /// </summary>
        public int NextCheckPointNumber
        {
            get
            {
                SyncSettings();

                return m_Progress.NextCheckPoint;
            }
        }

        // --- チェックポイント通過時の処理 ---

        /// <summary>
        /// チェックポイントを通過したときに呼び出し。
        /// 引数には通過したチェックポイントの番号を指定。
        /// </summary>
        /// <param name="checkPointNumber">通過したチェックポイント番号</param>
        public void PassCheckPoint(int checkPointNumber)
        {
            SyncSettings();

            // 順番通りでない通過（飛ばし・逆走・二重）は、LapProgress が無視する
            m_Progress.PassCheckPoint(checkPointNumber);
        }

        // --- ゴール通過時の処理 ---

        /// <summary>
        /// ゴールラインを通過したときに呼び出し。
        /// すべてのチェックポイントを順番通りに通過していれば、ラップを進める。
        /// 規定の周回を走り切った時は、OnFinished を呼ぶ。
        /// </summary>
        public void PassGoal()
        {
            SyncSettings();

            LapProgress.GateResult result = m_Progress.PassGoal();

            // 数えなかった（チェックポイントが足りない・二重・ゴール完了後）なら何もしない
            if (result == LapProgress.GateResult.Ignored)
            {
                return;
            }

            currentLap = m_Progress.CurrentLap;

            if (result == LapProgress.GateResult.Finished)
            {
                // ゴール完了：周回の更新ではなく、完了を知らせる
                OnFinished?.Invoke(this);

                return;
            }

            // 次の周に入った：ラップ更新イベントを発行（UIの更新などに利用）
            OnLapUpdated?.Invoke(currentLap);
        }

        // --- リセット処理 ---

        /// <summary>
        /// ラップとチェックポイントの状態を初期状態にリセット。
        /// レース開始時やリトライ時に呼び出すことを想定。
        /// </summary>
        public void ResetLap()
        {
            SyncSettings();

            m_Progress.Reset();

            // ラップ数を1に戻す
            currentLap = 1;

            // ラップ更新イベントを発行（UIを1周目表示に戻すなど）
            OnLapUpdated?.Invoke(currentLap);
        }

        // --- 内部処理 ---

        /// <summary>
        /// インスペクタや他のスクリプトが変えた設定（maxLaps・totalCheckPoints）を、
        /// 数え方（LapProgress）に反映する。
        /// 外から currentLap が書き換えられていても、数え方の側を正とする。
        /// </summary>
        private void SyncSettings()
        {
            m_Progress.Configure(maxLaps, totalCheckPoints);

            currentLap = m_Progress.CurrentLap;
        }
    }
}
