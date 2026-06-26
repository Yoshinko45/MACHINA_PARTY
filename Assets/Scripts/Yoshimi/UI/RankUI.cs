using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Tanks.Complete
{
    /// <summary>
    /// 順位表示用のUIコンポーネント。
    /// 順位の数字と接尾辞（st, nd, rd, th）を表示し、
    /// 順位に応じた色付けとアニメーションを行う。
    /// </summary>
    public class RankUI : MonoBehaviour
    {
        // --- UI参照 ---
        [Header("UI参照")]
        // 順位の数字部分（例: 1）
        public Text rankNumberText;
        // 順位の接尾辞部分（例: st）
        public Text rankSuffixText;

        // --- 色設定 ---
        [Header("色設定")]
        // 1位の色（金色）
        public Color firstColor  = new Color(1.0f, 0.84f, 0.0f);
        // 2位の色（銀色）
        public Color secondColor = new Color(0.75f, 0.75f, 0.75f);
        // 3位の色（銅色）
        public Color thirdColor  = new Color(0.80f, 0.50f, 0.20f);
        // 4位の色（白）
        public Color fourthColor = Color.white;

        // --- アニメーション設定 ---
        [Header("アニメーション設定")]
        // 回転アニメーションにかける時間（秒）
        public float rotateDuration = 0.3f;
        // フェードイン・フェードアウトにかける時間（秒）
        public float fadeDuration   = 0.2f;
        // 拡大アニメーションの倍率
        public float scaleMultiplier = 1.2f;

        // 現在表示している順位
        private int currentRank = 1;
        // 実行中のアニメーションコルーチンの参照
        private Coroutine animationCoroutine;

        /// <summary>
        /// 初期化処理。
        /// Textコンポーネントの参照を確保し、初期順位を表示する。
        /// </summary>
        void Start()
        {
            // rankNumberText が未設定の場合は子オブジェクトから取得
            if (rankNumberText == null)
                rankNumberText = GetComponentInChildren<Text>();

            // 初期順位をUIに反映
            UpdateRankText(currentRank);
        }

        /// <summary>
        /// 外部から順位を更新するための公開メソッド。
        /// 同じ順位の場合は何もせず、異なる順位の場合はアニメーションを開始する。
        /// </summary>
        /// <param name="newRank">新しい順位</param>
        public void SetRank(int newRank)
        {
            // 同じ順位なら何もしない
            if (newRank == currentRank) return;

            // 既にアニメーションが動いていれば停止
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            // 新しい順位変更アニメーションを開始
            animationCoroutine = StartCoroutine(RankChangeAnimation(newRank));
        }

        /// <summary>
        /// 順位変更時のアニメーションコルーチン。
        /// フェードアウト → 回転＋拡大 → 順位更新 → フェードイン＋縮小＋回転リセット
        /// の順で処理を行う。
        /// </summary>
        /// <param name="newRank">新しい順位</param>
        private IEnumerator RankChangeAnimation(int newRank)
        {
            // --- ① フェードアウト ---
            float t = 0f;
            // 現在の色を保持
            Color startNumColor = rankNumberText.color;
            Color startSufColor = rankSuffixText.color;
            // 透明色
            Color transparent = new Color(1f, 1f, 1f, 0f);

            // fadeDuration の時間で徐々に透明にする
            while (t < 1f)
            {
                t += Time.deltaTime / fadeDuration;
                rankNumberText.color = Color.Lerp(startNumColor, transparent, t);
                rankSuffixText.color = Color.Lerp(startSufColor, transparent, t);
                yield return null;
            }

            // --- ② 回転＋スケール（親オブジェクトごと回す） ---
            t = 0f;
            // RankUI自体を回転・拡大する
            Transform parentTransform = transform;
            Vector3 startScale = parentTransform.localScale;
            Vector3 targetScale = startScale * scaleMultiplier;

            // rotateDuration の時間で拡大しながら1回転させる
            while (t < 1f)
            {
                t += Time.deltaTime / rotateDuration;
                // スケールを線形補間
                parentTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                // 毎フレーム一定角度ずつ回転
                parentTransform.Rotate(0f, 0f, 360f * Time.deltaTime / rotateDuration);
                yield return null;
            }

            // --- ③ 順位を更新 ---
            currentRank = newRank;
            // UIテキストと色を更新
            UpdateRankText(currentRank);

            // --- ④ フェードイン＋スケール戻し ---
            t = 0f;
            // 現在の色（透明）を保持
            startNumColor = rankNumberText.color;
            startSufColor = rankSuffixText.color;
            // 新しい順位に対応した色を取得
            Color targetColor = GetRankColor(currentRank);

            // fadeDuration の時間で色を戻しつつ縮小する
            while (t < 1f)
            {
                t += Time.deltaTime / fadeDuration;
                rankNumberText.color = Color.Lerp(startNumColor, targetColor, t);
                rankSuffixText.color = Color.Lerp(startSufColor, targetColor, t);
                parentTransform.localScale = Vector3.Lerp(targetScale, startScale, t);
                yield return null;
            }

            // --- 最終値で確定 ---
            rankNumberText.color = targetColor;
            rankSuffixText.color = targetColor;
            parentTransform.localScale = startScale;
            // 回転を完全にリセット（Z軸回転を0に）
            parentTransform.rotation = Quaternion.identity;

            // アニメーション終了
            animationCoroutine = null;
        }

        /// <summary>
        /// 順位に応じた色を返す。
        /// 1〜4位は専用の色、それ以外は白を返す。
        /// </summary>
        /// <param name="rank">順位</param>
        /// <returns>対応する色</returns>
        private Color GetRankColor(int rank)
        {
            switch (rank)
            {
                case 1: return firstColor;
                case 2: return secondColor;
                case 3: return thirdColor;
                case 4: return fourthColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 順位の数字と接尾辞をUIに反映する。
        /// </summary>
        /// <param name="rank">表示する順位</param>
        private void UpdateRankText(int rank)
        {
            // Textコンポーネントが未設定なら何もしない
            if (rankNumberText == null || rankSuffixText == null) return;

            // 数字部分を更新
            rankNumberText.text = rank.ToString();

            // 接尾辞部分を更新
            string suffix = GetRankSuffix(rank);
            rankSuffixText.text = suffix;

            // 順位に応じた色を適用
            Color color = GetRankColor(rank);
            rankNumberText.color = color;
            rankSuffixText.color = color;
        }

        /// <summary>
        /// 順位に応じた英語の序数接尾辞（st, nd, rd, th）を返す。
        /// </summary>
        /// <param name="rank">順位</param>
        /// <returns>対応する接尾辞</returns>
        private string GetRankSuffix(int rank)
        {
            switch (rank)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                case 4: return "th";
                default: return "th";
            }
        }
    }
}