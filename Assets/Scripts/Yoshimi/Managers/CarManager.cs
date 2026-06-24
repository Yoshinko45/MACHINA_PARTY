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
    // ■ 車管理クラス
    // =========================================================
    //
    // ・プレイヤー情報管理
    // ・入力設定
    // ・各コンポーネント管理
    // ・ラウンド開始時の初期化
    //
    // =========================================================

    [Serializable]
    public class CarManager
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

        // 車本体
        public GameObject m_Instance;

        // 勝利数
        [HideInInspector]
        public int m_Wins;


        // =====================================================
        // ■ 入力設定
        // =====================================================

        /*
            1 = WASD
            2 = Arrow
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

        private CarMovement m_Movement;

        private InputReader m_InputReader;

        private GameObject m_CanvasGameObject;


        // =====================================================
        // ■ 初期設定
        // =====================================================

        public void Setup()
        {
            // =================================================
            // インスタンス確認
            // =================================================

            if (m_Instance == null)
            {
                Debug.LogError(
                    "CarManager : m_Instance が設定されていません"
                );

                return;
            }

            Debug.Log(
                "Instance = " + m_Instance.name +
                "\nScene = " + m_Instance.scene.name +
                "\nValid = " + m_Instance.scene.IsValid()
            );


            // =================================================
            // 各コンポーネント取得
            // =================================================

            m_Movement =
                m_Instance.GetComponent<CarMovement>();

            m_InputReader =
                m_Instance.GetComponent<InputReader>();


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
            // CarMovement設定
            // =====================================================

            if (m_Movement != null)
            {
                // プレイヤー番号設定
                m_Movement.m_PlayerNumber =
                    m_PlayerNumber;
            }


            // =====================================================
            // 入力設定
            // =====================================================

            if (m_InputReader != null)
            {
                m_InputReader.ControlIndex = ControlIndex;
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
            // 車体カラー変更
            // =================================================

            // CarColorという名前を含むマテリアルだけを
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

                    // CarColorのみ色変更
                    if (mat.name.Contains("CarColor"))
                    {
                        mat.color = m_PlayerColor;
                    }
                }
            }


            // =================================================
            // デバッグ表示
            // =================================================

            Debug.Log(
                "Car Setup 完了 : " +
                m_Instance.name +
                " / Player " +
                m_PlayerNumber +
                " / ControlIndex = " +
                ControlIndex
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
            // インスタンス確認
            // =================================================

            if (m_Instance == null)
            {
                Debug.LogError(
                    "Car Instance が存在しません"
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
            // 一度OFF → ONに戻す
            // =================================================

            m_Instance.SetActive(false);
            m_Instance.SetActive(true);
        }
    }


#if UNITY_EDITOR

    // =========================================================
    // ■ Inspector表示カスタム
    // =========================================================

    [CustomPropertyDrawer(typeof(CarManager))]

    public class CarManagerDrawer : PropertyDrawer
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
                        nameof(CarManager.m_SpawnPoint)
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
                        nameof(CarManager.m_PlayerNumber)
                    ),
                    "Player Number"
                )
            );


            // =================================================
            // Car Instance
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(CarManager.m_Instance)
                    ),
                    "Car Prefab"
                )
            );


            // =================================================
            // Player Color
            // =================================================

            root.Add(
                new PropertyField(
                    property.FindPropertyRelative(
                        nameof(CarManager.m_PlayerColor)
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
                        nameof(CarManager.ControlIndex)
                    ),
                    "Control Index"
                )
            );


            return root;
        }
    }

#endif
}