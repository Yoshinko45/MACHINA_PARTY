using UnityEngine;
using Fusion;

namespace Tanks.Complete
{
    public class Tank_Move : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 12f;
        [SerializeField] float turnSpeed = 180f;

        [Header("Ground")]
        [SerializeField] float groundCheckDistance = 1.2f;
        [SerializeField] float slopeLimit = 45f;

        [Header("Audio")]
        [SerializeField] AudioSource movementAudio;
        [SerializeField] AudioClip engineIdle;
        [SerializeField] AudioClip engineDriving;
        [SerializeField] float pitchRange = 0.2f;

        Rigidbody rb;

        float originalPitch;

        float throttle;
        float steering;

        // =========================
        // Networked Velocity
        // =========================
        [Networked] Vector3 NetworkVelocity { get; set; }

        Vector3 explosionVelocity;

        public override void Spawned()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = false;

            if (movementAudio != null)
            {
                originalPitch = movementAudio.pitch;
                movementAudio.clip = engineIdle;
                movementAudio.loop = true;
                movementAudio.Play();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput(out PlayerInputData input))
            {
                throttle = input.Throttle;
                steering = input.Steering;
            }
            else
            {
                throttle = 0;
                steering = 0;
            }

            SimulateMovement(input);
        }

        public override void Render()
        {
            if (!Object.HasInputAuthority)
                return;

            EngineAudio();
        }

        // =====================================================
        // ■ メイン移動シミュレーション
        // =====================================================
        void SimulateMovement(PlayerInputData input)
        {
            float dt = Runner.DeltaTime;

            // -------------------------
            // 回転
            // -------------------------
            Quaternion deltaRot =
                Quaternion.Euler(0f, steering * turnSpeed * dt, 0f);

            rb.MoveRotation(rb.rotation * deltaRot);

            // -------------------------
            // 進行方向
            // -------------------------
            Vector3 forward = transform.forward;

            Vector3 desiredMove =
                forward * throttle * moveSpeed;

            // -------------------------
            // 爆風
            // -------------------------
            Vector3 knockback = explosionVelocity;

            // -------------------------
            // 合成速度（Networked）
            // -------------------------
            Vector3 velocity = desiredMove + knockback;

            // =========================
            // 坂・地面補正（スロープ対応）
            // =========================
            velocity = ProjectOnGround(velocity);

            // =========================
            // 壁押し返し（簡易）
            // =========================
            velocity = ResolveWallCollision(velocity, dt);

            // =========================
            // Network状態更新（重要）
            // =========================
            NetworkVelocity = velocity;

            // -------------------------
            // 移動
            // -------------------------
            rb.MovePosition(rb.position + velocity * dt);

            // -------------------------
            // 減衰
            // -------------------------
            explosionVelocity =
                Vector3.Lerp(explosionVelocity, Vector3.zero, dt * 5f);
        }

        // =====================================================
        // ■ 地面スロープ追従
        // =====================================================
        Vector3 ProjectOnGround(Vector3 velocity)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);

                if (angle <= slopeLimit)
                {
                    // 地面に沿わせる
                    velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
                }
            }

            return velocity;
        }

        // =====================================================
        // ■ 壁押し返し（簡易Raycast）
        // =====================================================
        Vector3 ResolveWallCollision(Vector3 velocity, float dt)
        {
            if (velocity.sqrMagnitude < 0.0001f)
                return velocity;

            Vector3 dir = velocity.normalized;
            float dist = velocity.magnitude * dt;

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, dist + 0.2f))
            {
                // 壁に沿わせる
                Vector3 slide = Vector3.ProjectOnPlane(velocity, hit.normal);
                return slide;
            }

            return velocity;
        }

        // =====================================================
        // ■ 爆風
        // =====================================================
        public void AddExplosionForce(
            float explosionForce,
            Vector3 explosionPosition,
            float explosionRadius)
        {
            Vector3 dir = transform.position - explosionPosition;
            dir.y = 0;

            float dist = dir.magnitude;
            if (dist < 0.001f)
                return;

            dir.Normalize();

            float attenuation =
                1f - Mathf.Clamp01(dist / explosionRadius);

            explosionVelocity += dir * explosionForce * attenuation;
        }

        // =====================================================
        // ■ 音
        // =====================================================
        void EngineAudio()
        {
            if (movementAudio == null)
                return;

            bool isMoving =
                Mathf.Abs(throttle) > 0.1f ||
                Mathf.Abs(steering) > 0.1f;

            if (isMoving)
            {
                if (movementAudio.clip != engineDriving)
                {
                    movementAudio.clip = engineDriving;
                    movementAudio.pitch =
                        Random.Range(originalPitch - pitchRange,
                                     originalPitch + pitchRange);
                    movementAudio.Play();
                }
            }
            else
            {
                if (movementAudio.clip != engineIdle)
                {
                    movementAudio.clip = engineIdle;
                    movementAudio.pitch =
                        Random.Range(originalPitch - pitchRange,
                                     originalPitch + pitchRange);
                    movementAudio.Play();
                }
            }
        }
    }
}