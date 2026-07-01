using UnityEngine;

namespace Tanks.Complete
{
    public class Tank_Invincible_Effect : MonoBehaviour
    {
        [SerializeField]
        Tank_Health health;

        Renderer[] renderers;

        void Awake()
        {
            renderers =
                GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            bool visible = true;

            if (health.Invincible)
            {
                visible =
                    Mathf.Sin(Time.time * 20f) > 0;
            }

            foreach (Renderer r in renderers)
            {
                r.enabled = visible;
            }
        }
    }
}