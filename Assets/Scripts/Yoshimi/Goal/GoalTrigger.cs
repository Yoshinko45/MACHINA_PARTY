using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// ゴールラインにプレイヤーが侵入したことを検知し、
    /// 周回カウンタに「ゴール通過」を通知するクラス。
    /// </summary>
    public class GoalTrigger : MonoBehaviour
    {
        /// <summary>
        /// 他のコライダーがこのオブジェクトのトリガーに入ったときに呼ばれる。
        /// </summary>
        /// <param name="other">侵入してきたコライダー</param>
        void OnTriggerEnter(Collider other)
        {
            // 侵入してきたオブジェクトが「Player」タグを持っていなければ処理を終了
            if (!other.CompareTag("Player")) return;

            // プレイヤーオブジェクトから LapCounter コンポーネントを取得
            LapCounter lapCounter = other.GetComponent<LapCounter>();

            // LapCounter コンポーネントがアタッチされていなければ処理を終了
            if (lapCounter == null) return;

            // ゴール通過を LapCounter に通知（周回数を増やすなどの処理を実行）
            lapCounter.PassGoal();
        }
    }
}