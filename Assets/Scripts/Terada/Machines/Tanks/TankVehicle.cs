//using Fusion;
//using Tanks.Complete;
//using UnityEngine;

//public class TankVehicle : NetworkBehaviour, IVehicle
//{
//    [SerializeField] TankMove move;
//    [SerializeField] TankShot shot;

//    PlayerInputData currentInput;

//    public override void Spawned()
//    {
//        shot.Initialize(Runner);
//        Debug.Log($"State = {Object.StateAuthority}" +
//                $"Input = {Object.InputAuthority}");;
//    }

//    public override void FixedUpdateNetwork()
//    {
//        move.SetMoveInput(currentInput.Move);

//        if (Object.HasStateAuthority)
//        {
//            move.TickMove(Runner.DeltaTime);
//        }

//        bool fire =
//            currentInput.Buttons.IsSet(NetworkInputButtons.Fire);

//        shot.SetFireInput(fire);

//        if (Object.HasStateAuthority)
//        {
//            shot.TickShoot(Runner.DeltaTime, true);
//        }
//        else        
//        {
//            shot.TickShoot(Runner.DeltaTime, false);
//        }
//    }

//    void LateUpdate()
//    {
//        Debug.Log($"Root={transform.position}");

//        var model = transform.Find("TankModel");

//        if (model != null)
//        {
//            Debug.Log($"Model={model.position}");
//        }
//    }

//    public void SetInput(PlayerInputData data)
//    {
//        currentInput = data;
//    }

//    public Transform GetTransform()
//    {
//        return transform;
//    }
//}


using Fusion;
using UnityEngine;

namespace Tanks.Complete
{
    public class TankVehicle : NetworkBehaviour, IVehicle
    {

        TankMove move;
        TankShot shot;
        InputState currentInput;

        public TankMove Move => move;
        public TankShot Shot => shot;

        void Awake()
        {
            move = GetComponent<TankMove>();
            shot = GetComponent<TankShot>();
        }

        public Transform GetTransform()
        {
            return transform;
        }
    }
}