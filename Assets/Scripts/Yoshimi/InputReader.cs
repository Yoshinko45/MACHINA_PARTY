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
            0  = 自動判定（WASD／矢印キー／ゲームパッド1台目のうち、
                 実際に触っているものがそのまま反映される）
                 例：1人でのテストプレイ中に、キーボードと
                 ゲームパッドをInspectorをいじらずその場で
                 切り替えたい時に使う
            1  = WASD + Space
            2  = Arrow + Enter
            10以上 = Gamepad（10 + Gamepad.all のインデックス）
                例: 10 = 1台目のパッド, 11 = 2台目, 12 = 3台目...

            ※複数人の参加（GameUIHandlerの参加画面）は
              これまで通り1／2／10以上を使うので、0とは無関係
        */

        public int ControlIndex = 1;


        // =====================================================
        // ■ 入力のロック
        // =====================================================

        /// <summary>
        /// true の間は、どの装置の入力も読まず、全部ニュートラル（0／押していない）にする。
        /// レースでゴールした後など、操作だけ止めたい時に使う。
        /// （ControlIndex を無効な番号にする方法と違って、警告が毎フレーム出ない）
        /// </summary>
        public bool InputLocked
        {
            get;
            set;
        }


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
        // ■ レースモード（車）用の入力
        // =====================================================
        //
        // CarMovement / RaceCameraFollow から参照される。
        // キーボード（WASD/矢印）は既存のMoveInputをそのまま使うが、
        // ゲームパッドだけはY軸をスティックではなくボタン式
        // （東=アクセル／南=ブレーキ・バック）に上書きする。
        //
        // =====================================================

        /// <summary>
        /// 車の移動入力（マリカー風）
        /// X = ハンドル（左右）
        /// Y = アクセル/ブレーキ（前後）
        /// </summary>
        public Vector2 CarMoveInput
        {
            get;
            private set;
        }

        /// <summary>
        /// ドリフトボタンを押している間
        /// （WASD＝左Shift／矢印キー＝右Shift／ゲームパッド＝Rボタン（右肩ボタン））
        /// </summary>
        public bool DriftHeld
        {
            get;
            private set;
        }

        /// <summary>
        /// 後方確認ボタンを押している間
        /// （ゲームパッド＝X or Yボタン／WASD＝Cキー／矢印キー＝右Ctrl）
        /// </summary>
        public bool LookBackHeld
        {
            get;
            private set;
        }

        /// <summary>
        /// 後方確認ボタンを押した瞬間
        /// （ゲームパッド＝X or Yボタン／WASD＝Cキー／矢印キー＝右Ctrl）
        /// </summary>
        public bool LookBackPressed
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

            // レースモード用（車を操作していないデバイスでは常にニュートラル）
            CarMoveInput    = Vector2.zero;
            DriftHeld       = false;
            LookBackHeld    = false;
            LookBackPressed = false;


            // =================================================
            // ■ ロック中（ゴール後など）は、何も読まずニュートラルのまま
            // =================================================

            if (InputLocked)
            {
                return;
            }


            // =================================================
            // ■ 自動判定（WASD／矢印キー／ゲームパッド1台目）
            // =================================================

            if (ControlIndex == 0)
            {
                ReadAnyInput();
            }


            // =================================================
            // ■ WASD操作
            // =================================================

            else if (ControlIndex == 1)
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
        // ■ WASD入力（Tank/Car共通）
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

            // レースモード用：ハンドル/アクセル/ブレーキはMoveInputをそのまま流用
            CarMoveInput = MoveInput;

            // ドリフトは左Shift
            DriftHeld = Keyboard.current.leftShiftKey.isPressed;

            // 後方確認はCキー
            LookBackHeld    = Keyboard.current.cKey.isPressed;
            LookBackPressed = Keyboard.current.cKey.wasPressedThisFrame;
        }


        // =====================================================
        // ■ 矢印キー入力（Tank/Car共通）
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

            // レースモード用：ハンドル/アクセル/ブレーキはMoveInputをそのまま流用
            // （これが無かったため、矢印キーでは車が動かなかった）
            CarMoveInput = MoveInput;

            // ドリフトは右Shift
            DriftHeld = Keyboard.current.rightShiftKey.isPressed;

            // 後方確認は右Ctrl
            LookBackHeld    = Keyboard.current.rightCtrlKey.isPressed;
            LookBackPressed = Keyboard.current.rightCtrlKey.wasPressedThisFrame;
        }


        // =====================================================
        // ■ ゲームパッド入力（Tank/Car共通）
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


            // =================================================
            // レースモード用（マリカー風ボタン配置）
            // =================================================
            //
            // 左スティックはハンドル（左右）専用にし、
            // 前後はスティックではなくボタン式にする。
            //
            // ボタンは位置（東/南）で判定しているため、
            // 実機での表記は機種ごとに以下のようになる：
            //   東（右） … Xbox=B／PlayStation=○／Switch=A → アクセル
            //   南（下） … Xbox=A／PlayStation=×／Switch=B → ブレーキ・バック
            //
            // =================================================

            bool accelHeld = pad.buttonEast.isPressed;
            bool brakeHeld = pad.buttonSouth.isPressed;

            float carY = accelHeld ? 1f : (brakeHeld ? -1f : 0f);
            CarMoveInput = new Vector2(MoveInput.x, carY);

            // ドリフトはRボタン（右肩ボタン）
            DriftHeld = pad.rightShoulder.isPressed;

            // 後方確認はXボタン or Yボタン（位置でいう北・西のどちらでも反応）
            LookBackHeld    = pad.buttonNorth.isPressed          || pad.buttonWest.isPressed;
            LookBackPressed = pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
        }


        // =====================================================
        // ■ 自動判定入力（WASD／矢印キー／ゲームパッド1台目）
        // =====================================================
        //
        // ControlIndex = 0 の専用モード。
        // 「このデバイスに固定」ではなく、WASD・矢印キー・
        // ゲームパッド1台目のうち、実際に触っているものを
        // そのまま合成して反映する（同時に何も触ってなければ
        // ニュートラルのまま）。
        //
        // レースモードのようにまだ1人しか操作しないシーンで、
        // キーボードとゲームパッドをInspectorをいじらずに
        // その場で切り替えてテストしたい時のためのモード。
        //
        // ※複数人の参加（GameUIHandlerの参加画面）は各デバイスに
        // 　固定の番号（1／2／10以上）を割り当てる方式のままなので、
        // 　このモードとは無関係（GameUIHandlerが0を割り当てることはない）。
        //
        // =====================================================

        private void ReadAnyInput()
        {
            // ── WASD分 ────────────────────────────────────
            float wasdX = 0f, wasdY = 0f;
            bool wasdFirePressed = false, wasdFireHeld = false, wasdFireReleased = false;
            bool wasdDrift = false;
            bool wasdLookBackHeld = false, wasdLookBackPressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) wasdX -= 1f;
                if (Keyboard.current.dKey.isPressed) wasdX += 1f;
                if (Keyboard.current.wKey.isPressed) wasdY += 1f;
                if (Keyboard.current.sKey.isPressed) wasdY -= 1f;

                wasdFirePressed  = Keyboard.current.spaceKey.wasPressedThisFrame;
                wasdFireReleased = Keyboard.current.spaceKey.wasReleasedThisFrame;
                wasdFireHeld     = Keyboard.current.spaceKey.isPressed;

                wasdDrift = Keyboard.current.leftShiftKey.isPressed;

                wasdLookBackHeld    = Keyboard.current.cKey.isPressed;
                wasdLookBackPressed = Keyboard.current.cKey.wasPressedThisFrame;
            }

            // ── 矢印キー分 ──────────────────────────────────
            float arrowX = 0f, arrowY = 0f;
            bool arrowFirePressed = false, arrowFireHeld = false, arrowFireReleased = false;
            bool arrowDrift = false;
            bool arrowLookBackHeld = false, arrowLookBackPressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed)  arrowX -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed) arrowX += 1f;
                if (Keyboard.current.upArrowKey.isPressed)    arrowY += 1f;
                if (Keyboard.current.downArrowKey.isPressed)  arrowY -= 1f;

                arrowFirePressed  = Keyboard.current.enterKey.wasPressedThisFrame;
                arrowFireReleased = Keyboard.current.enterKey.wasReleasedThisFrame;
                arrowFireHeld     = Keyboard.current.enterKey.isPressed;

                arrowDrift           = Keyboard.current.rightShiftKey.isPressed;
                arrowLookBackHeld    = Keyboard.current.rightCtrlKey.isPressed;
                arrowLookBackPressed = Keyboard.current.rightCtrlKey.wasPressedThisFrame;
            }

            // ── ゲームパッド分（1台目のみ） ───────────────────
            // ※自動判定モードは1人プレイ想定のため、合成対象は1台目に限定する
            Vector2 padMove = Vector2.zero;
            Vector2 padLook = Vector2.zero;
            bool padFirePressed = false, padFireHeld = false, padFireReleased = false;
            float padCarY = 0f;
            bool padDrift = false;
            bool padLookBackHeld = false, padLookBackPressed = false;
            bool padExists = false;

            if (Gamepad.all.Count > 0 && Gamepad.all[0] != null)
            {
                Gamepad pad = Gamepad.all[0];
                padExists = true;

                padMove = pad.leftStick.ReadValue();
                padLook = pad.rightStick.ReadValue();

                padFirePressed  = pad.rightShoulder.wasPressedThisFrame;
                padFireReleased = pad.rightShoulder.wasReleasedThisFrame;
                padFireHeld     = pad.rightShoulder.isPressed;

                bool accelHeld = pad.buttonEast.isPressed;
                bool brakeHeld = pad.buttonSouth.isPressed;
                padCarY = accelHeld ? 1f : (brakeHeld ? -1f : 0f);

                padDrift = pad.rightShoulder.isPressed;

                padLookBackHeld    = pad.buttonNorth.isPressed           || pad.buttonWest.isPressed;
                padLookBackPressed = pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
            }

            // ── 合成 ──────────────────────────────────────
            // 同時に複数のデバイスを触ることは基本無い前提なので、
            // 単純に足し合わせるだけで「触っている方がそのまま反映される」
            float mergedX = wasdX + arrowX;
            float mergedY = wasdY + arrowY;

            MoveInput = new Vector2(mergedX + padMove.x, mergedY + padMove.y);
            LookInput = padLook;

            FirePressed  = wasdFirePressed  || arrowFirePressed  || padFirePressed;
            FireHeld     = wasdFireHeld     || arrowFireHeld     || padFireHeld;
            FireReleased = wasdFireReleased || arrowFireReleased || padFireReleased;

            // ゲームパッドに今実際に触れている間だけtrueにする
            // （繋いだだけでキーボードを使っている間はfalseのまま＝
            // 　TankMovement側のカメラ相対操作などが誤爆しないように）
            IsUsingGamepad =
                padExists &&
                (padMove.sqrMagnitude > 0.01f ||
                 padLook.sqrMagnitude > 0.01f ||
                 padFireHeld || padDrift || padLookBackHeld ||
                 Mathf.Abs(padCarY) > 0.01f);

            // レースモード用（キーボードはMoveInputと同じ値、
            // ゲームパッドだけY軸をボタン式にする点はReadGamepadInputと同じ）
            CarMoveInput = new Vector2(mergedX + padMove.x, mergedY + padCarY);

            DriftHeld       = wasdDrift || arrowDrift || padDrift;
            LookBackHeld    = padLookBackHeld    || wasdLookBackHeld    || arrowLookBackHeld;
            LookBackPressed = padLookBackPressed || wasdLookBackPressed || arrowLookBackPressed;
        }
    }
}