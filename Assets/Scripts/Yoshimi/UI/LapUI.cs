using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// プレイヤーのラップ数をUIに表示するクラス
    /// </summary>
    public class LapUI : MonoBehaviour
    {
        [Header("UI参照")]
        public Text lapText; // InspectorでTextをアサイン

        [Header("対象プレイヤー")]
        public LapCounter targetLapCounter; // InspectorでプレイヤーのLapCounterをアサイン

        void Start()
        {
            // lapTextが未設定なら、このGameObjectにアタッチされたTextを取得
            if (lapText == null)
                lapText = GetComponent<Text>();

            // 対象プレイヤーが設定されている場合
            if (targetLapCounter != null)
            {
                // ラップ更新イベントを購読
                targetLapCounter.OnLapUpdated += UpdateLapText;
                // 初期表示を更新
                UpdateLapText(targetLapCounter.currentLap);
            }
        }

        void OnDestroy()
        {
            // 破棄時にイベント購読を解除（メモリリーク防止）
            if (targetLapCounter != null)
                targetLapCounter.OnLapUpdated -= UpdateLapText;
        }

        /// <summary>
        /// ラップ数が更新されたときにUIテキストを更新する
        /// </summary>
        /// <param name="lap">現在のラップ数</param>
        void UpdateLapText(int lap)
        {
            if (lapText != null)
                // 例: "1 / 3" のように「現在ラップ / 最大ラップ」の形式で表示
                lapText.text = $"{lap} / {targetLapCounter.maxLaps}";
        }
    }
}