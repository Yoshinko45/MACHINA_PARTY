using UnityEngine;

namespace Tanks.Complete
{
    public class ShellExplosion : MonoBehaviour
    {
        // =====================================================
        // ■ 爆風対象レイヤー
        // =====================================================

        [Header("Explosion Mask")]

        // 爆風が影響するレイヤー
        // Tankレイヤーのみを設定推奨
        public LayerMask m_TankMask;


        // =====================================================
        // ■ エフェクト関連
        // =====================================================

        [Header("Effects")]

        // 爆発パーティクル
        public ParticleSystem m_ExplosionParticles;

        // 爆発音
        public AudioSource m_ExplosionAudio;


        // =====================================================
        // ■ 爆発設定
        // =====================================================

        [Header("Explosion Settings")]

        // Shellが自動削除されるまでの時間
        [HideInInspector]
        public float m_MaxLifeTime = 2f;

        // 最大ダメージ
        [HideInInspector]
        public float m_MaxDamage = 100f;

        // 爆風の吹き飛ばし力
        [HideInInspector]
        public float m_ExplosionForce = 50f;

        // 爆風範囲
        [HideInInspector]
        public float m_ExplosionRadius = 5f;


        // =====================================================
        // ■ 発射者情報
        // =====================================================

        // この弾を撃ったTank
        // 自爆防止に使用
        private GameObject m_Owner;


        // =====================================================
        // ■ 爆発状態管理
        // =====================================================

        // 多重爆発防止
        private bool m_HasExploded;


        // =====================================================
        // ■ 発射者設定
        // =====================================================

        /// <summary>
        /// 弾を撃ったTankを登録
        /// </summary>
        public void SetOwner(GameObject owner)
        {
            m_Owner = owner;
        }


        // =====================================================
        // ■ Start
        // =====================================================

        private void Start()
        {
            // 一定時間後に自動削除
            Destroy(gameObject, m_MaxLifeTime);
        }


        // =====================================================
        // ■ Trigger衝突
        // =====================================================

        private void OnTriggerEnter(Collider other)
        {
            // =================================================
            // 既に爆発済みなら終了
            // =================================================

            if (m_HasExploded)
            {
                return;
            }


            // =================================================
            // 発射者本人に当たった場合は無視
            // =================================================

            // 発射直後に自分へ当たる事故防止
            if (m_Owner != null &&
                other.transform.root.gameObject ==
                m_Owner)
            {
                return;
            }


            // =================================================
            // 爆発済みにする
            // =================================================

            m_HasExploded = true;


            // =================================================
            // デバッグ表示
            // =================================================

            Debug.Log(
                "Shell Hit : " +
                other.name
            );


            // =================================================
            // 爆風範囲内のCollider取得
            // =================================================

            Collider[] colliders =
                Physics.OverlapSphere(
                    transform.position,
                    m_ExplosionRadius,
                    m_TankMask
                );


            // =================================================
            // 爆風処理
            // =================================================

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider =
                    colliders[i];


                // =============================================
                // 発射者本人はダメージ無効
                // =============================================

                if (m_Owner != null &&
                    targetCollider.transform.root.gameObject ==
                    m_Owner)
                {
                    continue;
                }


                // =============================================
                // TankMovement取得
                // =============================================

                TankMovement tankMovement =
                    targetCollider.GetComponent<TankMovement>();


                // 無ければスキップ
                if (tankMovement == null)
                {
                    continue;
                }


                // =============================================
                // 爆風ノックバック
                // =============================================

                tankMovement.AddExplosionForce(
                    m_ExplosionForce,
                    transform.position,
                    m_ExplosionRadius
                );


                // =============================================
                // TankHealth取得
                // =============================================

                TankHealth targetHealth =
                    targetCollider.GetComponent<TankHealth>();


                // 無ければスキップ
                if (targetHealth == null)
                {
                    continue;
                }


                // =============================================
                // ダメージ計算
                // =============================================

                float damage =
                    CalculateDamage(
                        targetCollider.transform.position
                    );


                // =============================================
                // ダメージ適用
                // =============================================

                targetHealth.TakeDamage(damage);
            }


            // =================================================
            // パーティクルをShellから切り離す
            // =================================================

            // Shell削除後もエフェクト継続させる
            m_ExplosionParticles.transform.parent =
                null;


            // =================================================
            // 爆発エフェクト再生
            // =================================================

            m_ExplosionParticles.Play();


            // =================================================
            // 爆発音再生
            // =================================================

            if (m_ExplosionAudio != null)
            {
                m_ExplosionAudio.Play();
            }


            // =================================================
            // パーティクル削除
            // =================================================

            ParticleSystem.MainModule mainModule =
                m_ExplosionParticles.main;

            Destroy(
                m_ExplosionParticles.gameObject,
                mainModule.duration
            );


            // =================================================
            // Shell削除
            // =================================================

            Destroy(gameObject);
        }


        // =====================================================
        // ■ ダメージ計算
        // =====================================================

        /// <summary>
        /// 爆心地との距離からダメージを計算
        /// </summary>
        private float CalculateDamage(
            Vector3 targetPosition)
        {
            // =================================================
            // 爆心地 → Target方向
            // =================================================

            Vector3 explosionToTarget =
                targetPosition -
                transform.position;


            // =================================================
            // 距離取得
            // =================================================

            float explosionDistance =
                explosionToTarget.magnitude;


            // =================================================
            // 距離割合
            // =================================================

            // 中心に近いほど大ダメージ
            float relativeDistance =
                (m_ExplosionRadius -
                 explosionDistance)
                / m_ExplosionRadius;


            // =================================================
            // ダメージ計算
            // =================================================

            float damage =
                relativeDistance *
                m_MaxDamage;


            // =================================================
            // 最低0ダメージ保証
            // =================================================

            damage =
                Mathf.Max(0f, damage);

            return damage;
        }


        // =====================================================
        // ■ 爆風範囲可視化
        // =====================================================

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            Gizmos.color =
                new Color(
                    1f,
                    0.3f,
                    0.3f,
                    0.5f
                );

            Gizmos.DrawWireSphere(
                transform.position,
                m_ExplosionRadius
            );
        }

#endif
    }
}