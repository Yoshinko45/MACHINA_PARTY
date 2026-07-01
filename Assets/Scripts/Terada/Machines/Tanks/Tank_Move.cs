using UnityEngine;
using Fusion;

namespace Tanks.Complete
{
    public class Tank_Move : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 12;
        [SerializeField] float turnSpeed = 180;

        [Header("Audio")]
        [SerializeField] AudioSource movementAudio;
        [SerializeField] AudioClip engineIdle;
        [SerializeField] AudioClip engineDriving;
        [SerializeField] float pitchRange = 0.2f;

        float originalPitch;

        float throttle;
        float steering;

        Vector3 explosionVelocity;

        public override void Spawned()
        {
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

                Move(input);
            }
            else
            {
                throttle = 0;
                steering = 0;
            }
        }

        public override void Render()
        {
            if (!Object.HasInputAuthority)
                return;

            EngineAudio();
        }

        void Move(PlayerInputData input)
        {
            transform.Rotate(
                0,
                input.Steering * turnSpeed * Runner.DeltaTime,
                0);

            transform.position +=
                transform.forward *
                input.Throttle *
                moveSpeed *
                Runner.DeltaTime;

            transform.position +=
                explosionVelocity *
                Runner.DeltaTime;

            explosionVelocity =
                Vector3.Lerp(
                    explosionVelocity,
                    Vector3.zero,
                    Runner.DeltaTime * 5f);
        }

        public void AddExplosionForce(
            float explosionForce,
            Vector3 explosionPosition,
            float explosionRadius)
        {
            Vector3 dir =
                transform.position - explosionPosition;

            dir.y = 0f;

            float distance = dir.magnitude;

            dir.Normalize();

            float attenuation =
                1f - Mathf.Clamp01(distance / explosionRadius);

            transform.position +=
                dir *
                explosionForce *
                attenuation;
        }

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
                        Random.Range(
                            originalPitch - pitchRange,
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
                        Random.Range(
                            originalPitch - pitchRange,
                            originalPitch + pitchRange);

                    movementAudio.Play();
                }
            }
        }
    }
}
