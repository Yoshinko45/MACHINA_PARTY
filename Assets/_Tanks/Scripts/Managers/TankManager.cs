using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

using UnityEngine;
using UnityEngine.UIElements;

namespace Tanks.Complete
{
    [Serializable]
    public class TankManager
    {
        // =====================================================
        // ■ プレイヤー情報
        // =====================================================

        [HideInInspector]
        public Color m_PlayerColor;
        // プレイヤーカラー

        public Transform m_SpawnPoint;
        // 出現位置

        [HideInInspector]
        public int m_PlayerNumber;
        // プレイヤー番号

        [HideInInspector]
        public string m_ColoredPlayerText;
        // UI表示用文字列

        [HideInInspector]
        public GameObject m_Instance;
        // Tank本体

        [HideInInspector]
        public int m_Wins;
        // 勝利数

        [HideInInspector]
        public bool m_ComputerControlled;
        // CPU操作か


        // =====================================================
        // ■ 入力番号
        // =====================================================

        /*
            1 = WASD + Space
            2 = Arrow + Enter
            3 = Gamepad1
            4 = Gamepad2
        */

        [HideInInspector]
        public int ControlIndex = 1;


        // =====================================================
        // ■ 各コンポーネント参照
        // =====================================================

        private TankMovement m_Movement;

        private TankShooting m_Shooting;

        private InputReader m_InputReader;

        private GameObject m_CanvasGameObject;

        private TankAI m_AI;


        // =====================================================
        // ■ 初期設定
        // =====================================================

        public void Setup(GameManager manager)
        {
            // =================================================
            // コンポーネント取得
            // =================================================

            m_Movement =
                m_Instance.GetComponent<TankMovement>();

            m_Shooting =
                m_Instance.GetComponent<TankShooting>();

            m_InputReader =
                m_Instance.GetComponent<InputReader>();

            m_AI =
                m_Instance.GetComponent<TankAI>();

            m_CanvasGameObject =
                m_Instance
                .GetComponentInChildren<Canvas>()
                .gameObject;


            // =================================================
            // CPU設定
            // =================================================

            if (m_Movement != null)
            {
                m_Movement.m_IsComputerControlled =
                    m_ComputerControlled;
            }

            if (m_Shooting != null)
            {
                m_Shooting.m_IsComputerControlled =
                    m_ComputerControlled;
            }


            // =================================================
            // プレイヤー番号設定
            // =================================================

            if (m_Movement != null)
            {
                m_Movement.m_PlayerNumber =
                    m_PlayerNumber;
            }

            // =================================================
            // InputReaderへControlIndex設定
            // =================================================

            InputReader inputReader =
                m_Instance.GetComponent<InputReader>();

            if (inputReader != null)
            {
                inputReader.ControlIndex =
                    ControlIndex;
            }

            // =================================================
            // InputReaderへControlIndex設定
            // =================================================

            if (m_InputReader != null)
            {
                m_InputReader.ControlIndex =
                    ControlIndex;
            }


            // =================================================
            // CPUならAI追加
            // =================================================

            if (m_ComputerControlled)
            {
                // AIが無ければ追加
                if (m_AI == null)
                {
                    m_AI =
                        m_Instance.AddComponent<TankAI>();
                }

                // AIセットアップ
                m_AI.Setup(manager);
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

            MeshRenderer[] renderers =
                m_Instance.GetComponentsInChildren<MeshRenderer>();


            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer =
                    renderers[i];

                for (int j = 0;
                     j < renderer.materials.Length;
                     ++j)
                {
                    // TankColorマテリアルなら
                    if (renderer.materials[j]
                        .name
                        .Contains("TankColor"))
                    {
                        renderer.materials[j].color =
                            m_PlayerColor;
                    }
                }
            }
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

            // AI有効
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
            // 位置リセット
            m_Instance.transform.position =
                m_SpawnPoint.position;

            // 回転リセット
            m_Instance.transform.rotation =
                m_SpawnPoint.rotation;


            // 一度OFF
            m_Instance.SetActive(false);

            // ONに戻す
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
            // SpawnPointだけ表示
            var itemSlot =
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(TankManager.m_SpawnPoint)
                    )
                );

            return itemSlot;
        }
    }

#endif
}