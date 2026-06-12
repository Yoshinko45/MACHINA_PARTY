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
        // ■ 入力モード
        // =====================================================
        // ゲームモードによって入力の解釈を切り替える
        //
        // TankBattle
        //     通常の戦車モード
        //     X = 回転
        //     Y = 前進 / 後退
        //
        // Race
        //     レースモード
        //     X = ハンドル
        //     Y = アクセル / バック
        //
        public enum InputProfile
        {
            TankBattle, // タンクvsタンクモード
            Race        // レースモード
        }

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

        [Header("Input Profile")]

        // 現在使用する入力モード
        public InputProfile m_InputProfile =
            InputProfile.TankBattle;

        // レースモード用ゲームパッドデッドゾーン
        //
        // スティックは触っていなくても
        // 微妙な入力値が入る場合があるため、
        // この値以下は0として扱う
        [Range(0f, 0.5f)]
        public float m_GamepadRaceDeadZone = 0.15f;


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

            FirePressed = false;
            FireHeld = false;
            FireReleased = false;

            IsUsingGamepad = false;

            // =================================================
            // レースモード判定
            // =================================================
            //
            // TankBattle と Race では
            // 入力の意味が異なるため、
            // レースモードの場合は専用処理へ分岐する
            //
            if (m_InputProfile == InputProfile.Race)
            {
                // レースモードの入力処理は専用関数でまとめる
                ReadRaceInput();
                return;
            }


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
        // ■ レースモード入力
        // =====================================================
        //
        // ControlIndexごとに
        // レース専用入力処理へ振り分ける
        //
        // 1 = WASD
        // 2 = Arrow
        // 3 = Gamepad1
        // 4 = Gamepad2
        //
        private void ReadRaceInput()
        {
            if (ControlIndex == 1)
            {
                ReadRaceKeyboardInput(
                    useWASD: true,
                    useArrows: false,
                    usesEnterFire: false
                );
            }
            else if (ControlIndex == 2)
            {
                ReadRaceKeyboardInput(
                    useWASD: false,
                    useArrows: true,
                    usesEnterFire: true
                );
            }
            else if (ControlIndex == 3)
            {
                ReadRaceGamepadInput(0);
            }
            else if (ControlIndex == 4)
            {
                ReadRaceGamepadInput(1);
            }
            else
            {
                Debug.LogWarning(
                    $"Unsupported race ControlIndex : {ControlIndex}"
                );
            }
        }

        // =====================================================
        // ■ レース用キーボード入力
        // =====================================================
        //
        // steering
        //     左右ハンドル入力
        //
        // throttle
        //     前進・後退入力
        //
        // MoveInput.x = ハンドル
        // MoveInput.y = アクセル / バック
        //
        private void ReadRaceKeyboardInput(
            bool useWASD,
            bool useArrows,
            bool usesEnterFire)
        {
            if (Keyboard.current == null)
            {
                return;
            }

            float steering = 0f;
            float throttle = 0f;

            // =================================================
            // WASD操作
            // =================================================
            //
            // A = 左
            // D = 右
            // W = アクセル
            // S = バック
            //
            if (useWASD)
            {
                if (Keyboard.current.aKey.isPressed)
                {
                    steering -= 1f;
                }

                if (Keyboard.current.dKey.isPressed)
                {
                    steering += 1f;
                }

                if (Keyboard.current.wKey.isPressed)
                {
                    throttle += 1f;
                }

                if (Keyboard.current.sKey.isPressed)
                {
                    throttle -= 1f;
                }
            }

            // =================================================
            // 矢印キー操作
            // =================================================
            //
            // ← = 左
            // → = 右
            // ↑ = アクセル
            // ↓ = バック
            //
            if (useArrows)
            {
                if (Keyboard.current.leftArrowKey.isPressed)
                {
                    steering -= 1f;
                }

                if (Keyboard.current.rightArrowKey.isPressed)
                {
                    steering += 1f;
                }

                if (Keyboard.current.upArrowKey.isPressed)
                {
                    throttle += 1f;
                }

                if (Keyboard.current.downArrowKey.isPressed)
                {
                    throttle -= 1f;
                }
            }

            // =================================================
            // レースモード用入力生成
            // =================================================
            //
            // X = ハンドル
            // Y = アクセル / バック
            //
            MoveInput =
                new Vector2(
                    Mathf.Clamp(steering, -1f, 1f),
                    Mathf.Clamp(throttle, -1f, 1f)
                );

            var fireKey = usesEnterFire
                ? Keyboard.current.enterKey
                : Keyboard.current.spaceKey;

            FirePressed =
                fireKey.wasPressedThisFrame;

            FireReleased =
                fireKey.wasReleasedThisFrame;

            FireHeld =
                fireKey.isPressed;
        }

        // =====================================================
        // ■ レース用ゲームパッド入力
        // =====================================================
        //
        // 左スティック左右
        //      ハンドル
        //
        // RT
        //      アクセル
        //
        // 左スティック下
        //      バック
        //
        // Aボタン
        //      モード固有アクション
        //
        private void ReadRaceGamepadInput(int index)
        {
            if (Gamepad.all.Count <= index)
            {
                return;
            }

            Gamepad pad =
                Gamepad.all[index];

            if (pad == null)
            {
                return;
            }

            // =================================================
            // 左スティック取得
            // =================================================
            //
            // X = ハンドル
            // Y = 前後入力
            //
            Vector2 stick =
                pad.leftStick.ReadValue();

            // =================================================
            // ハンドル入力
            // =================================================
            //
            // 左スティック左右をそのまま使用
            //
            float steering =
                ApplyRaceDeadZone(stick.x);

            // =================================================
            // バック入力
            // =================================================
            //
            // 左スティック下方向のみ使用
            //
            // 上方向はアクセルではなく
            // RTに割り当てるため無効
            //
            float reverse =
                Mathf.Min(0f, ApplyRaceDeadZone(stick.y));

            // =================================================
            // アクセル入力
            // =================================================
            //
            // 基本はRTで加速
            //
            float accelerator =
                ApplyRaceDeadZone(
                    pad.rightTrigger.ReadValue()
                );

            // =================================================
            // 補助アクセル
            // =================================================
            //
            // RTが無いコントローラーや
            // テストプレイ用
            //
            // RB または Bボタンで
            // 最大加速
            //
            if (pad.rightShoulder.isPressed ||
                pad.buttonEast.isPressed)
            {
                accelerator = 1f;
            }

            // =================================================
            // レースモード入力生成
            // =================================================
            //
            // X = ハンドル
            //
            // Y =
            //     RT入力がある → アクセル
            //     RT入力が無い → バック入力
            //
            MoveInput =
                new Vector2(
                    steering,
                    accelerator > 0f
                        ? accelerator
                        : reverse
                );

            FirePressed =
                pad.buttonSouth.wasPressedThisFrame;

            FireReleased =
                pad.buttonSouth.wasReleasedThisFrame;

            FireHeld =
                pad.buttonSouth.isPressed;

            IsUsingGamepad = true;
        }

        // =====================================================
        // ■ デッドゾーン適用
        // =====================================================
        //
        // スティックの微小入力を無視する
        //
        // 例：
        //     0.03
        //     -0.08
        //
        // などを0に変換
        //
        private float ApplyRaceDeadZone(float value)
        {
            if (Mathf.Abs(value) < m_GamepadRaceDeadZone)
            {
                return 0f;
            }

            return Mathf.Clamp(value, -1f, 1f);
        }

        // wasdや矢印キーのレースモード入力処理は共通化できるため、専用関数でまとめる
        private void ReadWASDInput()
        {
            // キーボード未接続
            if (Keyboard.current == null)
            {
                return;
            }

            float x = 0f;
            float y = 0f;

            // =================================================
            // 左右入力
            // =================================================

            if (Keyboard.current.aKey.isPressed)
            {
                x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                x += 1f;
            }


            // =================================================
            // 前後入力
            // =================================================

            if (Keyboard.current.wKey.isPressed)
            {
                y += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                y -= 1f;
            }


            // =================================================
            // 斜め移動速度補正
            // =================================================

            MoveInput =
                new Vector2(x, y).normalized;


            // =================================================
            // 発射入力
            // =================================================

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


        // =====================================================
        // ■ 矢印キー入力
        // =====================================================

        private void ReadArrowInput()
        {
            // キーボード未接続
            if (Keyboard.current == null)
            {
                return;
            }


            float x = 0f;
            float y = 0f;


            // =================================================
            // 左右入力
            // =================================================

            if (Keyboard.current.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                x += 1f;
            }


            // =================================================
            // 前後入力
            // =================================================

            if (Keyboard.current.upArrowKey.isPressed)
            {
                y += 1f;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                y -= 1f;
            }


            // =================================================
            // 斜め移動補正
            // =================================================

            MoveInput =
                new Vector2(x, y).normalized;


            // =================================================
            // 発射入力
            // =================================================

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


        // =====================================================
        // ■ ゲームパッド入力
        // =====================================================

        /// <summary>
        /// 指定番号のゲームパッド入力取得
        /// </summary>
        /// <param name="index">
        /// Gamepad番号
        /// </param>

        private void ReadGamepadInput(int index)
        {
            // =================================================
            // 指定番号が存在するか
            // =================================================

            if (Gamepad.all.Count <= index)
            {
                return;
            }


            // =================================================
            // Gamepad取得
            // =================================================

            Gamepad pad =
                Gamepad.all[index];


            // Gamepad無効
            if (pad == null)
            {
                return;
            }


            // =================================================
            // スティック入力
            // =================================================

            MoveInput =
                pad.leftStick.ReadValue();


            // =================================================
            // 発射入力
            // =================================================

            FirePressed =
                pad.buttonSouth
                .wasPressedThisFrame;

            FireReleased =
                pad.buttonSouth
                .wasReleasedThisFrame;

            FireHeld =
                pad.buttonSouth
                .isPressed;


            // =================================================
            // ゲームパッド使用中
            // =================================================

            IsUsingGamepad = true;
        }
    }
}
