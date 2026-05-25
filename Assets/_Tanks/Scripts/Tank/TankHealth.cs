using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : MonoBehaviour
    {
        // =====================================================
        // ■ HP関連
        // =====================================================

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
        // ■ 内部管理用
        // =====================================================

        // 爆発音
        private AudioSource m_ExplosionAudio;

        // 爆発パーティクル
        private ParticleSystem m_ExplosionParticles;

        // 現在のHP
        private float m_CurrentHealth;

        // 死亡済みか
        private bool m_Dead;

        // シールド軽減率
        // 例：0.5f → ダメージ半減
        private float m_ShieldValue;

        // 無敵状態か
        private bool m_IsInvincible;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // 爆発エフェクト生成
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


            // HPバーの最大値設定
            m_Slider.maxValue =
                m_StartingHealth;
        }


        // =====================================================
        // ■ オブジェクト削除時
        // =====================================================

        private void OnDestroy()
        {
            // 生成した爆発エフェクト削除
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
            // HP初期化
            m_CurrentHealth =
                m_StartingHealth;

            // 死亡状態解除
            m_Dead = false;

            // シールド解除
            m_HasShield = false;

            // シールド値初期化
            m_ShieldValue = 0;

            // 無敵解除
            m_IsInvincible = false;


            // HPバー更新
            SetHealthUI();
        }


        // =====================================================
        // ■ ダメージ処理
        // =====================================================

        public void TakeDamage(float amount)
        {
            // 無敵中ならダメージを受けない
            if (!m_IsInvincible)
            {
                // シールド軽減込みでHP減少
                m_CurrentHealth -=
                    amount *
                    (1 - m_ShieldValue);


                // HPバー更新
                SetHealthUI();


                // HPが0以下なら死亡
                if (m_CurrentHealth <= 0f &&
                    !m_Dead)
                {
                    OnDeath();
                }
            }
        }


        // =====================================================
        // ■ HP回復
        // =====================================================

        public void IncreaseHealth(float amount)
        {
            // 回復後も最大HP以下なら加算
            if (m_CurrentHealth + amount <=
                m_StartingHealth)
            {
                m_CurrentHealth += amount;
            }

            // 最大HPを超える場合
            else
            {
                // 最大値で止める
                m_CurrentHealth =
                    m_StartingHealth;
            }


            // HPバー更新
            SetHealthUI();
        }


        // =====================================================
        // ■ シールドON/OFF
        // =====================================================

        public void ToggleShield(float shieldAmount)
        {
            // true / false を切り替える
            m_HasShield =
                !m_HasShield;


            // =================================================
            // シールドON
            // =================================================

            if (m_HasShield)
            {
                // ダメージ軽減率設定
                m_ShieldValue =
                    shieldAmount;
            }

            // =================================================
            // シールドOFF
            // =================================================

            else
            {
                // 軽減無し
                m_ShieldValue = 0;
            }
        }


        // =====================================================
        // ■ 無敵ON/OFF
        // =====================================================

        public void ToggleInvincibility()
        {
            // true / false 切り替え
            m_IsInvincible =
                !m_IsInvincible;
        }


        // =====================================================
        // ■ HPバー更新
        // =====================================================

        private void SetHealthUI()
        {
            // Sliderの値更新
            m_Slider.value =
                m_CurrentHealth;


            // =================================================
            // HP割合に応じて色変更
            // =================================================
            // HP満タン → 緑
            // HP0       → 赤
            // 中間      → 黄色系
            // =================================================

            m_FillImage.color =
                Color.Lerp(
                    m_ZeroHealthColor,
                    m_FullHealthColor,
                    m_CurrentHealth /
                    m_StartingHealth
                );
        }


        // =====================================================
        // ■ 死亡処理
        // =====================================================

        private void OnDeath()
        {
            // 二重実行防止
            m_Dead = true;


            // =================================================
            // 爆発エフェクト位置設定
            // =================================================

            m_ExplosionParticles
                .transform.position =
                transform.position;


            // エフェクト有効化
            m_ExplosionParticles
                .gameObject
                .SetActive(true);


            // 爆発パーティクル再生
            m_ExplosionParticles.Play();


            // 爆発音再生
            m_ExplosionAudio.Play();


            // タンクを非表示（死亡）
            gameObject.SetActive(false);
        }
    }
}