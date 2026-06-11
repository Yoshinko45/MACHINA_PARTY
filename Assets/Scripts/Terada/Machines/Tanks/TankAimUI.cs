using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankAimUI : MonoBehaviour
    {
        public static TankAimUI Instance;

        [SerializeField] Slider chargeSlider;
        [SerializeField] ProjectileMarker marker;

        TankShot currentShot;

        private void Awake()
        {
           if (Instance != null && Instance != this)
           {
                Destroy(gameObject);
                return;
           }
           Instance = this;
        }

        public void SetVehicleShot(TankVehicle vehicle)
        {
            currentShot = vehicle != null ? vehicle.Shot : null;

            marker.Initialize(currentShot);
        }

        void Update()
        {
            if (currentShot == null) return;

            chargeSlider.value = currentShot.ChargeRatio;
        }
    }
}