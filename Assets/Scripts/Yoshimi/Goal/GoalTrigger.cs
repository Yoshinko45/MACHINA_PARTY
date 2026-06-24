using UnityEngine;

namespace Tanks.Complete
{
    public class GoalTrigger : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            // プレイヤーの車かどうか確認（例：タグ "Player" やレイヤーで判定）
            if (!other.CompareTag("Player")) return;

            // 車に LapCounter コンポーネントがあるか確認
            LapCounter lapCounter = other.GetComponent<LapCounter>();
            if (lapCounter == null) return;

            // ラップを進める
            lapCounter.PassGoal();
        }
    }
}