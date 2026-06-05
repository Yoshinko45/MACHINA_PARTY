using System.Collections;
using UnityEngine;
namespace Tanks.Complete
{
    public class PowerUpDetector : MonoBehaviour
    {
        // =====================================================
        // ■ PowerUp状態
        // =====================================================

        // 現在PowerUpを持っているか
        public bool m_HasActivePowerUp = false;


        // =====================================================
        // ■ コンポーネント参照
        // =====================================================

        private TankShooting m_TankShooting;

        private TankMovement m_TankMovement;

        private TankHealth m_TankHealth;

        private PowerUpHUD m_PowerUpHUD;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // 各コンポーネント取得
            m_TankShooting =
                GetComponent<TankShooting>();

            m_TankMovement =
                GetComponent<TankMovement>();

            m_TankHealth =
                GetComponent<TankHealth>();

            m_PowerUpHUD =
                GetComponentInChildren<PowerUpHUD>();
        }


        // =====================================================
        // ■ スピード強化
        // =====================================================

        public void PowerUpSpeed(
            float speedBoost,
            float turnSpeedBoost,
            float duration)
        {
            StartCoroutine(
                IncreaseSpeed(
                    speedBoost,
                    turnSpeedBoost,
                    duration
                )
            );
        }


        // =====================================================
        // ■ スピード強化Coroutine
        // =====================================================

        private IEnumerator IncreaseSpeed(
            float speedBoost,
            float turnSpeedBoost,
            float duration)
        {
            // =================================================
            // PowerUp開始
            // =================================================

            m_HasActivePowerUp = true;


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.Speed,
                    duration
                );
            }


            // =================================================
            // スピード強化
            // =================================================

            m_TankMovement.m_Speed +=
                speedBoost;

            m_TankMovement.m_TurnSpeed +=
                turnSpeedBoost;


            // =================================================
            // 待機
            // =================================================

            yield return new WaitForSeconds(
                duration
            );


            // =================================================
            // 元に戻す
            // =================================================

            m_TankMovement.m_Speed -=
                speedBoost;

            m_TankMovement.m_TurnSpeed -=
                turnSpeedBoost;


            // =================================================
            // 終了
            // =================================================

            m_HasActivePowerUp = false;
        }


        // =====================================================
        // ■ 連射速度強化
        // =====================================================

        public void PowerUpShoootingRate(
            float cooldownReduction,
            float duration)
        {
            StartCoroutine(
                IncreaseShootingRate(
                    cooldownReduction,
                    duration
                )
            );
        }


        // =====================================================
        // ■ 連射速度強化Coroutine
        // =====================================================

        private IEnumerator IncreaseShootingRate(
            float cooldownReduction,
            float duration)
        {
            // 無効値防止
            if (cooldownReduction <= 0f)
            {
                yield break;
            }


            // =================================================
            // PowerUp開始
            // =================================================

            m_HasActivePowerUp = true;


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.ShootingBonus,
                    duration
                );
            }


            // =================================================
            // クールタイム短縮
            // =================================================

            m_TankShooting.m_ShotCooldown *=
                cooldownReduction;


            // =================================================
            // 待機
            // =================================================

            yield return new WaitForSeconds(
                duration
            );


            // =================================================
            // 元に戻す
            // =================================================

            m_TankShooting.m_ShotCooldown /=
                cooldownReduction;


            // =================================================
            // 終了
            // =================================================

            m_HasActivePowerUp = false;
        }


        // =====================================================
        // ■ シールド取得
        // =====================================================

        public void PickUpShield(
            float shieldAmount,
            float duration)
        {
            // 既にシールド中なら無効
            if (!m_TankHealth.m_HasShield)
            {
                StartCoroutine(
                    ActivateShield(
                        shieldAmount,
                        duration
                    )
                );
            }
        }


        // =====================================================
        // ■ シールドCoroutine
        // =====================================================

        private IEnumerator ActivateShield(
            float shieldAmount,
            float duration)
        {
            // =================================================
            // PowerUp開始
            // =================================================

            m_HasActivePowerUp = true;


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.DamageReduction,
                    duration
                );
            }


            // =================================================
            // シールドON
            // =================================================

            m_TankHealth.SetShield(
                true,
                shieldAmount
            );


            // =================================================
            // 待機
            // =================================================

            yield return new WaitForSeconds(
                duration
            );


            // =================================================
            // シールドOFF
            // =================================================

            m_TankHealth.SetShield(false);


            // =================================================
            // 終了
            // =================================================

            m_HasActivePowerUp = false;
        }


        // =====================================================
        // ■ 回復
        // =====================================================

        public void PowerUpHealing(
            float healAmount)
        {
            // HP回復
            m_TankHealth.IncreaseHealth(
                healAmount
            );


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.Healing,
                    1.0f
                );
            }
        }


        // =====================================================
        // ■ 無敵化
        // =====================================================

        public void PowerUpInvincibility(
            float duration)
        {
            StartCoroutine(
                ActivateInvincibility(
                    duration
                )
            );
        }


        // =====================================================
        // ■ 無敵Coroutine
        // =====================================================

        private IEnumerator ActivateInvincibility(
            float duration)
        {
            // =================================================
            // PowerUp開始
            // =================================================

            m_HasActivePowerUp = true;


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.Invincibility,
                    duration
                );
            }


            // =================================================
            // 無敵ON
            // =================================================

            m_TankHealth.SetInvincible(true);


            // =================================================
            // 待機
            // =================================================

            yield return new WaitForSeconds(
                duration
            );


            // =================================================
            // 無敵OFF
            // =================================================

            m_TankHealth.SetInvincible(false);


            // =================================================
            // 終了
            // =================================================

            m_HasActivePowerUp = false;
        }


        // =====================================================
        // ■ 強化弾
        // =====================================================

        public void PowerUpSpecialShell(
            float damageMultiplier)
        {
            // =================================================
            // PowerUp開始
            // =================================================

            m_HasActivePowerUp = true;


            // HUD表示
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.SetActivePowerUp(
                    PowerUp.PowerUpType.DamageMultiplier,
                    0f
                );
            }


            // =================================================
            // 強化弾装備
            // =================================================

            m_TankShooting.EquipSpecialShell(
                damageMultiplier
            );
        }
    }
}