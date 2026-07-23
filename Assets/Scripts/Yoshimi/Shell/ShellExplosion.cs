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
        // ■ 視認性向上（トレイル・発光）
        // =====================================================
        //
        // 弾のメッシュ自体は小さいままにしておき（当たり判定や
        // タンクとの見た目のバランスを崩さないため）、飛んでいる
        // 最中だけ目立つようにするための演出。
        //
        // TankShooting.Fire()から、撃ったプレイヤーの色で
        // SetTrailColor()が呼ばれ、トレイル・発光の色が
        // プレイヤーカラーに合わせて変わる（誰の弾か一目で
        // わかるようにするため）。
        //
        // 数値はここで調整可能。実際に試遊しながら太さ・
        // 明るさを詰めていってほしい。
        //
        // =====================================================

        [Header("Visibility (Trail / Glow)")]

        [Tooltip("弾に光の軌跡（トレイル）を付けるか")]
        public bool m_UseTrail = true;

        [Tooltip("トレイルの表示時間（秒）。長いほど尾が伸びる")]
        public float m_TrailTime = 0.15f;

        [Tooltip("トレイルの太さ（弾側）")]
        public float m_TrailStartWidth = 0.45f;

        [Tooltip("トレイルの太さ（末尾側）")]
        public float m_TrailEndWidth = 0.05f;

        [Tooltip("弾自体に発光（Point Light）を付けるか")]
        public bool m_UseGlow = true;

        [Tooltip("発光が届く範囲")]
        public float m_GlowRange = 3f;

        [Tooltip("発光の強さ")]
        public float m_GlowIntensity = 3f;

        // 実行時に追加するトレイル本体
        private TrailRenderer m_TrailRenderer;

        // 実行時に追加する発光用ライト
        private Light m_GlowLight;


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
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            SetupTrail();
            SetupGlow();
        }


        // =====================================================
        // ■ トレイル初期化
        // =====================================================
        //
        // プレイヤーカラーが伝わる前（SetTrailColor呼び出し前）に
        // 見えてしまっても不自然にならないよう、まずは白系の
        // デフォルト色で用意しておく。
        //
        // =====================================================

        private void SetupTrail()
        {
            if (!m_UseTrail)
            {
                return;
            }

            m_TrailRenderer =
                gameObject.AddComponent<TrailRenderer>();

            m_TrailRenderer.time =
                m_TrailTime;

            m_TrailRenderer.startWidth =
                m_TrailStartWidth;

            m_TrailRenderer.endWidth =
                m_TrailEndWidth;

            m_TrailRenderer.minVertexDistance = 0.05f;

            m_TrailRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            m_TrailRenderer.receiveShadows = false;

            m_TrailRenderer.textureMode =
                LineTextureMode.Stretch;

            Shader trailShader =
                FindBestUnlitShader();

            if (trailShader != null)
            {
                m_TrailRenderer.material =
                    new Material(trailShader);
            }
            else
            {
                Debug.LogWarning(
                    "[ShellExplosion] トレイル用のシェーダーが" +
                    "見つかりませんでした。トレイルの色が" +
                    "正しく表示されない可能性があります。"
                );
            }

            // プレイヤーカラーが伝わるまでのデフォルト色
            ApplyTrailColor(Color.white);
        }


        // =====================================================
        // ■ 発光ライト初期化
        // =====================================================

        private void SetupGlow()
        {
            if (!m_UseGlow)
            {
                return;
            }

            GameObject glowObject =
                new GameObject("ShellGlow");

            glowObject.transform.SetParent(
                transform,
                false
            );

            m_GlowLight =
                glowObject.AddComponent<Light>();

            m_GlowLight.type = LightType.Point;

            m_GlowLight.range =
                m_GlowRange;

            m_GlowLight.intensity =
                m_GlowIntensity;

            m_GlowLight.shadows = LightShadows.None;

            m_GlowLight.color = Color.white;
        }


        // =====================================================
        // ■ トレイル・発光用シェーダー探索
        // =====================================================
        //
        // URP環境でも確実に見えるように、優先順位を付けて
        // 対応シェーダーを探す。
        //
        // =====================================================

        private static Shader FindBestUnlitShader()
        {
            string[] candidates =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Universal Render Pipeline/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default",
                "Unlit/Color"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Shader shader =
                    Shader.Find(candidates[i]);

                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }


        // =====================================================
        // ■ トレイル・発光カラー設定
        // =====================================================
        //
        // TankShooting.Fire()から、発射したプレイヤーの色で
        // 呼び出してもらうAPI。
        //
        // =====================================================

        public void SetTrailColor(Color color)
        {
            ApplyTrailColor(color);

            if (m_GlowLight != null)
            {
                m_GlowLight.color = color;
            }
        }


        // =====================================================
        // ■ トレイルへの色反映
        // =====================================================

        private void ApplyTrailColor(Color color)
        {
            if (m_TrailRenderer == null)
            {
                return;
            }

            // 発光っぽく見せるため、先端は白寄りに明るくし、
            // 末尾に向かってプレイヤーカラー→透明へ落とす
            Color brightHead =
                Color.Lerp(color, Color.white, 0.5f);

            Gradient gradient = new Gradient();

            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(brightHead, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            m_TrailRenderer.colorGradient = gradient;

            if (m_TrailRenderer.material == null)
            {
                return;
            }

            // Gradientを反映しないシェーダーだった場合の保険として、
            // マテリアル自体の色にも明るめの色を設定しておく
            if (m_TrailRenderer.material.HasProperty(
                    "_BaseColor"))
            {
                m_TrailRenderer.material.SetColor(
                    "_BaseColor",
                    brightHead
                );
            }

            if (m_TrailRenderer.material.HasProperty(
                    "_Color"))
            {
                m_TrailRenderer.material.SetColor(
                    "_Color",
                    brightHead
                );
            }
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