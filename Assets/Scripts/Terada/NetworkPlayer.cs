using Fusion;
using Tanks.Complete;
using UnityEngine;

// プレイヤーの同期データ管理クラス.
public class NetworkPlayer : NetworkBehaviour
{
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    [Networked] public bool IsReady { get; set; }

    [Networked, OnChangedRender(nameof(OnVehicleChanged))]
    public NetworkObject VehicleObject { get; set; }

    [SerializeField] float interactDistance = 5f;

    // ローカルのプレイヤー取得用.
    public static NetworkPlayer LocalPlayer;

    private NetworkCharacterController charactorController;
    private CameraFollow cam;

    // 直前の入力状態保存用.
    private NetworkButtons previousButtons;

    // プレイヤー生成時の処理.
    public override void Spawned()
    {
        charactorController = GetComponent<NetworkCharacterController>();
        cam = Camera.main.GetComponent<CameraFollow>();

        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;
            
            if (VehicleObject!= null)
            {
                cam.SetTarget(VehicleObject.transform);
            }

            RPC_SetName("Player_" + Random.Range(100, 999));
        }
    }

    // ネットワーク更新ごとの処理.
    public override void FixedUpdateNetwork()
    {
        // ホストでなければ処理しない.
        if (!HasStateAuthority)
            return;
        // 入力処理.
        if (!GetInput(out PlayerInputData data))
            return;

        // 車にPlayerInputDataから取得した入力を渡す.
        VehicleObject
            ?.GetComponent<IVehicle>()
            ?.SetInput(data);

        var attackvehicle = VehicleObject
            ?.GetComponent<IAttackVehicle>();
        //VehicleObject
        //    ?.GetComponent<IAttackVehicle>();
        //?.SetAttackInput(data);

        // 攻撃車両の入力処理.
        if (attackvehicle != null)
        {
            var pressed = data.Buttons.GetPressed(previousButtons);
            var released = data.Buttons.GetReleased(previousButtons);

            if (pressed.IsSet(NetworkInputButtons.Fire))
            {
                attackvehicle.BeginCharge();
            }
            if (released.IsSet(NetworkInputButtons.Fire))
            {
                attackvehicle.ReleaseCharge();
            }

            previousButtons = data.Buttons;
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        if (InputManager.Instance.ConsumeInteractPressed())
        {
            Debug.Log("IInteract Pressed");
            TryInteract();
        }
    }

    void OnVehicleChanged()
    {
        if (!Object.HasInputAuthority)
            return;

        if (VehicleObject == null)
            return;

        cam.SetTarget(VehicleObject.transform);

        var tankVehicle = VehicleObject.GetComponent<TankVehicle>();
        TankAimUI.Instance?.SetVehicleShot(tankVehicle);
    }

    void TryInteract()
    {
        if (VehicleObject == null)
            return;

        Vector3 origin = VehicleObject.transform.position + Vector3.up;
        Vector3 dir = VehicleObject.transform.forward;

        Debug.DrawRay(origin, dir * interactDistance, Color.red, 5f);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, interactDistance))
        {
            Debug.Log("Hit : " + hit.collider.name);

            var interactable =
                hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                Debug.Log("Interact!");

                interactable.Interact(this);
            }
        }
        else
        {
            Debug.Log("Nothing Hit");
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
        Vector3 spawnPos = transform.position;

        // 現在の車を削除.
        if (VehicleObject != null)
        {
            spawnPos = VehicleObject.transform.position;
            Runner.Despawn(VehicleObject);
        }

        // データベースからIDを取得.
        var prefab = VehicleDatabase.Instance.GetPrefab(vehicleID);

        // 新しく生成し、Object を更新.
        var newVehicle = Runner.Spawn(
            prefab,
            spawnPos,
            Quaternion.identity,
            Object.InputAuthority
        );
        // Networked同期.
        VehicleObject = newVehicle;
    }
}