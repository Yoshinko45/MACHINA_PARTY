using UnityEngine;

namespace Tanks.Complete
{
    public class CheckPoint : MonoBehaviour
    {
        // Inspector上で分かりやすくするための見出し
        [Header("チェックポイント番号")]
        // チェックポイントの番号（例：1〜5）
        public int checkPointNumber = 1;

        // 他のColliderがこのオブジェクトのトリガーに入ったときに呼ばれる
        void OnTriggerEnter(Collider other)
        {
            // タグが「Player」でなければ何もせず終了
            if (!other.CompareTag("Player")) return;

            // プレイヤー側のLapCounterコンポーネントを取得
            LapCounter lapCounter = other.GetComponent<LapCounter>();

            // LapCounterがなければ何もせず終了
            if (lapCounter == null) return;

            // LapCounterに対して「このチェックポイントを通過した」と通知
            lapCounter.PassCheckPoint(checkPointNumber);
        }
    }
}