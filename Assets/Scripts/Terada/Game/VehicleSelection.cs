using Fusion;

// プレイヤーが選択した車両の内容を保持.
namespace Tanks.Complete
{
    public class VehicleSelection : NetworkBehaviour
    {
        [Networked] public VehicleType TankVehicle { get; set; }

        [Networked] public VehicleType RaceVehicle { get; set; }

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                TankVehicle = VehicleType.NormalTank;
                RaceVehicle = VehicleType.RaceCar;
            }
        }
    }
}