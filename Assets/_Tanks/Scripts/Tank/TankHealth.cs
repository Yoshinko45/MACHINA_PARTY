using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : MonoBehaviour
    {
        // =====================================================
        // ■ HP関連
        // =====================================================

        [Header("Health")]

        // タンクの最大HP
        public float m_StartingHealth = 100f;

        // HPバー本体（Slider）
        public Slider m_Slider;

        // HPバーの色部分
        public Image m_FillImage;

        // HP満タン時の色
        public Color m_FullHealthColor = Color.green;

        // HP0時の色
        public Color m_ZeroHealthColor = Color.red;

        // 爆発エフェクトPrefab
        public GameObject m_ExplosionPrefab;

        // シールドを持っているか
        [HideInInspector]
        public bool m_HasShield;


        // =====================================================
        // ■ 無敵時間設定
        // =====================================================

        [Header("Invincible")]

        // 被弾後の無敵時間
        public float m_InvincibleTime = 1.5f;

        // 点滅間隔
        public float m_BlinkInterval = 0.1f;


        // =====================================================
        // ■ HPバー演出設定
        // =====================================================

        [Header("Health Bar Animation")]

        // HPバーが減る速度
        public float m_HealthBarSmoothSpeed = 8f;


        // =====================================================
        // ■ 内部管理用
        // =====================================================

        // 爆発音
        private AudioSource m_ExplosionAudio;

        // 爆発パーティクル
        private ParticleSystem m_ExplosionParticles;

        // 現在のHP
        private float m_CurrentHealth;

        // HPバー表示用HP
        private float m_DisplayHealth;

        // 死亡済みか
        private bool m_Dead;

        // シールド軽減率
        private float m_ShieldValue;

        // 無敵状態か
        private bool m_IsInvincible;

        // タンク内のRenderer一覧
        private Renderer[] m_Renderers;

        // 無敵Coroutine管理
        private Coroutine m_InvincibleCoroutine;


        // =====================================================
        // ■ 外部参照用
        // =====================================================

        // 現在無敵中か
        public bool IsInvincible =>
            m_IsInvincible;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // =================================================
            // 爆発エフェクト生成
            // =================================================

            if (m_ExplosionPrefab != null)
            {
                m_ExplosionParticles =
                    Instantiate(m_ExplosionPrefab)
                    .GetComponent<ParticleSystem>();


                // 爆発音取得
                m_ExplosionAudio =
                    m_ExplosionParticles
                    .GetComponent<AudioSource>();


                // 最初は非表示
                m_ExplosionParticles
                    .gameObject
                    .SetActive(false);
            }


            // =================================================
            // HPバー最大値設定
            // =================================================

            if (m_Slider != null)
            {
                m_Slider.maxValue =
                    m_StartingHealth;
            }


            // =================================================
            // Renderer取得
            // =================================================

            m_Renderers =
                GetComponentsInChildren<Renderer>();
        }


        // =====================================================
        // ■ Update
        // =====================================================

        private void Update()
        {
            // HPバー更新
            SetHealthUI();
        }


        // =====================================================
        // ■ オブジェクト削除時
        // =====================================================

        private void OnDestroy()
        {
            // =================================================
            // 生成した爆発エフェクト削除
            // =================================================

            if (m_ExplosionParticles != null)
            {
                Destroy(
                    m_ExplosionParticles.gameObject
                );
            }
        }


        // =====================================================
        // ■ 有効化時
        // =====================================================

        private void OnEnable()
        {
            // =================================================
            // HP初期化
            // =================================================

            m_CurrentHealth =
                m_StartingHealth;

            m_DisplayHealth =
                m_StartingHealth;


            // =================================================
            // 死亡状態解除
            // =================================================

            m_Dead = false;


            // =================================================
            // シールド解除
            // =================================================

            m_HasShield = false;

            m_ShieldValue = 0f;


            // =================================================
            // 無敵解除
            // =================================================

            m_IsInvincible = false;


            // =================================================
            // Renderer表示ON
            // =================================================

            foreach (Renderer renderer in m_Renderers)
            {
                renderer.enabled = true;
            }


            // =================================================
            // HPバー更新
            // =================================================

            SetHealthUI();
        }


        // =====================================================
        // ■ ダメージ処理
        // =====================================================

        public void TakeDamage(float amount)
        {
            // =================================================
            // 無敵中ならダメージ無効
            // =================================================

            if (m_IsInvincible ||
                m_Dead)
            {
                return;
            }


            // =================================================
            // シールド軽減込みでHP減少
            // =================================================

            m_CurrentHealth =
                Mathf.Max(
                    m_CurrentHealth -
                    amount *
                    (1f - m_ShieldValue),
                    0f
                );


            // =================================================
            // HP0以下なら死亡
            // =================================================

            if (m_CurrentHealth <= 0f)
            {
                OnDeath();

                return;
            }


            // =================================================
            // 無敵Coroutine再起動
            // =================================================

            if (m_InvincibleCoroutine != null)
            {
                StopCoroutine(
                    m_InvincibleCoroutine
                );
            }


            m_InvincibleCoroutine =
                StartCoroutine(
                    InvincibleRoutine()
                );
        }


        // =====================================================
        // ■ HP回復
        // =====================================================

        public void IncreaseHealth(float amount)
        {
            // =================================================
            // 最大HPを超えないように回復
            // =================================================

            m_CurrentHealth =
                Mathf.Clamp(
                    m_CurrentHealth + amount,
                    0f,
                    m_StartingHealth
                );
        }


        // =====================================================
        // ■ シールド設定
        // =====================================================

        public void SetShield(
            bool enabled,
            float shieldAmount = 0f)
        {
            m_HasShield = enabled;


            // =================================================
            // シールドON
            // =================================================

            if (m_HasShield)
            {
                m_ShieldValue =
                    Mathf.Clamp01(
                        shieldAmount
                    );
            }


            // =================================================
            // シールドOFF
            // =================================================

            else
            {
                m_ShieldValue = 0f;
            }
        }


        // =====================================================
        // ■ 無敵設定
        // =====================================================

        public void SetInvincible(bool value)
        {
            m_IsInvincible = value;
        }


        // =====================================================
        // ■ HPバー更新
        // =====================================================

        private void SetHealthUI()
        {
            // =================================================
            // 表示HPを徐々に現在HPへ近づける
            // =================================================

            m_DisplayHealth =
                Mathf.Lerp(
                    m_DisplayHealth,
                    m_CurrentHealth,
                    Time.deltaTime *
                    m_HealthBarSmoothSpeed
                );


            // =================================================
            // 小数誤差対策
            // =================================================

            if (Mathf.Abs(
                m_DisplayHealth -
                m_CurrentHealth) < 0.01f)
            {
                m_DisplayHealth =
                    m_CurrentHealth;
            }


            // =================================================
            // Slider値更新
            // =================================================

            if (m_Slider != null)
            {
                m_Slider.value =
                    m_DisplayHealth;
            }


            // =================================================
            // HP割合に応じて色変更
            // =================================================

            if (m_FillImage != null)
            {
                m_FillImage.color =
                    Color.Lerp(
                        m_ZeroHealthColor,
                        m_FullHealthColor,
                        m_DisplayHealth /
                        m_StartingHealth
                    );
            }
        }


        // =====================================================
        // ■ 被弾後無敵処理
        // =====================================================

        private IEnumerator InvincibleRoutine()
        {
            // =================================================
            // 無敵ON
            // =================================================

            m_IsInvincible = true;


            // 経過時間
            float timer = 0f;


            // =================================================
            // 無敵時間中ループ
            // =================================================

            while (timer < m_InvincibleTime)
            {
                // =================================================
                // Renderer ON/OFF切り替え
                // =================================================

                foreach (Renderer renderer in m_Renderers)
                {
                    renderer.enabled =
                        !renderer.enabled;
                }


                // =================================================
                // 点滅待機
                // =================================================

                yield return new WaitForSecondsRealtime(
                    m_BlinkInterval
                );


                // 時間加算
                timer += m_BlinkInterval;
            }


            // =================================================
            // 最後は必ず表示ON
            // =================================================

            foreach (Renderer renderer in m_Renderers)
            {
                renderer.enabled = true;
            }


            // =================================================
            // 無敵解除
            // =================================================

            m_IsInvincible = false;

            m_InvincibleCoroutine = null;
        }


        // =====================================================
        // ■ 死亡処理
        // =====================================================

        private void OnDeath()
        {
            // =================================================
            // 二重実行防止
            // =================================================

            if (m_Dead)
            {
                return;
            }

            m_Dead = true;


            // =================================================
            // 無敵Coroutine停止
            // =================================================

            if (m_InvincibleCoroutine != null)
            {
                StopCoroutine(
                    m_InvincibleCoroutine
                );
            }

            m_InvincibleCoroutine = null;


            // =================================================
            // Renderer強制表示
            // =================================================

            foreach (Renderer renderer in m_Renderers)
            {
                renderer.enabled = true;
            }


            // =================================================
            // 爆発エフェクト位置設定
            // =================================================

            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles
                    .transform.position =
                    transform.position;


                // エフェクト有効化
                m_ExplosionParticles
                    .gameObject
                    .SetActive(true);


                // 爆発パーティクル再生
                m_ExplosionParticles.Play();
            }


            // =================================================
            // 爆発音再生
            // =================================================

            if (m_ExplosionAudio != null)
            {
                AudioSource.PlayClipAtPoint(
                    m_ExplosionAudio.clip,
                    transform.position
                );
            }


            // =================================================
            // タンク非表示
            // =================================================

            gameObject.SetActive(false);
        }
    }
}