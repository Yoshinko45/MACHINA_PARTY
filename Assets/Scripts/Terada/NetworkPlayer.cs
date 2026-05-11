using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using UnityEngine;


// プレイヤーの同期データ管理クラス.
public class NetworkPlayer : NetworkBehaviour
{
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    [Networked] public bool IsReady { get; set; }
    [Networked] public NetworkObject VehicleObject { get; set; }

    public static NetworkPlayer LocalPlayer;

    private NetworkCharacterController charactorController;
    //private IVehicle vehicle;

    bool _cameraSet;

    public override void Spawned()
    {
        charactorController = GetComponent<NetworkCharacterController>();

        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;
            
            RPC_SetName("Player_" + Random.Range(100, 999));
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasInputAuthority && VehicleObject != null)
        {
            if (!_cameraSet)
            {
                Camera.main.GetComponent<CameraFollow>()
                    .SetTarget(VehicleObject.transform);

                _cameraSet = true;
            }
        }

        if (GetInput(out PlayerInputData data))
        {
            var vehicle = VehicleObject?.GetComponent<IVehicle>();
            vehicle?.SetInput(data);

            //// ベクトル正規化.
            //data.Direction.Normalize();
            //// 入力方向を移動方向としてそのまま渡す.
            //charactorController.Move(data.Direction * Runner.DeltaTime * 5f);
            //if (data.Buttons.IsSet(NetworkInputButtons.Jump))
            //{
            //    charactorController.Jump();
            //}
        }
    }

    public void SetVehicle(NetworkObject vehicle)
    {
        VehicleObject = vehicle;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(string name)
    {
        PlayerName = name;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Ready(bool ready)
    {
        IsReady = ready;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ChangeVehicle(int vehicleID)
    {
        if (VehicleObject != null)
        {
            Runner.Despawn(VehicleObject);
        }

        var prefab = VehicleDatabase.Instance.Get(vehicleID);

        var newVehicle = Runner.Spawn(
            prefab,
            transform.position,
            Quaternion.identity,
            Object.InputAuthority
        );

        SetVehicle(newVehicle);
    }
}