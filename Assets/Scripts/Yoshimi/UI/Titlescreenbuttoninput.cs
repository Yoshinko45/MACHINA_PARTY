using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // =====================================================
    // ■ タイトル画面ボタンのゲームパッド/キーボード対応
    // =====================================================
    //
    // TankSelect_Buttonなど、タイトル画面でクリックする
    // ボタンと同じGameObjectに付けて使うスクリプト。
    //
    // このボタン自体はまだ「Menus」が非アクティブな段階で
    // 押される想定なので、GameUIHandler側のUpdate()に
    // 参加入力の監視をまとめることができない
    // （Menusが非アクティブ＝GameUIHandlerもまだ動いていない）。
    // そのためこのスクリプトを別途、常時アクティブな
    // TitleCanvas側に置いて解決する。
    //
    // WASD(Space) / 矢印キー(Enter) / 各ゲームパッド(南ボタン)
    // のいずれかが押された瞬間、このボタンのOnClickを
    // そのまま呼び出す。Inspectorで設定済みの処理
    // （Menusを有効化 / TitleCanvasを無効化など）を
    // そのまま再利用するので、OnClickの中身が変わっても
    // このスクリプト側の修正は不要。
    //
    // =====================================================

    [RequireComponent(typeof(Button))]
    public class TitleScreenButtonInput : MonoBehaviour
    {
        // 対象ボタン
        private Button m_Button;


        // =================================================
        // ■ Awake
        // =================================================

        private void Awake()
        {
            m_Button =
                GetComponent<Button>();
        }


        // =================================================
        // ■ Update
        // =================================================

        private void Update()
        {
            // =============================================
            // ボタンが押せない状態なら何もしない
            // =============================================

            if (!m_Button.interactable)
            {
                return;
            }


            // =============================================
            // いずれかのデバイスの決定ボタンが
            // 押されたか判定
            // =============================================

            bool pressed = false;

            // WASD勢（Space）
            if (Keyboard.current != null &&
                Keyboard.current.spaceKey
                    .wasPressedThisFrame)
            {
                pressed = true;
            }

            // 矢印キー勢（Enter）
            if (Keyboard.current != null &&
                Keyboard.current.enterKey
                    .wasPressedThisFrame)
            {
                pressed = true;
            }

            // 各ゲームパッド（南ボタン）
            for (int i = 0;
                 i < Gamepad.all.Count;
                 ++i)
            {
                if (Gamepad.all[i].buttonSouth
                    .wasPressedThisFrame)
                {
                    pressed = true;
                    break;
                }
            }


            // =============================================
            // 押されていたらボタンのOnClickをそのまま実行
            // =============================================

            if (pressed)
            {
                m_Button.onClick.Invoke();
            }
        }
    }
}