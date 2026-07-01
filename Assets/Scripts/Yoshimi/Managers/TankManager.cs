using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

using UnityEngine;
using UnityEngine.UIElements;

namespace Tanks.Complete
{
    // =========================================================
    // ■ タンク管理クラス
    // =========================================================
    //
    // ・プレイヤー情報管理
    // ・入力設定
    // ・CPU設定
    // ・各コンポーネント管理
    // ・ラウンド開始時の初期化
    //
    // =========================================================

    [Serializable]
    public class TankManager
    {
        // =====================================================
        // ■ プレイヤー情報
        // =====================================================

        [Header("Player")]

        // プレイヤーカラー
        public Color m_PlayerColor = Color.white;

        // 出現位置
        public Transform m_SpawnPoint;

        // プレイヤー番号
        public int m_PlayerNumber = 1;

        // タンク本体
        public GameObject m_Instance;

        // 勝利数
        [HideInInspector]
        public int m_Wins;

        // CPU操作か
        public bool m_ComputerControlled;


        // =====================================================
        // ■ 入力設定
        // =====================================================

        /*
            1 = WASD + Space
            2 = Arrow + Enter
            3 = Gamepad1
            4 = Gamepad2
        */

        [Header("Control")]

        public int ControlIndex = 1;


        // =====================================================
        // ■ UI表示用
        // =====================================================

        // プレイヤー名カラー文字列
        [HideInInspector]
        public string m_ColoredPlayerText;


        // =====================================================
        // ■ 各コンポーネント
        // =====================================================

        private TankMovement m_Movement;

        private TankShooting m_Shooting;

        private InputReader m_InputReader;

        private TankAI m_AI;

        private GameObject m_CanvasGameObject;


        // =====================================================
        // ■ 初期設定
        // =====================================================

        public void Setup(GameManager manager)
        {
            // =================================================
            // デバッグ情報表示
            // =================================================

            Debug.Log(
                "Instance = " + m_Instance.name +
                "\nScene = " + m_Instance.scene.name +
                "\nValid = " + m_Instance.scene.IsValid()
            );

            if (m_Instance == null)
            {
                Debug.LogError(
                    "TankManager : m_Instance が設定されていません"
                );

                return;
            }


            // =================================================
            // 各コンポーネント取得
            // =================================================

            m_Movement =
                m_Instance.GetComponent<TankMovement>();

            m_Shooting =
                m_Instance.GetComponent<TankShooting>();

            m_InputReader =
                m_Instance.GetComponent<InputReader>();

            m_AI =
                m_Instance.GetComponent<TankAI>();


            // =================================================
            // Canvas取得
            // =================================================

            Canvas canvas =
                m_Instance.GetComponentInChildren<Canvas>();

            if (canvas != null)
            {
                m_CanvasGameObject =
                    canvas.gameObject;
            }
            else
            {
                Debug.LogWarning(
                    m_Instance.name +
                    " に Canvas が見つかりません"
                );
            }


            // =================================================
            // InputReader確認
            // =================================================

            if (m_InputReader == null)
            {
                Debug.LogError(
                    m_Instance.name +
                    " に InputReader がありません"
                );
            }


            // =====================================================
            // TankMovement設定
            // =====================================================

            if (m_Movement != null)
            {
                // CPU操作設定
                m_Movement.m_IsComputerControlled =
                    m_ComputerControlled;

                // プレイヤー番号設定
                m_Movement.m_PlayerNumber =
                    m_PlayerNumber;
            }


            // =====================================================
            // TankShooting設定
            // =====================================================

            if (m_Shooting != null)
            {
                m_Shooting.m_IsComputerControlled =
                    m_ComputerControlled;

                m_Shooting.enabled = true;
            }


            // =====================================================
            // 入力設定
            // =====================================================

            if (m_InputReader != null)
            {
                m_InputReader.ControlIndex = ControlIndex;
            }


            // =====================================================
            // CPU設定
            // =====================================================

            if (m_ComputerControlled)
            {
                // AI未取得なら追加
                if (m_AI == null)
                {
                    m_AI = m_Instance.GetComponent<TankAI>();

                    if (m_AI == null)
                        m_AI = m_Instance.AddComponent<TankAI>();
                }

                // AI初期化
                m_AI.Setup(manager);

                // AI有効化
                m_AI.enabled = true;
            }
            else
            {
                if (m_AI == null)
                    m_AI = m_Instance.GetComponent<TankAI>();

                if (m_AI != null)
                    m_AI.enabled = false;
            }


            // =================================================
            // プレイヤー名カラー設定
            // =================================================

            m_ColoredPlayerText =
                "<color=#" +
                ColorUtility.ToHtmlStringRGB(
                    m_PlayerColor
                ) +
                ">PLAYER " +
                m_PlayerNumber +
                "</color>";


            // =================================================
            // タンクカラー変更
            // =================================================

            #if UNITY_EDITOR
            Debug.Log(
                m_Instance.name +
                "  IsPersistent = " +
                UnityEditor.EditorUtility.IsPersistent(m_Instance)
            );
            #endif

            // TankColorという名前を含むマテリアルだけを
            // プレイヤーカラーに変更する。

            MeshRenderer[] renderers =
                m_Instance.GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];

                Material[] materials = renderer.materials;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material mat = materials[j];

                    if (mat == null)
                        continue;

                    // TankColorのみ色変更
                    if (mat.name.Contains("TankColor"))
                    {
                        mat.color = m_PlayerColor;
                    }
                }
            }


            // =================================================
            // デバッグ表示
            // =================================================

            Debug.Log(
                "Tank Setup 完了 : " +
                m_Instance.name +
                " / Player " +
                m_PlayerNumber +
                " / ControlIndex = " +
                ControlIndex +
                " / CPU = " +
                m_ComputerControlled
            );
        }


        // =====================================================
        // ■ 操作禁止
        // =====================================================

        public void DisableControl()
        {
            // 移動停止
            if (m_Movement != null)
            {
                m_Movement.enabled = false;
            }

            // 発射停止
            if (m_Shooting != null)
            {
                m_Shooting.enabled = false;
            }

            // AI停止
            if (m_ComputerControlled &&
                m_AI != null)
            {
                m_AI.enabled = false;
            }

            // UI非表示
            if (m_CanvasGameObject != null)
            {
                m_CanvasGameObject.SetActive(false);
            }
        }


        // =====================================================
        // ■ 操作許可
        // =====================================================

        public void EnableControl()
        {
            // 移動有効
            if (m_Movement != null)
            {
                m_Movement.enabled = true;
            }

            // 発射有効
            if (m_Shooting != null)
            {
                m_Shooting.enabled = true;
            }

            // AI再有効化
            if (m_ComputerControlled &&
                m_AI != null)
            {
                m_AI.enabled = true;
            }

            // UI表示
            if (m_CanvasGameObject != null)
            {
                m_CanvasGameObject.SetActive(true);
            }
        }


        // =====================================================
        // ■ ラウンド開始時リセット
        // =====================================================

        public void Reset()
        {
            // =================================================
            // SpawnPoint確認
            // =================================================

            if (m_SpawnPoint == null)
            {
                Debug.LogError(
                    "SpawnPoint が設定されていません"
                );

                return;
            }


            // =================================================
            // Tank確認
            // =================================================

            if (m_Instance == null)
            {
                Debug.LogError(
                    "Tank Instance が存在しません"
                );

                return;
            }


            // =================================================
            // 位置リセット
            // =================================================

            m_Instance.transform.position =
                m_SpawnPoint.position;


            // =================================================
            // 回転リセット
            // =================================================

            m_Instance.transform.rotation =
                m_SpawnPoint.rotation;


            // =================================================
            // 一度OFF
            // =================================================

            m_Instance.SetActive(false);


            // =================================================
            // ONに戻す
            // =================================================

            m_Instance.SetActive(true);
        }
    }


#if UNITY_EDITOR

    // =========================================================
    // ■ Inspector表示カスタム
    // =========================================================

    [CustomPropertyDrawer(typeof(TankManager))]

    public class TankManagerDrawer : PropertyDrawer
    {
        public override VisualElement
            CreatePropertyGUI(
                SerializedProperty property
            )
        {
            // =================================================
            // Root
            // =================================================

            VisualElement root =
                new VisualElement();


            // =================================================
            // SpawnPoint
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.m_SpawnPoint)
                    ),
                    "Spawn Point"
                )
            );


            // =================================================
            // Player Number
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.m_PlayerNumber)
                    ),
                    "Player Number"
                )
            );


            // =================================================
            // Tank Instance
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.m_Instance)
                    ),
                    "Tank Prefab"
                )
            );


            // =================================================
            // Player Color
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.m_PlayerColor)
                    ),
                    "Player Color"
                )
            );


            // =================================================
            // 入力番号
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.ControlIndex)
                    ),
                    "Control Index"
                )
            );


            return root;
        }
    }

#endif
}