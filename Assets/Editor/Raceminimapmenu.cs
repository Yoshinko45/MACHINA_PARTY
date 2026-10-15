#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Tanks.Complete
{
    // =====================================================
    // ■ ミニマップ作成メニュー（Editor専用）
    // =====================================================
    //
    // Hierarchyの右クリック → UI → Race Minimap で、
    // Canvasの下にミニマップのオブジェクトを作る。
    //
    // 作られるもの
    //   Minimap（RectTransform + RaceMinimap）
    //   ・画面右下、順位表示（RankUI）の少し上に配置
    //   ・Course Root に「TrackCourse」を自動で設定
    //
    // このスクリプトは、Editor フォルダに入れるのが基本。
    // ファイル全体を #if UNITY_EDITOR で囲んであるので、他のフォルダに置いても、
    // ビルドした製品には含まれない（メニューは Unity エディタの中でだけ使う）。
    //
    // このスクリプトが無くても、ミニマップ自体は動く。その場合は、
    // Canvasの下に空のUIオブジェクトを作り、RaceMinimapコンポーネントを手動で付ければよい
    //
    // =====================================================

    /// <summary>
    /// Hierarchyの右クリックメニューに「UI → Race Minimap」を足して、
    /// ミニマップを自動で作るエディタ用のクラス。
    /// ゲームの実行中には使われない。
    /// </summary>
    public static class RaceMinimapMenu
    {
        // コース（道路のColliderが入っているオブジェクト）の名前。
        // この名前のオブジェクトを探して、ミニマップの Course Root に設定する
        private const string COURSE_ROOT_NAME = "TrackCourse";


        // =====================================================
        // ■ メニュー本体
        // =====================================================

        /// <summary>
        /// メニュー「GameObject → UI → Race Minimap」を選んだ時に呼ばれる。
        /// Canvasの下に、RaceMinimap を付けたオブジェクト「Minimap」を作る。
        /// </summary>
        /// <param name="command">実行情報（context ＝ 右クリックしたオブジェクト）</param>
        // メニューの場所と並び順（"GameObject/UI/…" にすると、Hierarchyの右クリックにも出る）。
        // false ＝ 実行する関数（使えるかを調べる関数ではない）
        // 2100 ＝ メニュー内の並び順（数字が小さいほど上）
        [MenuItem("GameObject/UI/Race Minimap", false, 2100)]
        private static void CreateMinimap(MenuCommand command)
        {
            // 右クリックされたオブジェクトを取り出す
            // （メニューバーから実行した時は、null になることがある）。
            // 複数のオブジェクトを選択していると、メニューが選択数ぶん呼ばれてしまうので、
            // アクティブな（今選んでいる）1つの時だけ処理する
            GameObject context = command.context as GameObject;

            if (context != null && context != Selection.activeGameObject)
            {
                return;
            }


            // ---------------------------------------------
            // 作る場所（Canvasの中）を決める
            // ---------------------------------------------

            // ミニマップを入れる親を探す（Canvasの中が見つからなければ null）
            Transform parent = FindParent(context);

            if (parent == null)
            {
                // Canvasが無いと置き場所が無いので、ダイアログで知らせて、何も作らずに終わる
                EditorUtility.DisplayDialog(
                    "Race Minimap",
                    "シーンにCanvasが見つかりません。\n" +
                    "先にCanvas（UI → Canvas）を作ってから、もう一度実行してください。",
                    "OK"
                );

                return;
            }


            // ---------------------------------------------
            // オブジェクトを作る
            // ---------------------------------------------

            // 「Minimap」という名前の空のオブジェクトを作る（UIなので RectTransform を持たせる）
            GameObject go = new GameObject("Minimap", typeof(RectTransform));

            // 「元に戻す」（Ctrl+Z）で、作ったオブジェクトを消せるようにする
            Undo.RegisterCreatedObjectUndo(go, "Create Race Minimap");

            // 親と同じレイヤーにする（UIを映すカメラのレイヤー設定に合わせるため）
            go.layer = parent.gameObject.layer;

            RectTransform rect = go.GetComponent<RectTransform>();

            // 親（Canvas）の子にする。false ＝ ワールド位置は保たず、親を基準にした位置・
            // 大きさのままにする
            rect.SetParent(parent, false);

            // 画面右下、順位表示（RankUI）の少し上に置く（RaceMinimap.Reset と同じ配置）。
            // anchorMin / anchorMax / pivot を (1, 0) にして、基準を右下の角にする。
            // anchoredPosition ＝ 右端から40、下端から250ピクセルの位置
            // sizeDelta ＝ 360 × 360 ピクセルの大きさ
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 250f);
            rect.sizeDelta = new Vector2(360f, 360f);

            // RaceMinimap コンポーネントを付ける（Undo付きなので、元に戻せる）
            RaceMinimap minimap = Undo.AddComponent<RaceMinimap>(go);

            // コース（TrackCourse）が見つかれば、最初から Course Root に設定しておく
            // （GameObject.Find ＝ 名前で探す。非アクティブなオブジェクトは見つからない）
            GameObject course = GameObject.Find(COURSE_ROOT_NAME);

            if (course != null)
            {
                minimap.m_CourseRoot = course.transform;
            }
            else
            {
                Debug.LogWarning(
                    "RaceMinimapMenu : 「" + COURSE_ROOT_NAME + "」が見つかりませんでした。" +
                    "Minimap の Course Root に、道路のColliderが入っているオブジェクトを設定してください"
                );
            }

            // 作ったオブジェクトを選択状態にする（そのままInspectorで調整できる）
            Selection.activeGameObject = go;
        }


        // =====================================================
        // ■ 親にするオブジェクトを探す
        // =====================================================
        //
        // 右クリックしたオブジェクトがCanvasの中ならその下に、
        // そうでなければシーン内のCanvasの直下に作る
        //
        // =====================================================

        /// <summary>
        /// ミニマップを作る場所（親にするオブジェクト）を決める。
        /// </summary>
        /// <param name="context">右クリックされたオブジェクト（無ければ null）</param>
        /// <returns>親にする Transform。シーンにCanvasが1つも無ければ null</returns>
        private static Transform FindParent(GameObject context)
        {
            if (context != null)
            {
                // 右クリックされたオブジェクトが、Canvasの中（Canvas自身を含む）にあるか調べる
                Canvas contextCanvas = context.GetComponentInParent<Canvas>();

                if (contextCanvas != null)
                {
                    return context.transform;
                }
            }

            // 右クリックがCanvasの外だった時は、シーン内のCanvasを1つ探す（最初に見つかったもの）
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                return null;
            }

            // Canvasが入れ子になっている場合は、一番外側（ルート）のCanvasの直下に作る
            return canvas.rootCanvas.transform;
        }
    }
}
#endif