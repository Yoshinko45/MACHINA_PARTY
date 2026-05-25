using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Tanks.Complete
{
    /// <summary>
    /// CPUタンク用AI
    ///
    /// ・最も近い敵を探す
    /// ・NavMeshで追跡
    /// ・射程内なら攻撃
    /// ・近づきすぎたら逃走
    /// </summary>
    public class TankAI : MonoBehaviour
    {
        // =====================================================
        // ■ AI状態
        // =====================================================

        enum State
        {
            // 敵を追いかける
            Seek,

            // 敵から逃げる
            Flee
        }


        // =====================================================
        // ■ コンポーネント参照
        // =====================================================

        // 移動処理
        private TankMovement m_Movement;

        // 射撃処理
        private TankShooting m_Shooting;

        // 実際の移動処理
        private CharacterController m_Controller;


        // =====================================================
        // ■ 経路探索関連
        // =====================================================

        // 経路再計算間隔
        private float m_PathfindTime = 0.5f;

        // 経路探索タイマー
        private float m_PathfindTimer = 0.0f;

        // 現在の移動経路
        private NavMeshPath m_CurrentPath = null;

        // 現在向かっているCorner番号
        private int m_CurrentCorner = 0;

        // 移動中か
        private bool m_IsMoving = false;


        // =====================================================
        // ■ ターゲット関連
        // =====================================================

        // 現在狙っている敵
        private Transform m_CurrentTarget = null;

        // 全タンク一覧
        private GameObject[] m_AllTanks;

        // 前回の敵位置
        private Vector3 m_LastTargetPosition;

        // 敵が停止している時間
        private float m_TimeSinceLastTargetMove;

        // 敵に近づいている時間
        private float m_TimeCloseToTarget;


        // =====================================================
        // ■ 射撃関連
        // =====================================================

        // 最大射程距離
        private float m_MaxShootingDistance = 0.0f;

        // 次弾発射までの時間
        private float m_TimeBetweenShot = 2.0f;

        // 射撃クールタイム
        private float m_ShotCooldown = 0.0f;


        // =====================================================
        // ■ 逃走関連
        // =====================================================

        // 前回の逃走位置
        private Vector3 m_FleeingLastPosition;

        // 停止時間
        private float m_SinceLastFleeingMove = 0.0f;


        // =====================================================
        // ■ AI状態
        // =====================================================

        // 現在の状態
        private State m_CurrentState = State.Seek;


        // =====================================================
        // ■ Awake
        // =====================================================

        private void Awake()
        {
            // 無効状態なら処理しない
            if (!isActiveAndEnabled)
                return;


            // =================================================
            // コンポーネント取得
            // =================================================

            m_Movement =
                GetComponent<TankMovement>();

            m_Shooting =
                GetComponent<TankShooting>();

            m_Controller =
                GetComponent<CharacterController>();


            // =================================================
            // CPU操作として設定
            // =================================================

            m_Movement.m_IsComputerControlled = true;

            m_Shooting.m_IsComputerControlled = true;


            // =================================================
            // 経路探索更新時間をランダム化
            // CPU全員が同時更新しないようにする
            // =================================================

            m_PathfindTime =
                Random.Range(0.3f, 0.6f);


            // =================================================
            // 最大射程距離を計算
            // =================================================

            m_MaxShootingDistance =
                Vector3.Distance(
                    m_Shooting.GetProjectilePosition(1.0f),
                    transform.position
                );


            // =================================================
            // シーン内の全タンク取得
            // =================================================

            m_AllTanks =
                FindObjectsByType<TankMovement>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                )
                .Select(e => e.gameObject)
                .ToArray();
        }


        // =====================================================
        // ■ GameManagerからセットアップ
        // =====================================================

        public void Setup(GameManager manager)
        {
            // SpawnPointからタンク一覧取得
            m_AllTanks =
                manager.m_SpawnPoints
                .Select(e => e.m_Instance)
                .ToArray();
        }


        // =====================================================
        // ■ AI停止
        // =====================================================

        public void TurnOff()
        {
            enabled = false;
        }


        // =====================================================
        // ■ Update
        // =====================================================

        void Update()
        {
            // =================================================
            // 射撃クールタイム減少
            // =================================================

            if (m_ShotCooldown > 0)
            {
                m_ShotCooldown -= Time.deltaTime;
            }


            // =================================================
            // 経路探索タイマー更新
            // =================================================

            m_PathfindTimer += Time.deltaTime;


            // =================================================
            // 状態別更新
            // =================================================

            switch (m_CurrentState)
            {
                case State.Seek:
                    SeekUpdate();
                    break;

                case State.Flee:
                    FleeUpdate();
                    break;
            }
        }


        // =====================================================
        // ■ 敵追跡状態
        // =====================================================

        void SeekUpdate()
        {
            // =================================================
            // 一定時間ごとに経路再探索
            // =================================================

            if (m_PathfindTimer > m_PathfindTime)
            {
                m_PathfindTimer = 0;

                NavMeshPath[] paths =
                    new NavMeshPath[m_AllTanks.Length];

                // 最短経路長
                float shortestPath = float.MaxValue;

                // 使用するPath番号
                int usedPath = -1;

                // 最終ターゲット
                Transform target = null;


                // =================================================
                // 全タンク調査
                // =================================================

                for (var i = 0; i < m_AllTanks.Length; i++)
                {
                    var tank = m_AllTanks[i].gameObject;

                    // 自分自身は無視
                    if (tank == gameObject)
                        continue;

                    // 無効オブジェクトは無視
                    if (tank == null ||
                        !tank.activeInHierarchy)
                        continue;

                    paths[i] = new NavMeshPath();


                    // =================================================
                    // 経路計算
                    // =================================================

                    if (NavMesh.CalculatePath(
                            transform.position,
                            tank.transform.position,
                            ~0,
                            paths[i]))
                    {
                        float length =
                            GetPathLength(paths[i]);


                        // =================================================
                        // 最短経路更新
                        // =================================================

                        if (shortestPath > length)
                        {
                            usedPath = i;

                            shortestPath = length;

                            target = tank.transform;
                        }
                    }
                }


                // =================================================
                // ターゲット決定
                // =================================================

                if (usedPath != -1)
                {
                    // ターゲット変更時
                    if (target != m_CurrentTarget)
                    {
                        m_CurrentTarget = target;

                        m_LastTargetPosition =
                            m_CurrentTarget.position;
                    }

                    m_CurrentTarget = target;

                    // 現在の経路更新
                    m_CurrentPath = paths[usedPath];

                    // Corner0は現在位置なので1から開始
                    m_CurrentCorner = 1;

                    m_IsMoving = true;
                }
            }


            // =================================================
            // ターゲット存在確認
            // =================================================

            if (m_CurrentTarget != null)
            {
                // =================================================
                // 敵移動量確認
                // =================================================

                float targetMovement =
                    Vector3.Distance(
                        m_CurrentTarget.position,
                        m_LastTargetPosition
                    );


                // 敵が止まっている
                if (targetMovement < 0.001f)
                {
                    m_TimeSinceLastTargetMove +=
                        Time.deltaTime;
                }

                // 敵が移動した
                else
                {
                    m_TimeSinceLastTargetMove = 0;
                }

                m_LastTargetPosition =
                    m_CurrentTarget.position;


                // =================================================
                // ターゲット方向
                // =================================================

                Vector3 toTarget =
                    m_CurrentTarget.position -
                    transform.position;

                toTarget.y = 0;

                // 距離
                float targetDistance =
                    toTarget.magnitude;

                // 正規化
                toTarget.Normalize();


                // =================================================
                // 近づきすぎたら逃走
                // =================================================

                if (targetDistance < 3.0f)
                {
                    m_TimeCloseToTarget +=
                        Time.deltaTime;

                    // 2秒近距離維持で逃走
                    if (m_TimeCloseToTarget > 2.0f)
                    {
                        StartFleeing();
                        return;
                    }
                }
                else
                {
                    m_TimeCloseToTarget = 0.0f;
                }


                // =================================================
                // 正面向き判定
                // =================================================

                float dotToTarget =
                    Vector3.Dot(
                        toTarget,
                        transform.forward
                    );


                // =================================================
                // チャージ中
                // =================================================

                if (m_Shooting.IsCharging)
                {
                    // 現在の着弾位置取得
                    Vector3 currentShotTarget =
                        m_Shooting.GetProjectilePosition(
                            m_Shooting.CurrentChargeRatio
                        );

                    // 現在の射程距離
                    float currentShotDistance =
                        Vector3.Distance(
                            currentShotTarget,
                            transform.position
                        );


                    // =================================================
                    // 射程到達 ＆ 正面なら発射
                    // =================================================

                    if (currentShotDistance >= targetDistance - 2 &&
                        dotToTarget > 0.99f)
                    {
                        // 一旦停止
                        m_IsMoving = false;

                        // 発射
                        m_Shooting.StopCharging();

                        // クールタイム開始
                        m_ShotCooldown =
                            m_TimeBetweenShot;


                        // 敵が止まり続けていたら逃走
                        if (m_TimeSinceLastTargetMove > 2.0f)
                        {
                            StartFleeing();
                        }
                    }
                }


                // =================================================
                // 非チャージ中
                // =================================================

                else
                {
                    // 射程内
                    if (targetDistance < m_MaxShootingDistance)
                    {
                        // =================================================
                        // 壁チェック
                        // Raycast成功=falseなら壁無し
                        // =================================================

                        if (!NavMesh.Raycast(
                                transform.position,
                                m_CurrentTarget.position,
                                out var hit,
                                ~0))
                        {
                            // 停止して狙う
                            m_IsMoving = false;

                            // クールタイム終了ならチャージ開始
                            if (m_ShotCooldown <= 0.0f)
                            {
                                m_Shooting.StartCharging();
                            }
                        }
                    }
                }
            }
        }


        // =====================================================
        // ■ 逃走状態
        // =====================================================

        private void FleeUpdate()
        {
            // =================================================
            // 経路終了なら追跡へ戻る
            // =================================================

            if (m_CurrentCorner >=
                m_CurrentPath.corners.Length)
            {
                m_CurrentState = State.Seek;
            }


            // =================================================
            // 移動量確認
            // =================================================

            var distance =
                (transform.position -
                 m_FleeingLastPosition).magnitude;

            m_FleeingLastPosition =
                transform.position;


            // 停止している
            if (distance < 0.001f)
            {
                m_SinceLastFleeingMove +=
                    Time.deltaTime;
            }

            // 動いている
            else
            {
                m_SinceLastFleeingMove = 0;
            }


            // =================================================
            // 詰まったら再逃走
            // =================================================

            if (m_SinceLastFleeingMove > 2.0f)
            {
                StartFleeing();
            }
        }


        // =====================================================
        // ■ 逃走開始
        // =====================================================

        private void StartFleeing()
        {
            m_FleeingLastPosition =
                transform.position;

            m_SinceLastFleeingMove = 0.0f;


            // =================================================
            // 敵方向取得
            // =================================================

            var toTarget =
                (m_CurrentTarget.position -
                 transform.position).normalized;


            // =================================================
            // 左右ランダム方向へ回転
            // =================================================

            toTarget =
                Quaternion.AngleAxis(
                    Random.Range(90.0f, 180.0f) *
                    Mathf.Sign(Random.Range(-1.0f, 1.0f)),
                    Vector3.up
                ) * toTarget;


            // =================================================
            // 逃走距離ランダム
            // =================================================

            toTarget *=
                Random.Range(5.0f, 20.0f);


            // =================================================
            // 逃走経路生成
            // =================================================

            if (NavMesh.CalculatePath(
                    transform.position,
                    transform.position + toTarget,
                    NavMesh.AllAreas,
                    m_CurrentPath))
            {
                m_CurrentState = State.Flee;

                m_CurrentCorner = 1;

                m_IsMoving = true;
            }
        }


        // =====================================================
        // ■ FixedUpdate
        // =====================================================

        private void FixedUpdate()
        {
            // 経路無し
            if (m_CurrentPath == null ||
                m_CurrentPath.corners.Length == 0)
                return;


            // =================================================
            // 現在向かう地点
            // =================================================

            Vector3 orientTarget =
                m_CurrentPath.corners[
                    Mathf.Min(
                        m_CurrentCorner,
                        m_CurrentPath.corners.Length - 1
                    )
                ];


            // 移動停止中は敵方向を見る
            if (!m_IsMoving)
            {
                orientTarget =
                    m_CurrentTarget.position;
            }


            // =================================================
            // 向く方向
            // =================================================

            Vector3 toOrientTarget =
                orientTarget - transform.position;

            toOrientTarget.y = 0;

            toOrientTarget.Normalize();


            // 現在前方向
            Vector3 forward =
                transform.forward;


            // 正面向き具合
            float orientDot =
                Vector3.Dot(
                    forward,
                    toOrientTarget
                );


            // 必要回転角度
            float rotatingAngle =
                Vector3.SignedAngle(
                    forward,
                    toOrientTarget,
                    Vector3.up
                );


            // =================================================
            // 移動処理
            // =================================================

            float moveAmount =
                Mathf.Clamp01(orientDot) *
                m_Movement.m_Speed;


            if (m_IsMoving &&
                moveAmount > 0.000001f)
            {
                Vector3 move =
                    forward *
                    moveAmount *
                    Time.deltaTime;

                m_Controller.Move(move);
            }


            // =================================================
            // 回転処理
            // =================================================

            rotatingAngle =
                Mathf.Sign(rotatingAngle) *
                Mathf.Min(
                    Mathf.Abs(rotatingAngle),
                    m_Movement.m_TurnSpeed *
                    Time.deltaTime
                );


            if (Mathf.Abs(rotatingAngle) > 0.000001f)
            {
                transform.rotation *=
                    Quaternion.AngleAxis(
                        rotatingAngle,
                        Vector3.up
                    );
            }


            // =================================================
            // Corner到達判定
            // =================================================

            if (Vector3.Distance(
                    transform.position,
                    orientTarget) < 0.5f)
            {
                m_CurrentCorner += 1;
            }
        }


        // =====================================================
        // ■ 経路長計算
        // =====================================================

        float GetPathLength(NavMeshPath path)
        {
            float dist = 0;


            // Corner同士の距離を合計
            for (var i = 1;
                 i < path.corners.Length;
                 ++i)
            {
                dist += Vector3.Distance(
                    path.corners[i - 1],
                    path.corners[i]
                );
            }

            return dist;
        }
    }
}