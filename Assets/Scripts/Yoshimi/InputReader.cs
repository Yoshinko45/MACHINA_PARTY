using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    // =====================================================
    // ■ 他スクリプトより先に入力を取得する
    // =====================================================
    // TankMovement より早く実行し、
    // 1フレーム遅延入力を防ぐ
    [DefaultExecutionOrder(-100)]

    public class InputReader : MonoBehaviour
    {
        // =====================================================
        // ■ 操作タイプ
        // =====================================================

        [Header("Control Type")]

        /*
            1 = WASD + Space
            2 = Arrow + Enter
            3 = Gamepad1
            4 = Gamepad2
        */

        public int ControlIndex = 1;


        // =====================================================
        // ■ 入力値
        // =====================================================

        /// <summary>
        /// 移動入力
        /// X = 左右
        /// Y = 前後
        /// </summary>
        public Vector2 MoveInput
        {
            get;
            private set;
        }


        /// <summary>
        /// ボタンを押した瞬間
        /// </summary>
        public bool FirePressed
        {
            get;
            private set;
        }


        /// <summary>
        /// ボタンを押している間
        /// </summary>
        public bool FireHeld
        {
            get;
            private set;
        }


        /// <summary>
        /// ボタンを離した瞬間
        /// </summary>
        public bool FireReleased
        {
            get;
            private set;
        }


        // =====================================================
        // ■ 使用デバイス判定
        // =====================================================

        /// <summary>
        /// 現在ゲームパッド操作中か
        /// </summary>
        public bool IsUsingGamepad
        {
            get;
            private set;
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            // =================================================
            // 毎フレーム初期化
            // =================================================

            MoveInput = Vector2.zero;

            FirePressed  = false;
            FireHeld     = false;
            FireReleased = false;

            IsUsingGamepad = false;


            // =================================================
            // ■ WASD操作
            // =================================================

            if (ControlIndex == 1)
            {
                ReadWASDInput();
            }


            // =================================================
            // ■ 矢印キー操作
            // =================================================

            else if (ControlIndex == 2)
            {
                ReadArrowInput();
            }


            // =================================================
            // ■ Gamepad1
            // =================================================

            else if (ControlIndex == 3)
            {
                ReadGamepadInput(0);
            }


            // =================================================
            // ■ Gamepad2
            // =================================================

            else if (ControlIndex == 4)
            {
                ReadGamepadInput(1);
            }


            // =================================================
            // ■ 未対応番号
            // =================================================

            else
            {
                Debug.LogWarning(
                    $"未対応の ControlIndex : {ControlIndex}"
                );
            }
        }


        // =====================================================
        // ■ WASD入力（TankBattleモード）
        // =====================================================

        private void ReadWASDInput()
        {
            if (Keyboard.current == null)
                return;

            float x = 0f;
            float y = 0f;

            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;

            MoveInput = new Vector2(x, y).normalized;

            FirePressed  = Keyboard.current.spaceKey.wasPressedThisFrame;
            FireReleased = Keyboard.current.spaceKey.wasReleasedThisFrame;
            FireHeld     = Keyboard.current.spaceKey.isPressed;
        }


        // =====================================================
        // ■ 矢印キー入力（TankBattleモード）
        // =====================================================

        private void ReadArrowInput()
        {
            if (Keyboard.current == null)
                return;

            float x = 0f;
            float y = 0f;

            if (Keyboard.current.leftArrowKey.isPressed)  x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) x += 1f;
            if (Keyboard.current.upArrowKey.isPressed)    y += 1f;
            if (Keyboard.current.downArrowKey.isPressed)  y -= 1f;

            MoveInput = new Vector2(x, y).normalized;

            FirePressed  = Keyboard.current.enterKey.wasPressedThisFrame;
            FireReleased = Keyboard.current.enterKey.wasReleasedThisFrame;
            FireHeld     = Keyboard.current.enterKey.isPressed;
        }


        // =====================================================
        // ■ ゲームパッド入力（TankBattleモード）
        // =====================================================

        private void ReadGamepadInput(int index)
        {
            if (Gamepad.all.Count <= index)
                return;

            Gamepad pad = Gamepad.all[index];

            if (pad == null)
                return;

            MoveInput = pad.leftStick.ReadValue();

            FirePressed  = pad.buttonSouth.wasPressedThisFrame;
            FireReleased = pad.buttonSouth.wasReleasedThisFrame;
            FireHeld     = pad.buttonSouth.isPressed;

            IsUsingGamepad = true;
        }
    }
}