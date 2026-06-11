using UnityEngine;

namespace Tanks.Complete
{
    public class ProjectileMarker : MonoBehaviour
    {
        TankShot currentShot;

        public void Initialize(TankShot shot)
        {
            currentShot = shot;
        }

        void Update()
        {
            if (currentShot == null) return;

            if (!currentShot.IsCharging)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.position = currentShot.GetProjectilePosition();
        }
    }
}