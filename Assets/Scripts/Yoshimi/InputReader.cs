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
            1  = WASD + Space
            2  = Arrow + Enter
            10以上 = Gamepad（10 + Gamepad.all のインデックス）
                例: 10 = 1台目のパッド, 11 = 2台目, 12 = 3台目...
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
        /// 向き入力（右スティック等）
        /// 移動とは別に向きたい方向を指定するための入力
        /// X = 左右
        /// Y = 前後
        /// </summary>
        public Vector2 LookInput
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
            LookInput = Vector2.zero;

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
            // ■ Gamepad（3台目以降にも対応）
            // =================================================
            //
            // ControlIndexが10以上ならゲームパッドとして扱い、
            // 10を引いた値をGamepad.allのインデックスとする。
            //
            // =================================================

            else if (ControlIndex >= 10)
            {
                ReadGamepadInput(ControlIndex - 10);
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
            LookInput = pad.rightStick.ReadValue();

            // 発射はR1（右肩ボタン）
            // 右スティックで向きを操作するため、
            // 親指を離さず人差し指で押せるボタンにする
            FirePressed  = pad.rightShoulder.wasPressedThisFrame;
            FireReleased = pad.rightShoulder.wasReleasedThisFrame;
            FireHeld     = pad.rightShoulder.isPressed;

            IsUsingGamepad = true;
        }
    }
}