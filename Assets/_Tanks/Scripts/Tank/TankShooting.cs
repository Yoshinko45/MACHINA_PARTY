using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankShooting : MonoBehaviour
    {
        // =====================================================
        // ■ プレイヤー番号
        // =====================================================

        [Header("Player Settings")]

        [Tooltip("1 = Player1 / 2 = Player2")]
        public int playerID = 1;


        // =====================================================
        // ■ 弾関連
        // =====================================================

        [Header("Shell")]
        public Rigidbody m_Shell;

        public Transform m_FireTransform;


        // =====================================================
        // ■ UI関連
        // =====================================================

        [Header("UI")]
        public Slider m_AimSlider;


        // =====================================================
        // ■ サウンド関連
        // =====================================================

        [Header("Audio")]
        public AudioSource m_ShootingAudio;

        public AudioClip m_ChargingClip;

        public AudioClip m_FireClip;


        // =====================================================
        // ■ 発射設定
        // =====================================================

        [Header("Launch Settings")]

        [Tooltip("最低発射速度")]
        public float m_MinLaunchForce = 5f;

        [Tooltip("最大発射速度")]
        public float m_MaxLaunchForce = 20f;

        [Tooltip("最大チャージ時間")]
        public float m_MaxChargeTime = 0.75f;

        [Tooltip("発射後クールタイム")]
        public float m_ShotCooldown = 1.0f;


        // =====================================================
        // ■ 爆発設定
        // =====================================================

        [Header("Explosion Settings")]

        [Tooltip("最大ダメージ")]
        public float m_MaxDamage = 100f;

        [Tooltip("爆発力")]
        public float m_ExplosionForce = 50f;

        [Tooltip("爆発半径")]
        public float m_ExplosionRadius = 5f;


        // =====================================================
        // ■ 内部変数
        // =====================================================

        private float m_CurrentLaunchForce;

        private float m_ChargeSpeed;

        private bool m_Fired;

        private bool m_IsCharging;

        private float m_BaseMinLaunchForce;

        private float m_ShotCooldownTimer;


        // =====================================================
        // ■ 特殊弾関連
        // =====================================================

        private bool m_HasSpecialShell;

        private float m_SpecialShellMultiplier = 1f;


        // =====================================================
        // ■ 外部参照用
        // =====================================================

        public float CurrentChargeRatio =>
            (m_CurrentLaunchForce - m_MinLaunchForce) /
            (m_MaxLaunchForce - m_MinLaunchForce);

        public bool IsCharging => m_IsCharging;

        public bool m_IsComputerControlled { get; set; } = false;


        // =====================================================
        // ■ 有効化時
        // =====================================================

        private void OnEnable()
        {
            m_CurrentLaunchForce = m_MinLaunchForce;

            m_BaseMinLaunchForce = m_MinLaunchForce;

            if (m_AimSlider != null)
            {
                m_AimSlider.minValue = m_MinLaunchForce;
                m_AimSlider.maxValue = m_MaxLaunchForce;
                m_AimSlider.value = m_MinLaunchForce;
            }

            m_HasSpecialShell = false;

            m_SpecialShellMultiplier = 1f;
        }


        // =====================================================
        // ■ Start
        // =====================================================

        private void Start()
        {
            // チャージ速度計算
            m_ChargeSpeed =
                (m_MaxLaunchForce - m_MinLaunchForce) /
                m_MaxChargeTime;
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            if (m_IsComputerControlled)
            {
                ComputerUpdate();
            }
            else
            {
                HumanUpdate();
            }
        }


        // =====================================================
        // ■ AI用：チャージ開始
        // =====================================================

        public void StartCharging()
        {
            m_Fired = false;

            m_CurrentLaunchForce = m_MinLaunchForce;

            m_IsCharging = true;

            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip = m_ChargingClip;
                m_ShootingAudio.Play();
            }
        }


        // =====================================================
        // ■ AI用：チャージ停止
        // =====================================================

        public void StopCharging()
        {
            if (m_IsCharging)
            {
                Fire();

                m_IsCharging = false;
            }
        }


        // =====================================================
        // ■ プレイヤー操作更新
        // =====================================================

        private void HumanUpdate()
        {
            // =================================================
            // 入力取得
            // =================================================

            bool firePressed = false;

            bool fireHolding = false;

            bool fireReleased = false;


            // -------------------------------------------------
            // Player1
            // Spaceキー
            // -------------------------------------------------

            if (playerID == 1)
            {
                firePressed =
                    Keyboard.current.spaceKey.wasPressedThisFrame;

                fireHolding =
                    Keyboard.current.spaceKey.isPressed;

                fireReleased =
                    Keyboard.current.spaceKey.wasReleasedThisFrame;
            }


            // -------------------------------------------------
            // Player2
            // Enterキー
            // -------------------------------------------------

            else if (playerID == 2)
            {
                firePressed =
                    Keyboard.current.enterKey.wasPressedThisFrame;

                fireHolding =
                    Keyboard.current.enterKey.isPressed;

                fireReleased =
                    Keyboard.current.enterKey.wasReleasedThisFrame;
            }


            // =================================================
            // クールタイム
            // =================================================

            if (m_ShotCooldownTimer > 0f)
            {
                m_ShotCooldownTimer -= Time.deltaTime;
            }


            // =================================================
            // UI更新
            // =================================================

            if (m_AimSlider != null)
            {
                m_AimSlider.value = m_BaseMinLaunchForce;
            }


            // =================================================
            // 最大チャージ到達
            // =================================================

            if (m_CurrentLaunchForce >= m_MaxLaunchForce &&
                !m_Fired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;

                Fire();
            }


            // =================================================
            // 押した瞬間
            // =================================================

            else if (m_ShotCooldownTimer <= 0f &&
                     firePressed)
            {
                m_Fired = false;

                m_CurrentLaunchForce = m_MinLaunchForce;

                m_IsCharging = true;

                if (m_ShootingAudio != null)
                {
                    m_ShootingAudio.clip = m_ChargingClip;
                    m_ShootingAudio.Play();
                }
            }


            // =================================================
            // 長押し中
            // =================================================

            else if (fireHolding &&
                     !m_Fired)
            {
                m_CurrentLaunchForce +=
                    m_ChargeSpeed * Time.deltaTime;

                if (m_AimSlider != null)
                {
                    m_AimSlider.value = m_CurrentLaunchForce;
                }
            }


            // =================================================
            // 離した瞬間
            // =================================================

            else if (fireReleased &&
                     !m_Fired)
            {
                Fire();
            }
        }


        // =====================================================
        // ■ CPU操作更新
        // =====================================================

        private void ComputerUpdate()
        {
            if (m_AimSlider != null)
            {
                m_AimSlider.value = m_BaseMinLaunchForce;
            }

            if (m_CurrentLaunchForce >= m_MaxLaunchForce &&
                !m_Fired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;

                Fire();
            }

            else if (m_IsCharging &&
                     !m_Fired)
            {
                m_CurrentLaunchForce +=
                    m_ChargeSpeed * Time.deltaTime;

                if (m_AimSlider != null)
                {
                    m_AimSlider.value = m_CurrentLaunchForce;
                }
            }
        }


        // =====================================================
        // ■ 発射処理
        // =====================================================

        private void Fire()
        {
            if (m_Shell == null)
            {
                Debug.LogError("m_Shell が設定されていません");
                return;
            }

            if (m_FireTransform == null)
            {
                Debug.LogError("m_FireTransform が設定されていません");
                return;
            }

            m_Fired = true;

            m_IsCharging = false;


            // =================================================
            // 砲身の少し前に生成
            // =================================================

            Vector3 spawnPos =
                m_FireTransform.position +
                m_FireTransform.forward * 2f;


            Rigidbody shellInstance =
                Instantiate(
                    m_Shell,
                    spawnPos,
                    m_FireTransform.rotation
                );


            // =================================================
            // 発射速度
            // =================================================

            shellInstance.linearVelocity =
                m_CurrentLaunchForce *
                m_FireTransform.forward;


            // =================================================
            // 爆発設定
            // =================================================

            ShellExplosion explosionData =
                shellInstance.GetComponent<ShellExplosion>();

            if (explosionData != null)
            {
                explosionData.m_ExplosionForce =
                    m_ExplosionForce;

                explosionData.m_ExplosionRadius =
                    m_ExplosionRadius;

                explosionData.m_MaxDamage =
                    m_MaxDamage;


                // =============================================
                // 強化弾
                // =============================================

                if (m_HasSpecialShell)
                {
                    explosionData.m_MaxDamage *=
                        m_SpecialShellMultiplier;

                    m_HasSpecialShell = false;

                    m_SpecialShellMultiplier = 1f;

                    PowerUpDetector powerUpDetector =
                        GetComponent<PowerUpDetector>();

                    if (powerUpDetector != null)
                    {
                        powerUpDetector.m_HasActivePowerUp = false;
                    }

                    PowerUpHUD powerUpHUD =
                        GetComponentInChildren<PowerUpHUD>();

                    if (powerUpHUD != null)
                    {
                        powerUpHUD.DisableActiveHUD();
                    }
                }
            }


            // =================================================
            // 発射音
            // =================================================

            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip = m_FireClip;
                m_ShootingAudio.Play();
            }


            // =================================================
            // リセット
            // =================================================

            m_CurrentLaunchForce = m_MinLaunchForce;

            m_ShotCooldownTimer = m_ShotCooldown;
        }


        // =====================================================
        // ■ 強化弾装備
        // =====================================================

        public void EquipSpecialShell(float damageMultiplier)
        {
            m_HasSpecialShell = true;

            m_SpecialShellMultiplier = damageMultiplier;
        }


        // =====================================================
        // ■ 着弾予測位置取得
        // =====================================================

        public Vector3 GetProjectilePosition(float chargingLevel)
        {
            if (m_FireTransform == null)
            {
                return transform.position;
            }

            float chargeLevel =
                Mathf.Lerp(
                    m_MinLaunchForce,
                    m_MaxLaunchForce,
                    chargingLevel
                );

            Vector3 velocity =
                chargeLevel *
                m_FireTransform.forward;

            float a = 0.5f * Physics.gravity.y;

            float b = velocity.y;

            float c = m_FireTransform.position.y;

            float sqrtContent =
                b * b - 4 * a * c;

            if (sqrtContent <= 0)
            {
                return m_FireTransform.position;
            }

            float answer1 =
                (-b + Mathf.Sqrt(sqrtContent)) /
                (2 * a);

            float answer2 =
                (-b - Mathf.Sqrt(sqrtContent)) /
                (2 * a);

            float answer =
                answer1 > 0 ?
                answer1 :
                answer2;

            Vector3 position =
                m_FireTransform.position +
                new Vector3(
                    velocity.x,
                    0,
                    velocity.z
                ) * answer;

            position.y = 0;

            return position;
        }
    }
}