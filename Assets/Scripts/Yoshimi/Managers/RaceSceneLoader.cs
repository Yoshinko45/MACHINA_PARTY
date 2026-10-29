using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks.Complete
{
    // =====================================================================
    // ■ レースのシーンへの切り替え
    // =====================================================================
    //
    // タイトル画面の「レースモード」ボタンなどから、レースのシーンを読み込む。
    // 読み込んだ先のシーンでは、RaceGameManager が車種選択画面から始める。
    //
    // 使い方（タイトルのシーン）
    //   ① 空の GameObject にこのスクリプトを付ける
    //   ② Race Scene Name に、レースのシーンの名前を入れる
    //      （そのシーンは File → Build Profiles（Build Settings）の Scenes In Build に入れておく）
    //   ③ レースモードのボタンの On Click に、この GameObject の LoadRaceScene を設定する
    //
    // 注意
    //   TitleScreenButtonInput を付けたボタンは、Space・Enter・ゲームパッドの A で、
    //   そのボタンの On Click を押したことにする。
    //   同じシーンにそのボタンが2つ以上あると、1回の入力で全部の On Click が動いてしまう。
    //   レースモードのボタンだけに付けるか、選んでいるボタンだけが動くようにしてほしい。
    //
    // =====================================================================

    /// <summary>
    /// レースのシーンを読み込む（タイトルのボタンから呼ぶ）。
    /// </summary>
    public class RaceSceneLoader : MonoBehaviour
    {
        [Tooltip("読み込むレースのシーンの名前（Build Settings に入れておいてください）。" +
            "空の時は、下の番号を使います")]
        public string m_RaceSceneName = "";

        [Tooltip("レースのシーンの Build Settings での番号。名前が空の時に使います（-1 ＝ 未設定）")]
        public int m_RaceSceneBuildIndex = -1;

        [Tooltip("ONなら、前回の参加者を忘れて、車種選択を最初からやり直す。" +
            "OFFなら、前回の顔ぶれと車種が最初から入った状態で車種選択が始まる")]
        public bool m_ForgetPreviousPlayers = true;

        /// <summary>
        /// レースのシーンを読み込む。ボタンの On Click から呼ぶ。
        /// </summary>
        public void LoadRaceScene()
        {
            if (m_ForgetPreviousPlayers)
            {
                RaceSession.ClearAll();
            }
            else
            {
                // 顔ぶれの記憶は残すが、「すぐレースを始める」状態にはしない（車種選択から始める）
                RaceSession.Clear();
            }

            LoadScene(m_RaceSceneName, m_RaceSceneBuildIndex);
        }

        /// <summary>
        /// シーンを読み込む。名前が空でなければ名前で、空なら番号で読み込む。
        /// 読み込めない時は、原因を Console に出す（何も起きないように見えないように）。
        /// </summary>
        /// <param name="sceneName">シーンの名前（空なら番号を使う）</param>
        /// <param name="buildIndex">Build Settings での番号</param>
        /// <returns>読み込みを始められたら true</returns>
        public static bool LoadScene(string sceneName, int buildIndex)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogError(
                        "RaceSceneLoader : シーン「" + sceneName + "」を読み込めません。" +
                        "名前が合っているか、Build Settings の Scenes In Build に入っているか確認してください。");

                    return false;
                }

                SceneManager.LoadScene(sceneName);

                return true;
            }

            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError(
                    "RaceSceneLoader : 読み込むシーンが設定されていません" +
                    "（シーン名が空で、番号 " + buildIndex + " も使えません。" +
                    "Build Settings のシーン数は " + SceneManager.sceneCountInBuildSettings + " です）。" +
                    "シーン名か番号を設定してください。");

                return false;
            }

            SceneManager.LoadScene(buildIndex);

            return true;
        }
    }
}
