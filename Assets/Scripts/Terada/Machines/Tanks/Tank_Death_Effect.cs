using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class Tank_Death_Effect : NetworkBehaviour
    {
        [SerializeField]
        Tank_Health health;

        [SerializeField]
        ParticleSystem explosion;

        [SerializeField]
        AudioSource explosionAudio;

        bool deadEffectPlayed;

        public override void Render()
        {
            if (deadEffectPlayed)
                return;

            if (!health.Dead)
                return;

            deadEffectPlayed = true;

            if (explosion != null)
            {
                explosion.transform.parent = null;
                explosion.Play();
            }

            if (explosionAudio != null)
            {
                explosionAudio.transform.parent = null;
                explosionAudio.Play();
            }
        }
    }
}