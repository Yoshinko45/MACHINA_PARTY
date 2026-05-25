using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    public class InputReader : MonoBehaviour
    {
        // =====================================================
        // ■ PlayerInput
        // =====================================================

        private PlayerInput m_PlayerInput;


        // =====================================================
        // ■ Control Type
        // =====================================================

        [Header("Control Type")]

        // 1 = WASD
        // 2 = Arrow
        // 3 = Gamepad1
        // 4 = Gamepad2
        public int ControlIndex = 1;


        // =====================================================
        // ■ 入力値
        // =====================================================

        public Vector2 MoveInput
        {
            get;
            private set;
        }

        public bool FirePressed
        {
            get;
            private set;
        }

        public bool FireHeld
        {
            get;
            private set;
        }

        public bool FireReleased
        {
            get;
            private set;
        }


        // =====================================================
        // ■ デバイス判定
        // =====================================================

        public bool IsUsingGamepad
        {
            get;
            private set;
        }


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            m_PlayerInput =
                GetComponent<PlayerInput>();


            // 無くても今回は許可
            if (m_PlayerInput == null)
            {
                Debug.LogWarning(
                    gameObject.name +
                    " に PlayerInput がありません"
                );
            }
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            // 初期化
            MoveInput = Vector2.zero;

            FirePressed = false;
            FireHeld = false;
            FireReleased = false;

            IsUsingGamepad = false;


            // =================================================
            // WASD
            // =================================================

            if (ControlIndex == 1)
            {
                float x = 0f;
                float y = 0f;

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed)
                        x = -1f;

                    if (Keyboard.current.dKey.isPressed)
                        x = 1f;

                    if (Keyboard.current.wKey.isPressed)
                        y = 1f;

                    if (Keyboard.current.sKey.isPressed)
                        y = -1f;


                    MoveInput =
                        new Vector2(x, y);


                    FirePressed =
                        Keyboard.current.spaceKey
                        .wasPressedThisFrame;

                    FireReleased =
                        Keyboard.current.spaceKey
                        .wasReleasedThisFrame;

                    FireHeld =
                        Keyboard.current.spaceKey
                        .isPressed;
                }
            }


            // =================================================
            // 矢印キー
            // =================================================

            else if (ControlIndex == 2)
            {
                float x = 0f;
                float y = 0f;

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftArrowKey.isPressed)
                        x = -1f;

                    if (Keyboard.current.rightArrowKey.isPressed)
                        x = 1f;

                    if (Keyboard.current.upArrowKey.isPressed)
                        y = 1f;

                    if (Keyboard.current.downArrowKey.isPressed)
                        y = -1f;


                    MoveInput =
                        new Vector2(x, y);


                    FirePressed =
                        Keyboard.current.enterKey
                        .wasPressedThisFrame;

                    FireReleased =
                        Keyboard.current.enterKey
                        .wasReleasedThisFrame;

                    FireHeld =
                        Keyboard.current.enterKey
                        .isPressed;
                }
            }


            // =================================================
            // Gamepad1
            // =================================================

            else if (ControlIndex == 3)
            {
                if (Gamepad.all.Count > 0)
                {
                    Gamepad pad =
                        Gamepad.all[0];

                    MoveInput =
                        pad.leftStick.ReadValue();

                    FirePressed =
                        pad.buttonSouth
                        .wasPressedThisFrame;

                    FireReleased =
                        pad.buttonSouth
                        .wasReleasedThisFrame;

                    FireHeld =
                        pad.buttonSouth
                        .isPressed;

                    IsUsingGamepad = true;
                }
            }


            // =================================================
            // Gamepad2
            // =================================================

            else if (ControlIndex == 4)
            {
                if (Gamepad.all.Count > 1)
                {
                    Gamepad pad =
                        Gamepad.all[1];

                    MoveInput =
                        pad.leftStick.ReadValue();

                    FirePressed =
                        pad.buttonSouth
                        .wasPressedThisFrame;

                    FireReleased =
                        pad.buttonSouth
                        .wasReleasedThisFrame;

                    FireHeld =
                        pad.buttonSouth
                        .isPressed;

                    IsUsingGamepad = true;
                }
            }
        }


        // =====================================================
        // ■ LateUpdate
        // =====================================================

        private void LateUpdate()
        {
            FirePressed = false;

            FireReleased = false;
        }
    }
}