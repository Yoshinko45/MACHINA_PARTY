using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class TankShell : NetworkBehaviour
    {
        public LayerMask m_TankMask;

        public ParticleSystem m_ExplosionParticles;

        public AudioSource m_ExplosionAudio;

        [HideInInspector]
        public float m_MaxLifeTime = 2f;

        [HideInInspector]
        public float m_MaxDamage = 100f;

        [HideInInspector]
        public float m_ExplosionForce = 50f;

        [HideInInspector]
        public float m_ExplosionRadius = 5f;

        public override void Spawned()
        {
            Invoke(nameof(DespawnShell), m_MaxLifeTime);
        }

        void DespawnShell()
        {
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            // 爆風範囲取得
            Collider[] colliders =
                Physics.OverlapSphere(
                    transform.position,
                    m_ExplosionRadius,
                    m_TankMask
                );

            for (int i = 0; i < colliders.Length; i++)
            {
                // TankMovement取得
                TankMove tankMovement =
                    colliders[i].GetComponent<TankMove>();

                if (!tankMovement)
                    continue;

                // 爆風適用
                tankMovement.AddExplosionForce(
                    m_ExplosionForce,
                    transform.position,
                    m_ExplosionRadius
                );

                // TankHealth取得
                TankHealth targetHealth =
                    colliders[i].GetComponent<TankHealth>();

                if (!targetHealth)
                    continue;

                // ダメージ計算
                float damage =
                    CalculateDamage(
                        colliders[i].transform.position
                    );

                // ダメージ適用
                targetHealth.TakeDamage(damage);
            }

            // パーティクルを分離
            m_ExplosionParticles.transform.parent = null;

            // パーティクル再生
            m_ExplosionParticles.Play();

            // SE再生
            m_ExplosionAudio.Play();

            // パーティクル削除
            ParticleSystem.MainModule mainModule =
                m_ExplosionParticles.main;

            Destroy(
                m_ExplosionParticles.gameObject,
                mainModule.duration
            );

            // Shell削除
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }


        private float CalculateDamage(Vector3 targetPosition)
        {
            // Shell → Target方向
            Vector3 explosionToTarget =
                targetPosition - transform.position;

            // 距離
            float explosionDistance =
                explosionToTarget.magnitude;

            // 距離割合
            float relativeDistance =
                (m_ExplosionRadius - explosionDistance)
                / m_ExplosionRadius;

            // ダメージ計算
            float damage =
                relativeDistance * m_MaxDamage;

            // 最低0
            damage = Mathf.Max(0f, damage);

            return damage;
        }
    }
}