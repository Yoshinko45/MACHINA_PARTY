using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class Shell_Explosion : NetworkBehaviour
    {
        [Header("Explosion Mask")]
        [SerializeField] LayerMask tankMask;

        [Header("Effects")]
        [SerializeField] ParticleSystem explosionParticles;
        [SerializeField] AudioSource explosionAudio;

        [Header("Explosion Settings")]
        public float maxLifeTime = 2f;

        [HideInInspector] public float m_MaxDamage = 100f;
        [HideInInspector] public float m_ExplosionForce = 50f;
        [HideInInspector] public float m_ExplosionRadius = 5f;

        [Networked] TickTimer LifeTimer {get; set;}

        GameObject owner;
        bool exploded;

        public void SetOwner(GameObject obj)
        {
            owner = obj;
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                LifeTimer =
                    TickTimer.CreateFromSeconds(
                        Runner,
                        maxLifeTime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;

            if (LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!HasStateAuthority)
                return;

            if (exploded)
                return;

            if (owner != null &&
                other.transform.root.gameObject == owner)
                return;

            exploded = true;

            Collider[] targets =
                Physics.OverlapSphere(
                    transform.position,
                    m_ExplosionRadius,
                    tankMask);

            foreach (Collider col in targets)
            {
                if (owner != null &&
                    col.transform.root.gameObject == owner)
                    continue;

                Tank_Move move =
                    col.GetComponent<Tank_Move>();

                if (move != null)
                {
                    move.AddExplosionForce(
                        m_ExplosionForce,
                        transform.position,
                        m_ExplosionRadius);
                }

                Tank_Health health =
                    col.GetComponent<Tank_Health>();

                if (health != null)
                {
                    float damage =
                        CalculateDamage(col.transform.position);

                    health.TakeDamage(damage);
                }
            }

            RPC_PlayExplosion();

            Runner.Despawn(Object);
        }

        float CalculateDamage(Vector3 targetPosition)
        {
            Vector3 dir =
                targetPosition - transform.position;

            float distance = dir.magnitude;

            float relative =
                (m_ExplosionRadius - distance) /
                m_ExplosionRadius;

            return Mathf.Max(
                0,
                relative * m_MaxDamage);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_PlayExplosion()
        {
            if (explosionParticles != null)
            {
                explosionParticles.transform.parent = null;
                explosionParticles.Play();

                Destroy(
                    explosionParticles.gameObject,
                    explosionParticles.main.duration);
            }

            if (explosionAudio != null)
            {
                explosionAudio.Play();
            }
        }
    }
}