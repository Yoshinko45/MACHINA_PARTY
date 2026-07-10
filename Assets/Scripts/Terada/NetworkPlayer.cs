using Fusion;
using Tanks.Complete;
using UnityEngine;

// プレイヤーの同期データ管理クラス.
public class NetworkPlayer : NetworkBehaviour
{
    // プレイヤー名.
    [Networked] public NetworkString<_16> PlayerName { get; set; }

    // Room内でのプレイヤーごとの状態（全員Readyでゲームスタートできる）.
    [Networked, OnChangedRender(nameof(OnReadyChanged))]
    public bool IsReady { get; set; }

    public NetworkObject VehicleObject { get; set; }

    [Networked] public VehicleType TankVehicleType { get; set; }
    [Networked] public VehicleType RaceVehicleType { get; set; }

    [Networked] public bool HasVoted { get; set; }
    [Networked] public GameModes Vote { get; set; }

    [SerializeField] float interactDistance = 5f;

    [Networked] public int Wins { get; set; }

    // ローカルのプレイヤー取得用.
    public static NetworkPlayer LocalPlayer;

    //private NetworkCharacterController charactorController;
    private CameraFollow cam;

    // 直前の入力状態保存用.
    private NetworkButtons previousButtons;

    // プレイヤー生成時の処理.
    public override void Spawned()
    {
        //charactorController = GetComponent<NetworkCharacterController>();
        cam = Camera.main.GetComponent<CameraFollow>();

        if (Object.HasStateAuthority)
        {
            RPC_SetName("Host_" + Random.Range(10, 99));

            HasVoted = false;
            IsReady = false;

            // ルームに入ったらデフォルトで最初の車両を登録.
            TankVehicleType = VehicleType.NormalTank;
            RaceVehicleType = VehicleType.RaceCar;
        }

        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;
            
            if (VehicleObject!= null)
            {
                cam.SetTarget(VehicleObject.transform);
            }

            RPC_SetName("Player_" + Random.Range(100, 999));

            PlayerListManager.Instance?.Refresh();
        }

        UpdateCameraTarget();

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

        var attackvehicle = VehicleObject
            ?.GetComponent<IAttackVehicle>();

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

    void OnReadyChanged()
    {
        PlayerListManager.Instance?.Refresh();
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

    public VehicleType GetVehicle(GameModes mode)
    {
        return mode == GameModes.Tank
            ? TankVehicleType
            : RaceVehicleType;
    }

    public void SpawnVehicle(GameModes mode)
    {
        Debug.Log($"Mode = {mode}");
        if (!Object.HasStateAuthority)
            return;

        var type = GetVehicle(mode);

        var prefab =
            VehicleDatabase.Instance.GetPrefab(type);

        VehicleObject = Runner.Spawn(
            prefab,
            Vector3.zero,
            Quaternion.identity,
            Object.InputAuthority
        );
    }

    void OnVehicleChanged()
    {
        UpdateCameraTarget();

        if (VehicleObject != null)
        {
            var tankVehicle = VehicleObject.GetComponent<Tank_Vehicle>();

            if (tankVehicle != null)
                TankAimUI.Instance?.SetVehicleShot(tankVehicle);

            //cam.SetTarget(tankVehicle.Move.transform);

        }

    }

    // ゲームモードに合わせて車両を変更.
    // ゲーム開始時に使う.
    void ChangeCurrentVehicle(GameModes gameMode)
    {
        Vector3 spawnPos = Vector3.zero;

        Debug.Log($"[ChangeCurrentVehicle] mode={gameMode}");

        if (VehicleObject != null)
        {
            Debug.Log("old vehicle exists");
            spawnPos = VehicleObject.transform.position;
            spawnPos.y = 0f;
            Runner.Despawn(VehicleObject);
        }

        VehicleType type = GetVehicle(gameMode);

        var prefab =
            VehicleDatabase.Instance.GetPrefab(type);

        VehicleObject = Runner.Spawn(
            prefab,
            spawnPos,
            Quaternion.identity,
            Object.InputAuthority);
    }

    public void SetVehicle(NetworkObject vehicle)
    {
        VehicleObject = vehicle;
    }

    void UpdateCameraTarget()
    {
        if (!Object.HasInputAuthority) return;

        if (VehicleObject == null) return;

        if (cam == null)
            cam = Camera.main?.GetComponent<CameraFollow>();

        if (cam == null) return;

        cam.SetTarget(VehicleObject.transform);
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
    public void RPC_Vote(GameModes mode)
    {
        Vote = mode;
        HasVoted = true;
    }

    /// <summary>
    /// 車両変更用関数.
    /// </summary>
    /// <param name="gameMode"></param>
    /// <param name="type"></param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ChangeVehicle(GameModes gameMode, VehicleType type)
    {
        switch (gameMode)
        {
            case GameModes.Tank:
                TankVehicleType = type;
                break;
            case GameModes.Race:
                RaceVehicleType = type;
                break;
        }

        ChangeCurrentVehicle(gameMode);
    }

}