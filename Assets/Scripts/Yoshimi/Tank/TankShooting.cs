using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankShooting : MonoBehaviour
    {
        // =====================================================
        // ■ InputReader
        // =====================================================

        // このタンク専用の入力
        private InputReader m_InputReader;


        // =====================================================
        // ■ 弾関連
        // =====================================================

        [Header("Shell")]

        // 発射する弾Prefab
        public Rigidbody m_Shell;

        // 発射位置
        public Transform m_FireTransform;


        // =====================================================
        // ■ UI関連
        // =====================================================

        [Header("UI")]

        // チャージゲージ
        public Slider m_AimSlider;


        // =====================================================
        // ■ サウンド関連
        // =====================================================

        [Header("Audio")]

        // 発射音用AudioSource
        public AudioSource m_ShootingAudio;

        // チャージ音
        public AudioClip m_ChargingClip;

        // 発射音
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

        // 現在の発射力
        private float m_CurrentLaunchForce;

        // チャージ速度
        private float m_ChargeSpeed;

        // 発射済みか
        private bool m_Fired;

        // チャージ中か
        private bool m_IsCharging;

        // 最低威力保存用
        private float m_BaseMinLaunchForce;

        // クールタイムタイマー
        private float m_ShotCooldownTimer;


        // =====================================================
        // ■ 特殊弾関連
        // =====================================================

        // 強化弾を持っているか
        private bool m_HasSpecialShell;

        // 強化倍率
        private float m_SpecialShellMultiplier = 1f;


        // =====================================================
        // ■ 外部参照用
        // =====================================================

        // 現在のチャージ割合
        public float CurrentChargeRatio =>
            (m_CurrentLaunchForce - m_MinLaunchForce) /
            (m_MaxLaunchForce - m_MinLaunchForce);

        // チャージ中か
        public bool IsCharging => m_IsCharging;

        // CPU操作か
        public bool m_IsComputerControlled { get; set; } = false;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // InputReader取得
            m_InputReader =
                GetComponent<InputReader>();


            // 無かった場合
            if (m_InputReader == null)
            {
                Debug.LogError(
                    gameObject.name +
                    " に InputReader がありません"
                );
            }
        }


        // =====================================================
        // ■ 有効化時
        // =====================================================

        private void OnEnable()
        {
            // 発射力初期化
            m_CurrentLaunchForce =
                m_MinLaunchForce;

            // 最低威力保存
            m_BaseMinLaunchForce =
                m_MinLaunchForce;

            // 発射状態リセット
            m_Fired = false;

            // チャージ停止
            m_IsCharging = false;


            // =================================================
            // スライダー初期化
            // =================================================

            if (m_AimSlider != null)
            {
                m_AimSlider.minValue =
                    m_MinLaunchForce;

                m_AimSlider.maxValue =
                    m_MaxLaunchForce;

                m_AimSlider.value =
                    m_MinLaunchForce;
            }


            // =================================================
            // 強化弾初期化
            // =================================================

            m_HasSpecialShell = false;

            m_SpecialShellMultiplier = 1f;
        }


        // =====================================================
        // ■ Start
        // =====================================================

        private void Start()
        {
              if (m_MaxChargeTime <= 0.0001f)
            {
                Debug.LogError("m_MaxChargeTime が不正です（0以下）");
                m_MaxChargeTime = 0.1f;
            }

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
            // CPU操作かプレイヤー操作か
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

            m_CurrentLaunchForce =
                m_MinLaunchForce;

            m_IsCharging = true;


            // チャージ音
            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip =
                    m_ChargingClip;

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
            // InputReader未取得
            if (m_InputReader == null)
            {
                return;
            }


            // =================================================
            // 入力取得
            // =================================================

            bool firePressed =
                m_InputReader.FirePressed;

            bool fireHolding =
                m_InputReader.FireHeld;

            bool fireReleased =
                m_InputReader.FireReleased;


            // =================================================
            // クールタイム減少
            // =================================================

            if (m_ShotCooldownTimer > 0f)
            {
                m_ShotCooldownTimer -=
                    Time.deltaTime;
            }


            // =================================================
            // 待機中UI
            // =================================================

            if (!m_IsCharging &&
                m_AimSlider != null)
            {
                m_AimSlider.value =
                    m_BaseMinLaunchForce;
            }


            // =================================================
            // ボタン押した瞬間
            // =================================================

            if (m_ShotCooldownTimer <= 0f &&
                firePressed &&
                !m_IsCharging)
            {
                // 発射状態リセット
                m_Fired = false;

                // 最低威力から開始
                m_CurrentLaunchForce =
                    m_MinLaunchForce;

                // チャージ開始
                m_IsCharging = true;


                // チャージ音
                if (m_ShootingAudio != null)
                {
                    m_ShootingAudio.clip =
                        m_ChargingClip;

                    m_ShootingAudio.Play();
                }
            }


            // =================================================
            // チャージ中
            // =================================================

            if (m_IsCharging &&
                !m_Fired)
            {
                // チャージ増加
                m_CurrentLaunchForce +=
                    m_ChargeSpeed *
                    Time.deltaTime;


                // 最大値制限
                m_CurrentLaunchForce =
                    Mathf.Clamp(
                        m_CurrentLaunchForce,
                        m_MinLaunchForce,
                        m_MaxLaunchForce
                    );


                // UI更新
                if (m_AimSlider != null)
                {
                    m_AimSlider.value =
                        m_CurrentLaunchForce;
                }


                // 最大チャージで自動発射
                if (m_CurrentLaunchForce >=
                    m_MaxLaunchForce)
                {
                    Fire();

                    return;
                }


                // ボタン離した
                if (fireReleased)
                {
                    Fire();

                    return;
                }
            }
        }


        // =====================================================
        // ■ CPU操作更新
        // =====================================================

        private void ComputerUpdate()
        {
            // UI更新
            if (m_AimSlider != null)
            {
                m_AimSlider.value =
                    m_BaseMinLaunchForce;
            }


            // 最大チャージ到達
            if (m_CurrentLaunchForce >=
                m_MaxLaunchForce &&
                !m_Fired)
            {
                m_CurrentLaunchForce =
                    m_MaxLaunchForce;

                Fire();
            }


            // チャージ中
            else if (m_IsCharging &&
                     !m_Fired)
            {
                m_CurrentLaunchForce +=
                    m_ChargeSpeed *
                    Time.deltaTime;


                // UI更新
                if (m_AimSlider != null)
                {
                    m_AimSlider.value =
                        m_CurrentLaunchForce;
                }
            }
        }


        // =====================================================
        // ■ 発射処理
        // =====================================================

        private void Fire()
        {
            // 弾Prefab未設定
            if (m_Shell == null)
            {
                Debug.LogError(
                    "m_Shell が設定されていません"
                );

                return;
            }


            // 発射位置未設定
            if (m_FireTransform == null)
            {
                Debug.LogError(
                    "m_FireTransform が設定されていません"
                );

                return;
            }


            // 発射済み
            m_Fired = true;

            // チャージ終了
            m_IsCharging = false;


            // =================================================
            // 生成位置
            // =================================================

            Vector3 spawnPos =
                m_FireTransform.position +
                m_FireTransform.forward * 2f;


            // =================================================
            // 弾生成
            // =================================================

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
                 explosionData.SetOwner(gameObject);

                explosionData.m_ExplosionForce =
                    m_ExplosionForce;

                explosionData.m_ExplosionRadius =
                    m_ExplosionRadius;

                explosionData.m_MaxDamage =
                    m_MaxDamage;


                // 強化弾処理
                if (m_HasSpecialShell)
                {
                    explosionData.m_MaxDamage *=
                        m_SpecialShellMultiplier;

                    m_HasSpecialShell = false;

                    m_SpecialShellMultiplier = 1f;


                    // PowerUp解除
                    PowerUpDetector powerUpDetector =
                        GetComponent<PowerUpDetector>();

                    if (powerUpDetector != null)
                    {
                        powerUpDetector.m_HasActivePowerUp =
                            false;
                    }


                    // HUD非表示
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
                m_ShootingAudio.clip =
                    m_FireClip;

                m_ShootingAudio.Play();
            }


            // =================================================
            // リセット
            // =================================================

            m_CurrentLaunchForce =
                m_MinLaunchForce;

            m_ShotCooldownTimer =
                m_ShotCooldown;
        }


        // =====================================================
        // ■ 強化弾装備
        // =====================================================

        public void EquipSpecialShell(
            float damageMultiplier)
        {
            m_HasSpecialShell = true;

            m_SpecialShellMultiplier =
                damageMultiplier;
        }


        // =====================================================
        // ■ 着弾予測位置取得
        // =====================================================

        public Vector3 GetProjectilePosition(
            float chargingLevel)
        {
            // 発射位置未設定
            if (m_FireTransform == null)
            {
                return transform.position;
            }


            // 発射力補間
            float chargeLevel =
                Mathf.Lerp(
                    m_MinLaunchForce,
                    m_MaxLaunchForce,
                    chargingLevel
                );


            // 初速
            Vector3 velocity =
                chargeLevel *
                m_FireTransform.forward;


            // 放物線計算
            float a =
                0.5f * Physics.gravity.y;

            float b =
                velocity.y;

            float c =
                m_FireTransform.position.y;


            // 判別式
            float sqrtContent =
                b * b - 4 * a * c;


            // 解無し
            if (sqrtContent <= 0)
            {
                return m_FireTransform.position;
            }


            // 二次方程式
            float answer1 =
                (-b + Mathf.Sqrt(sqrtContent)) /
                (2 * a);

            float answer2 =
                (-b - Mathf.Sqrt(sqrtContent)) /
                (2 * a);


            // 正の解
            float answer =
                answer1 > 0 ?
                answer1 :
                answer2;


            // 着弾位置
            Vector3 position =
                m_FireTransform.position +
                new Vector3(
                    velocity.x,
                    0,
                    velocity.z
                ) * answer;


            // 地面高さ
            position.y = 0;

            return position;
        }
    }
}