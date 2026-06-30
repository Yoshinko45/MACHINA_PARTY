using Fusion;
using UnityEngine;
using static Unity.Collections.Unicode;

namespace Tanks.Complete
{
    public class Tank_Health : NetworkBehaviour
    {
        [Header("Health")]
        [SerializeField] float startingHealth = 100f;

        [Header("Shield")]
        [SerializeField] float shieldReduction = 0.5f;

        [Header("Invincible")]
        [SerializeField] float invincibleTime = 1.5f;

        [Networked] public float Health { get; set; }
        [Networked] public NetworkBool Dead { get; set; }
        [Networked] public NetworkBool HasShield { get; set; }
        [Networked] public NetworkBool Invincible { get; set; }
        [Networked] public NetworkBool DamageEnabled { get; set; }

        [Networked]
        TickTimer InvincibleTimer { get; set; }

        public float MaxHealth => startingHealth;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                Health = startingHealth;
                Dead = false;
                HasShield = false;
                Invincible = false;
                DamageEnabled = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;

            if (Invincible &&
                InvincibleTimer.Expired(Runner))
            {
                Invincible = false;
            }
        }

        public void TakeDamage(float damage)
        {
            if (!HasStateAuthority)
                return;

            if (!DamageEnabled)
                return;

            if (Dead || Invincible)
                return;

            if (HasShield)
                damage *= 1f - shieldReduction;

            Health = Mathf.Max(0, Health - damage);

            if (Health <= 0)
            {
                Dead = true;
                Runner.Despawn(Object);
                return;
            }

            Invincible = true;
            InvincibleTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    invincibleTime);
        }

        public void Heal(float amount)
        {
            if (!HasStateAuthority)
                return;

            Health =
                Mathf.Min(
                    Health + amount,
                    startingHealth);
        }

        public void SetShield(bool enable)
        {
            if (!HasStateAuthority)
                return;

            HasShield = enable;
        }
    }
}