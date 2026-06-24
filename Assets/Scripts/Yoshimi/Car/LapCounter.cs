using UnityEngine;

namespace Tanks.Complete
{
    public class LapCounter : MonoBehaviour
    {
        [Header("ラップ設定")]
        public int maxLaps = 3;          // 最大ラップ数（3周）
        public int currentLap = 1;       // 現在のラップ数（1〜maxLaps）

        // ラップが更新されたときに呼ばれるイベント（UI更新用）
        public System.Action<int> OnLapUpdated;

        // ゴールライン通過時に呼び出すメソッド
        public void PassGoal()
        {
            currentLap++;
            if (currentLap > maxLaps)
            {
                currentLap = maxLaps; // 3周以上にはしない
                // ここで「ゴール済み」フラグを立てるなども可
            }

            // UIなどに通知
            OnLapUpdated?.Invoke(currentLap);
        }

        // ラウンド開始時にリセット
        public void ResetLap()
        {
            currentLap = 1;
            OnLapUpdated?.Invoke(currentLap);
        }
    }
}