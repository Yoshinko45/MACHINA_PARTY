using Fusion;
using Tanks.Complete;
using UnityEngine;

// プレイヤーの同期データ管理クラス.
public class NetworkPlayer : NetworkBehaviour
{
    // プレイヤー名.
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    // Room内でのプレイヤーごとの状態（全員Readyでゲームスタートできる）.
    [Networked] public bool IsReady { get; set; }

    [Networked, OnChangedRender(nameof(OnVehicleChanged))]
    public NetworkObject VehicleObject { get; set; }

    [Networked] public VehicleType TankVehicleType { get; set; }
    [Networked] public VehicleType RaceVehicleType { get; set; }

    [SerializeField] float interactDistance = 5f;

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
        }

        PlayerListManager.Instance?.Refresh();
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

    public void SpawnVehicle(GameModes mode)
    {
        Debug.Log($"GameSettings null? {GameSettings.Instance == null}");
        Debug.Log($"VehicleDB null? {VehicleDatabase.Instance == null}");
        Debug.Log($"Mode = {GameSettings.Instance?.CurrentMode}");
        if (!Object.HasStateAuthority)
            return;

        var type =
            mode == GameModes.Tank
            ? TankVehicleType
            : RaceVehicleType;

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
        if (!Object.HasInputAuthority)
            return;

        if (VehicleObject == null)
            return;

        var tankVehicle = VehicleObject.GetComponent<TankVehicle>();
        if (tankVehicle == null)
            return;

        if (cam == null)
            cam = Camera.main?.GetComponent<CameraFollow>();

        if (cam == null)
            return;

        cam.SetTarget(tankVehicle.Move.VisualRoot);
        TankAimUI.Instance?.SetVehicleShot(tankVehicle);
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

        VehicleType type =
            gameMode == GameModes.Tank
            ? TankVehicleType
            : RaceVehicleType;

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