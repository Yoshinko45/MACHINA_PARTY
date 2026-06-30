using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// 周回（ラップ）とチェックポイントの通過状況を管理するクラス。
    /// ・最初の1周目はZ座標が一定以上になるまでゴール通過を無効に。
    /// ・チェックポイントは順番通りに通過した場合のみカウント。
    /// ・すべてのチェックポイントを通過した後にゴールを通過するとラップが進む。
    /// </summary>
    public class LapCounter : MonoBehaviour
    {
        // --- 公開フィールド（インスペクタで設定可能） ---

        [Header("ラップ設定")]
        public int maxLaps = 3;        // 最大ラップ数（例：3周でゴール）
        public int currentLap = 1;    // 現在のラップ数（1から開始）

        [Header("チェックポイント数")]
        public int totalCheckPoints = 5; // コース全体のチェックポイント数

        [Header("Z座標チェック")]
        public float firstLapEnableZ = 100f; // z >= この値になったら初めてゴール通過を有効にする

        // --- 内部状態（プライベート） ---

        // 最初の1周目が完了したかどうか
        // （Z座標が firstLapEnableZ 以上になったら true になる）
        private bool hasCompletedFirstLap = false;

        // 現在のチェックポイント番号（次に通過すべき番号）
        // 例：1,2,3...totalCheckPoints → 1（次のラップ）の順で増えていく
        private int currentCheckPoint = 1;

        // 現在のラップでゴールを通過したかどうか
        // 1ラップにつき1回だけカウントするために使用
        private bool hasPassedGoalThisLap = false;

        // --- イベント（外部から購読可能） ---

        /// <summary>
        /// ラップ数が更新されたときに呼ばれるイベント。
        /// 引数には新しいラップ数（currentLap）が渡される。
        /// </summary>
        public System.Action<int> OnLapUpdated;

        // --- 毎フレームの処理 ---

        void Update()
        {
            // Z座標チェック（最初の1周目がまだ完了していない場合）
            if (!hasCompletedFirstLap)
            {
                // プレイヤー（このスクリプトがアタッチされたオブジェクト）のZ座標が
                // firstLapEnableZ 以上になったら、初周完了とみなす
                if (transform.position.z >= firstLapEnableZ)
                {
                    // z >= 100 になったら「最初の1周目が完了した」とマーク
                    hasCompletedFirstLap = true;
                }
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
            // 最初の1周目がまだ完了していない場合は、ここでは何もしない
            // （Z座標が firstLapEnableZ に達するまではチェックポイントを無視）
            if (!hasCompletedFirstLap)
            {
                return;
            }

            // 順番通りに通過しているかチェック
            if (checkPointNumber == currentCheckPoint)
            {
                // 正しい順番で通過したので、次のチェックポイントに進める
                currentCheckPoint++;

                // すべてのチェックポイントを通過し終えたかチェック
                if (currentCheckPoint > totalCheckPoints)
                {
                    // 次のラップ用に1にリセット
                    currentCheckPoint = 1;
                }
            }
            else
            {
                // 順番通りでない場合は無視（不正な順序通過や逆走を防ぐ）
                return;
            }
        }

        // --- ゴール通過時の処理 ---

        /// <summary>
        /// ゴールラインを通過したときに呼び出し。
        /// すべてのチェックポイントを順番通りに通過していれば、ラップを進める。
        /// </summary>
        public void PassGoal()
        {
            // 最初の1周目がまだ完了していない場合は、ここでは何もしない
            // （Z座標が firstLapEnableZ に達するまではゴール通過を無視）
            if (!hasCompletedFirstLap)
            {
                return;
            }

            // すべてのチェックポイントを通過しているかチェック
            // currentCheckPoint が 1 になっている = 前のラップですべてのチェックポイントを通過済み
            if (currentCheckPoint != 1)
            {
                // まだすべてのチェックポイントを通過していない
                return;
            }

            // 現在のラップで既にゴールを通過している場合は無視
            // （1ラップにつき1回だけカウントするため）
            if (hasPassedGoalThisLap)
            {
                return;
            }

            // 現在のラップで初めてゴールを通過したことを記録
            hasPassedGoalThisLap = true;

            // ラップを進める
            currentLap++;

            // 最大ラップ数を超えないように制限
            if (currentLap > maxLaps)
            {
                currentLap = maxLaps;
                // ここで「ゴール済み」フラグを立てるなども可能
            }

            // ラップ更新イベントを発行（UI更新などに利用）
            OnLapUpdated?.Invoke(currentLap);
        }

        // --- リセット処理 ---

        /// <summary>
        /// ラップとチェックポイントの状態を初期状態にリセット。
        /// レース開始時やリトライ時に呼び出すことを想定。
        /// </summary>
        public void ResetLap()
        {
            // ラップ数を1に戻す
            currentLap = 1;

            // チェックポイントを1に戻す
            currentCheckPoint = 1;

            // 初周完了フラグを戻す（Z座標チェックからやり直し）
            hasCompletedFirstLap = false;

            // 現在のラップでゴール通過済みフラグを戻す
            hasPassedGoalThisLap = false;

            // ラップ更新イベントを発行（UIを1周目表示に戻すなど）
            OnLapUpdated?.Invoke(currentLap);
        }
    }
}