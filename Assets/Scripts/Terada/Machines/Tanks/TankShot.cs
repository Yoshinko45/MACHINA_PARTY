using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankShot : NetworkBehaviour, IAttackVehicle
    {
        [Header("Prefab")]
        [SerializeField] NetworkObject shellPrefab;

        [SerializeField] Transform fireTransform;

        [Header("Launch + Cooldown")]
        [SerializeField] float minLaunchForce = 5f;
        [SerializeField] float maxLaunchForce = 20f;
        [SerializeField] float chargeTime = 1f;
        [SerializeField] float shotCooldown = 1f;

        [Header("Explosion")]
        [SerializeField] float maxDamage = 100f;
        [SerializeField] float explosionForce = 50f;
        [SerializeField] float explosionRadius = 5f;

        bool hasSpecialShell;
        float specialShellMultiplier = 1f;

        [Header("AimUI")]
        [SerializeField] Slider chargeSlider;
        [SerializeField] ProjectileMarker marker;

        [Header("Audio")]
        [SerializeField] AudioSource shootingAudio;

        [SerializeField] AudioClip chargingClip;

        [SerializeField] AudioClip fireClip;

        [Networked] TickTimer Cooldown { get; set; }
        [Networked] public bool IsCharging { get; set; }
        [Networked] public float CurrentCharge { get; set; }

        private float ChargeSpeed => (maxLaunchForce - minLaunchForce) / chargeTime;

        public float ChargeRatio => Mathf.InverseLerp(minLaunchForce, maxLaunchForce, CurrentCharge);

        public override void Spawned()
        {
            marker.Initialize(this);
            bool local = Object.HasInputAuthority;
            chargeSlider.gameObject.SetActive(true);
            marker.gameObject.SetActive(true);

            CurrentCharge = minLaunchForce;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (!IsCharging) return;

            CurrentCharge += ChargeSpeed * Runner.DeltaTime;
            CurrentCharge = Mathf.Clamp(CurrentCharge, minLaunchForce, maxLaunchForce);

            if (chargeSlider != null)
            {
                chargeSlider.value = CurrentCharge;
            }

            if (CurrentCharge >= maxLaunchForce)
            {
                if (shootingAudio != null && shootingAudio.clip == chargingClip)
                {
                    shootingAudio.Stop();
                }

                Fire(CurrentCharge);
                IsCharging = false;
                CurrentCharge = minLaunchForce;
            }
        }

        public void BeginCharge()
        {
            if (!Cooldown.ExpiredOrNotRunning(Runner)) return;
            if (IsCharging) return;
            IsCharging = true;
            CurrentCharge = minLaunchForce;

            if (Object.HasInputAuthority && shootingAudio != null)
            {
                shootingAudio.clip = chargingClip;
                shootingAudio.Play();
            }
        }

        public void ReleaseCharge()
        {
            if (!IsCharging) return;

            if (shootingAudio != null && shootingAudio.clip == chargingClip)
            {
                shootingAudio.Stop();
            }

             Fire(CurrentCharge);
            IsCharging = false;
            CurrentCharge = minLaunchForce;
        }

        public void Fire(float force)
        {

            if (shellPrefab == null) return;

            // ”­ŽËˆÊ’u•Ï‚¾‚Á‚½‚ç‚±‚êŽg‚Á‚Ä.
            Vector3 spawnPos = fireTransform.position +
                fireTransform.forward;

            NetworkObject shell = Runner.Spawn(
                shellPrefab,
                spawnPos,
                fireTransform.rotation
            );

            ShellExplosion explosion =
                shell.GetComponent<ShellExplosion>();

            if (explosion != null)
            {
                explosion.SetOwner(gameObject);

                explosion.m_MaxDamage = maxDamage;
                explosion.m_ExplosionForce = explosionForce;
                explosion.m_ExplosionRadius = explosionRadius;

                if (hasSpecialShell)
                {
                    explosion.m_MaxDamage *= specialShellMultiplier;

                    hasSpecialShell = false;
                    specialShellMultiplier = 1f;

                    PowerUpDetector detector =
                        GetComponent<PowerUpDetector>();

                    if (detector != null)
                        detector.m_HasActivePowerUp = false;

                    PowerUpHUD hud =
                        GetComponentInChildren<PowerUpHUD>();

                    if (hud != null)
                        hud.DisableActiveHUD();
                }
            }

            Rigidbody rb = shell.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = fireTransform.forward * force;
            }

            RPC_PlayerFireSound();

            Cooldown = TickTimer.CreateFromSeconds(Runner, shotCooldown);
        }

        public void EquipSpecialShell(float multiplier)
        {
            hasSpecialShell = true;
            specialShellMultiplier = multiplier;
        }

        public Vector3 GetProjectilePosition()
        {
            float force = CurrentCharge;

            Vector3 velocity =
                fireTransform.forward * force;

            float a = 0.5f * Physics.gravity.y;
            float b = velocity.y;
            float c = fireTransform.position.y;

            float discriminant =
                b * b - 4 * a * c;

            if (discriminant <= 0)
                return fireTransform.position;

            float t1 =
                (-b + Mathf.Sqrt(discriminant))
                / (2 * a);

            float t2 =
                (-b - Mathf.Sqrt(discriminant))
                / (2 * a);

            float time =
                Mathf.Max(t1, t2);

            Vector3 result =
                fireTransform.position +
                new Vector3(
                    velocity.x,
                    0,
                    velocity.z)
                * time;

            result.y = 0;

            return result;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayerFireSound()
        {
            if (shootingAudio == null)
                return;
            shootingAudio.Stop();
            shootingAudio.clip = fireClip;
            shootingAudio.Play();
        }
    }
}