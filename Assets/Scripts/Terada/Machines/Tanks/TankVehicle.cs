using Fusion;
using Tanks.Complete;
using UnityEngine;

public class TankVehicle : NetworkBehaviour, IVehicle
{
    [SerializeField] TankMove move;
    [SerializeField] TankShot shot;

    PlayerInputData currentInput;

    public override void Spawned()
    {
        shot.Initialize(Runner);
    }

    public override void FixedUpdateNetwork()
    {
        move.SetMoveInput(currentInput.Move);
        move.TickMove(Runner.DeltaTime);

        bool fire =
            currentInput.Buttons.IsSet(NetworkInputButtons.Fire);

        shot.SetFireInput(fire);
        shot.TickShoot(Runner.DeltaTime);
    }

    public void SetInput(PlayerInputData data)
    {
        currentInput = data;
    }

    public Transform GetTransform()
    {
        return transform;
    }
}
