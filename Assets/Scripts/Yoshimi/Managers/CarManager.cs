using UnityEngine;

namespace Tanks.Complete
{
    // =========================================================
    // ■ CarManager
    // =========================================================
    //
    // レースゲーム専用管理クラス
    //
    // ・CarMovement管理
    // ・InputReader管理
    // ・プレイヤー番号設定
    // ・CPU設定
    // ・リスポーン
    // ・Enable / Disable
    //
    // =========================================================

    public class CarManager : MonoBehaviour
    {
        // =====================================================
        // ■ プレイヤー情報
        // =====================================================

        [Header("Player")]

        public int m_PlayerNumber = 1;

        public bool m_IsComputerControlled = false;

        public Transform m_SpawnPoint;


        // =====================================================
        // ■ コンポーネント
        // =====================================================

        private CarMovement m_Movement;

        private InputReader m_InputReader;

        private Rigidbody m_Rigidbody;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            m_Movement =
                GetComponent<CarMovement>();

            m_InputReader =
                GetComponent<InputReader>();

            m_Rigidbody =
                GetComponent<Rigidbody>();
        }


        // =====================================================
        // ■ Start
        // =====================================================

        private void Start()
        {
            Setup();
        }


        // =====================================================
        // ■ 初期設定
        // =====================================================

        public void Setup()
        {
            if (m_Movement != null)
            {
                m_Movement.m_PlayerNumber =
                    m_PlayerNumber;

                m_Movement.m_IsComputerControlled =
                    m_IsComputerControlled;
            }

            if (m_InputReader != null)
            {
                m_InputReader.ControlIndex =
                    m_PlayerNumber;

                m_InputReader.m_InputProfile =
                    InputReader.InputProfile.Race;
            }
        }


        // =====================================================
        // ■ 操作禁止
        // =====================================================

        public void DisableControl()
        {
            if (m_Movement != null)
            {
                m_Movement.enabled = false;
            }
        }


        // =====================================================
        // ■ 操作許可
        // =====================================================

        public void EnableControl()
        {
            if (m_Movement != null)
            {
                m_Movement.enabled = true;
            }
        }


        // =====================================================
        // ■ リスポーン
        // =====================================================

        public void ResetCar()
        {
            if (m_SpawnPoint == null)
                return;

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            transform.position =
                m_SpawnPoint.position;

            transform.rotation =
                m_SpawnPoint.rotation;
        }


        // =====================================================
        // ■ チェックポイント復帰
        // =====================================================

        public void RespawnAtCheckpoint(
            Transform checkpoint)
        {
            if (checkpoint == null)
                return;

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            transform.position =
                checkpoint.position;

            transform.rotation =
                checkpoint.rotation;
        }


        // =====================================================
        // ■ 現在速度取得
        // =====================================================

        public float GetSpeed()
        {
            if (m_Movement == null)
                return 0f;

            return m_Movement.CurrentSpeed;
        }


        // =====================================================
        // ■ ドリフト中
        // =====================================================

        public bool IsDrifting()
        {
            if (m_Movement == null)
                return false;

            return m_Movement.IsDrifting;
        }


        // =====================================================
        // ■ ブースト中
        // =====================================================

        public bool IsBoosting()
        {
            if (m_Movement == null)
                return false;

            return m_Movement.IsBoosting;
        }


        // =====================================================
        // ■ ブースト開始
        // =====================================================

        public void StartBoost()
        {
            if (m_Movement != null)
            {
                m_Movement.StartBoost();
            }
        }
    }
}