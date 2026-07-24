using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tanks.Complete
{
    // =========================================================
    // ■ タンク遮蔽物フェード
    // =========================================================
    //
    // カメラとタンクの間に大きな岩などの障害物が入り込むと、
    // タンクが完全に隠れて見えなくなってしまう問題への対策。
    //
    // 毎フレーム、カメラから各タンクへ向けてレイを飛ばし、
    // 指定レイヤー（m_ObstacleMask）に属する障害物に当たったら
    // その障害物だけを半透明化する。タンクが隠れなくなったら
    // 自動的に元の見た目へ戻す。
    //
    // ・CameraControlと同じGameObjectに追加して使う
    // 　（タンク戦は共有1台カメラのみなので、
    // 　　split screenでの誤爆の心配は無い）
    // ・対象にしたい岩などは専用のLayerを作って乗せ、
    // 　m_ObstacleMaskでそのLayerだけを選択すること
    // 　（地面やタンク自身まで含めると誤作動する）
    //
    // =========================================================

    [RequireComponent(typeof(CameraControl))]
    public class TankOcclusionFade : MonoBehaviour
    {
        // =====================================================
        // ■ 対象レイヤー
        // =====================================================

        [Header("対象レイヤー")]

        [Tooltip("透明化の対象にする障害物のレイヤー。" +
            "タンクを隠してしまう大きな岩などに専用のLayerを割り当てて設定する。" +
            "地面やタンク自身のLayerまで含めると誤作動するので注意")]
        public LayerMask m_ObstacleMask = 0;


        // =====================================================
        // ■ フェード設定
        // =====================================================

        [Header("フェード設定")]

        [Tooltip("透明化後の不透明度（0＝完全に透明、1＝不透明のまま＝実質OFF）")]
        [Range(0f, 1f)]
        public float m_FadedAlpha = 0.25f;

        [Tooltip("透明化・復元にかかる時間（秒）。短いほどパッと切り替わる")]
        public float m_FadeDuration = 0.15f;

        [Tooltip("タンクのどの高さを狙ってレイを飛ばすか（transform.position からの上方向オフセット）。" +
            "0のままだと接地面ギリギリを狙ってしまい、低い段差でも反応してしまうことがある")]
        public float m_TargetHeightOffset = 0.5f;


        // =====================================================
        // ■ 内部：カメラ・障害物ヒットバッファ
        // =====================================================

        private Camera m_Camera;
        private CameraControl m_CameraControl;

        // Physics.RaycastNonAlloc の使い回し用バッファ
        // （1本のレイで複数の岩が重なっていても全部拾えるようRaycastAll相当にする）
        private readonly RaycastHit[] m_HitBuffer = new RaycastHit[16];


        // =====================================================
        // ■ 内部：フェード状態管理
        // =====================================================

        private class FadeEntry
        {
            // 復元用（元の共有マテリアル。フェード完了後はこれに戻す）
            public Material[] OriginalSharedMaterials;

            // 復元用（元の影の落とし方）
            public ShadowCastingMode OriginalShadowMode;

            // 0=不透明 → 1=透明。毎フレームMoveTowardsで滑らかに変化させる
            public float CurrentBlend;

            // シェーダーがTransparent切り替えに対応しているか
            // （非対応の場合は表示/非表示の切り替えにフォールバックする）
            public bool SupportsTransparency;
        }

        // 現在フェード管理中のレンダラー一覧
        private readonly Dictionary<Renderer, FadeEntry> m_FadeEntries =
            new Dictionary<Renderer, FadeEntry>();

        // 今フレーム実際にタンクを遮っていたレンダラー（毎フレーム使い回す）
        private readonly HashSet<Renderer> m_OccludingThisFrame =
            new HashSet<Renderer>();

        // Dictionary反復中に削除しないための一時リスト（毎フレーム使い回す）
        private readonly List<Renderer> m_PendingRemoval =
            new List<Renderer>();


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            m_Camera =
                GetComponentInChildren<Camera>();

            m_CameraControl =
                GetComponent<CameraControl>();

            if (m_Camera == null)
            {
                Debug.LogError(
                    gameObject.name +
                    " : TankOcclusionFade は Camera が見つかりません"
                );
            }
        }


        // =====================================================
        // ■ LateUpdate
        // =====================================================
        //
        // CameraControlがLateUpdateでカメラを動かすため、
        // こちらも同じLateUpdateで判定する
        // （1フレームずれる可能性はあるが、
        // 　カメラはゆっくり追従するため体感には出ない）
        //
        // =====================================================

        private void LateUpdate()
        {
            if (m_Camera == null ||
                m_CameraControl == null)
            {
                return;
            }

            Transform[] targets =
                m_CameraControl.m_Targets;

            if (targets == null ||
                targets.Length == 0)
            {
                return;
            }


            // =================================================
            // 今フレームの遮蔽判定
            // =================================================

            m_OccludingThisFrame.Clear();

            for (int i = 0; i < targets.Length; i++)
            {
                Transform target = targets[i];

                if (target == null ||
                    !target.gameObject.activeSelf)
                {
                    continue;
                }

                DetectOccluders(target);
            }


            // =================================================
            // フェード状態の更新
            // =================================================

            UpdateFades();
        }


        // =====================================================
        // ■ 遮蔽物検出（カメラ→対象タンク）
        // =====================================================

        private void DetectOccluders(Transform target)
        {
            Vector3 camPos =
                m_Camera.transform.position;

            Vector3 targetPos =
                target.position +
                Vector3.up * m_TargetHeightOffset;

            Vector3 toTarget =
                targetPos - camPos;

            float distance =
                toTarget.magnitude;

            if (distance < 0.01f)
            {
                return;
            }

            Vector3 direction =
                toTarget / distance;


            // 障害物レイヤーだけを対象にレイを飛ばし、
            // 経路上の全ヒットを拾う（岩が重なっていても対応するため）
            int hitCount =
                Physics.RaycastNonAlloc(
                    camPos,
                    direction,
                    m_HitBuffer,
                    distance,
                    m_ObstacleMask,
                    QueryTriggerInteraction.Ignore
                );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider =
                    m_HitBuffer[i].collider;

                if (hitCollider == null)
                {
                    continue;
                }

                // ヒットしたオブジェクト以下の全レンダラーを対象にする
                // （岩モデルが複数パーツに分かれている場合にも対応）
                Renderer[] renderers =
                    hitCollider.GetComponentsInChildren<Renderer>();

                for (int r = 0; r < renderers.Length; r++)
                {
                    m_OccludingThisFrame.Add(renderers[r]);
                }
            }
        }


        // =====================================================
        // ■ フェード状態の更新・適用
        // =====================================================

        private void UpdateFades()
        {
            // 今フレーム遮蔽していたレンダラーを登録
            foreach (Renderer rend in m_OccludingThisFrame)
            {
                if (rend == null)
                {
                    continue;
                }

                if (!m_FadeEntries.ContainsKey(rend))
                {
                    FadeEntry entry =
                        CreateFadeEntry(rend);

                    if (entry != null)
                    {
                        m_FadeEntries[rend] = entry;
                    }
                }
            }


            // 管理中の全レンダラーを更新
            m_PendingRemoval.Clear();

            foreach (KeyValuePair<Renderer, FadeEntry> kv in m_FadeEntries)
            {
                Renderer rend = kv.Key;
                FadeEntry entry = kv.Value;

                // 岩自体が破棄されているケースへの保険
                if (rend == null)
                {
                    m_PendingRemoval.Add(rend);
                    continue;
                }

                bool occludingNow =
                    m_OccludingThisFrame.Contains(rend);

                float targetBlend =
                    occludingNow ? 1f : 0f;

                float step =
                    m_FadeDuration > 0f
                        ? Time.deltaTime / m_FadeDuration
                        : 1f;

                entry.CurrentBlend =
                    Mathf.MoveTowards(
                        entry.CurrentBlend,
                        targetBlend,
                        step
                    );

                ApplyBlend(rend, entry);

                // 完全に元へ戻ったら管理対象から外す
                // （マテリアルも共有アセットへ戻り、負荷も無くなる）
                if (!occludingNow &&
                    entry.CurrentBlend <= 0f)
                {
                    m_PendingRemoval.Add(rend);
                }
            }

            for (int i = 0; i < m_PendingRemoval.Count; i++)
            {
                m_FadeEntries.Remove(m_PendingRemoval[i]);
            }
        }


        // =====================================================
        // ■ フェード開始準備
        // =====================================================
        //
        // 初めて遮蔽が検出されたレンダラーに対して、
        // ・元のマテリアル／影設定を保存
        // ・Transparent切り替えが可能ならマテリアルを切り替え
        // を行う
        //
        // =====================================================

        private FadeEntry CreateFadeEntry(Renderer rend)
        {
            Material[] sharedMats =
                rend.sharedMaterials;

            if (sharedMats == null ||
                sharedMats.Length == 0)
            {
                return null;
            }

            FadeEntry entry = new FadeEntry
            {
                OriginalSharedMaterials = sharedMats,
                OriginalShadowMode = rend.shadowCastingMode,
                CurrentBlend = 0f,
                SupportsTransparency = true
            };


            // .materials（複数形）へのアクセスで、この
            // レンダラー専用のマテリアルインスタンスが自動生成される
            // （共有アセット・他の同じ岩には影響しない）
            Material[] instancedMats =
                rend.materials;

            for (int i = 0; i < instancedMats.Length; i++)
            {
                Material mat = instancedMats[i];

                if (mat == null)
                {
                    continue;
                }

                // URP Lit / Simple Lit系のSurface Type切り替えに
                // 対応していないシェーダーだった場合は
                // 透明化を諦め、表示/非表示切り替えにフォールバックする
                if (!mat.HasProperty("_Surface"))
                {
                    entry.SupportsTransparency = false;
                    continue;
                }

                SetMaterialTransparent(mat);
            }

            return entry;
        }


        // =====================================================
        // ■ ブレンド値の適用
        // =====================================================

        private void ApplyBlend(Renderer rend, FadeEntry entry)
        {
            if (entry.SupportsTransparency)
            {
                float alpha =
                    Mathf.Lerp(
                        1f,
                        m_FadedAlpha,
                        entry.CurrentBlend
                    );

                Material[] mats =
                    rend.materials;

                for (int i = 0; i < mats.Length; i++)
                {
                    SetMaterialAlpha(mats[i], alpha);
                }

                // 半透明中は影だけ不自然に濃く残らないよう消す
                rend.shadowCastingMode =
                    entry.CurrentBlend > 0.01f
                        ? ShadowCastingMode.Off
                        : entry.OriginalShadowMode;

                // 完全に元へ戻ったら共有マテリアルへ戻し、
                // インスタンスを解放してバッチングも回復する
                if (entry.CurrentBlend <= 0f)
                {
                    rend.sharedMaterials =
                        entry.OriginalSharedMaterials;

                    rend.shadowCastingMode =
                        entry.OriginalShadowMode;
                }
            }
            else
            {
                // Transparent非対応シェーダー用フォールバック：
                // 半分を境に表示/非表示を切り替える（滑らかさは無いが確実に動く）
                rend.enabled =
                    entry.CurrentBlend < 0.5f;
            }
        }


        // =====================================================
        // ■ マテリアルをTransparent化する
        // =====================================================
        //
        // URP Lit / Simple Lit標準の「Surface Type」プロパティを
        // 実行時にOpaque→Transparentへ切り替える。
        // 各プロパティはHasPropertyで確認してから設定するため、
        // 対応していないシェーダーに対しては何もしない
        // （CreateFadeEntry側で事前にHasProperty("_Surface")を
        // 　確認しているため、ここに来る時点でTransparent系
        // 　プロパティの存在はほぼ保証されている）
        //
        // =====================================================

        private static void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
            mat.SetOverrideTag("RenderType", "Transparent");

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f); // 0 = Alpha
            }

            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha
                );
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha
                );
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0f);
            }

            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            mat.renderQueue =
                (int)RenderQueue.Transparent;
        }


        // =====================================================
        // ■ マテリアルの不透明度を設定する
        // =====================================================

        private static void SetMaterialAlpha(Material mat, float alpha)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            else if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
        }


        // =====================================================
        // ■ 無効化時：フェード中のものを全て復元
        // =====================================================
        //
        // ラウンド切り替え等でこのコンポーネントや
        // GameObjectがOFFになっても、岩が半透明・非表示の
        // まま残ってしまわないようにする安全策
        //
        // =====================================================

        private void OnDisable()
        {
            foreach (KeyValuePair<Renderer, FadeEntry> kv in m_FadeEntries)
            {
                Renderer rend = kv.Key;
                FadeEntry entry = kv.Value;

                if (rend == null)
                {
                    continue;
                }

                rend.sharedMaterials =
                    entry.OriginalSharedMaterials;

                rend.shadowCastingMode =
                    entry.OriginalShadowMode;

                rend.enabled = true;
            }

            m_FadeEntries.Clear();
        }
    }
}