using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class Tank_Vehicle : NetworkBehaviour, IVehicle
    {
        Tank_Move move;
        TankShot shot;

        public Tank_Move Move => move;
        public TankShot Shot => shot;

        void Awake()
        {
            move = GetComponent<Tank_Move>();
            shot = GetComponent<TankShot>();
        }

        public Transform GetTransform()
        {
            return transform;
        }
    }
}