using Fusion;
using UnityEngine;


// プレイヤーの同期データ管理クラス.
public class NetworkPlayer : NetworkBehaviour
{
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    [Networked] public bool IsReady { get; set; }
    [Networked] public NetworkObject VehicleObject { get; set; }

    // ローカルのプレイヤー取得用.
    public static NetworkPlayer LocalPlayer;

    private NetworkCharacterController charactorController;
    //private IVehicle vehicle;

    private CameraFollow cam;
    bool _cameraSet;

    // 生成時に設定.
    public override void Spawned()
    {
        charactorController = GetComponent<NetworkCharacterController>();
        cam = Camera.main.GetComponent<CameraFollow>();

        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;
            
            RPC_SetName("Player_" + Random.Range(100, 999));
        }
    }

    public override void FixedUpdateNetwork()
    {
        // カメラ追従用にターゲットを自身に設定.
        if (Object.HasInputAuthority && VehicleObject != null)
        {
            if (!_cameraSet)
            {
                //Camera.main.GetComponent<CameraFollow>()
                //    .SetTarget(VehicleObject.transform);
                cam.SetTarget(VehicleObject.transform);

                _cameraSet = true;
            }
        }

        // 入力処理.
        if (GetInput(out PlayerInputData data))
        {
            // 車にPlayerInputDataから取得した入力を渡す.
            var vehicle = VehicleObject?.GetComponent<IVehicle>();
            vehicle?.SetInput(data);
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

    /// <summary>
    /// 車変更用関数
    /// </summary>
    /// <param name="vehicleID"></param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ChangeVehicle(int vehicleID)
    {
        // 現在の車を削除.
        if (VehicleObject != null)
        {
            Runner.Despawn(VehicleObject);
        }

        // データベースからIDを取得.
        var prefab = VehicleDatabase.Instance.Get(vehicleID);

        // 新しく生成し、Object を更新.
        var newVehicle = Runner.Spawn(
            prefab,
            transform.position,
            Quaternion.identity,
            Object.InputAuthority
        );
        SetVehicle(newVehicle);
    }
}