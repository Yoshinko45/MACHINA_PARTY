using Fusion;
using Tanks.Complete;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [Networked]
    public NetworkString<_16> PlayerName { get; set; }

    [Networked, OnChangedRender(nameof(OnReadyChanged))]
    public bool IsReady { get; set; }

    [Networked, OnChangedRender(nameof(OnVehicleChanged))]
    public NetworkObject VehicleObject { get; set; }

    [Networked]
    public VehicleType TankVehicleType { get; set; }

    [Networked]
    public VehicleType RaceVehicleType { get; set; }

    [Networked]
    public bool HasVoted { get; set; }

    [Networked]
    public GameModes Vote { get; set; }

    [Networked]
    public int Wins { get; set; }

    [SerializeField]
    float interactDistance = 5f;

    public static NetworkPlayer LocalPlayer;

    CameraFollow cam;

    NetworkButtons previousButtons;

    // ==================================================
    // Spawned
    // ==================================================

    public override void Spawned()
    {
        Debug.Log(
            $"[NetworkPlayer] Spawned " +
            $"Player={Object.InputAuthority} " +
            $"StateAuthority={Object.StateAuthority}");

        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;

            RPC_SetName(
                "Player_" +
                Random.Range(100, 999));

            PlayerListManager.Instance?.Refresh();
        }

        if (Object.HasStateAuthority)
        {
            TankVehicleType =
                VehicleType.NormalTank;

            RaceVehicleType =
                VehicleType.RaceCar;

            HasVoted = false;
            IsReady = false;
            Wins = 0;
        }

        UpdateCameraTarget();
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (LocalPlayer == this)
            LocalPlayer = null;
    }

    // ==================================================
    // Initial Vehicle
    // ==================================================

    /// <summary>
    /// RoomÇ…ì¸Ç¡ÇΩÇ∆Ç´Ç…VehicleÇê∂ê¨Ç∑ÇÈÅB
    /// </summary>
    public void SpawnInitialVehicle()
    {
        if (!Object.HasStateAuthority)
            return;

        if (VehicleObject != null &&
            VehicleObject.IsValid)
        {
            return;
        }

        // ç≈èâÇÕTankÇê∂ê¨
        SpawnVehicleForMode(
            GameModes.Tank);
    }

    // ==================================================
    // Vehicle Spawn
    // ==================================================

    public void SpawnVehicleForMode(
        GameModes mode)
    {
        if (!Object.HasStateAuthority)
            return;

        if (VehicleObject != null &&
            VehicleObject.IsValid)
        {
            return;
        }

        VehicleType type =
            GetVehicle(mode);

        NetworkPrefabRef prefab =
            VehicleDatabase.Instance
                .GetPrefab(type);

        if (!prefab.IsValid)
        {
            Debug.LogError(
                $"[NetworkPlayer] " +
                $"Invalid Vehicle Prefab : {type}");

            return;
        }

        NetworkObject vehicle =
            Runner.Spawn(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                Object.InputAuthority);

        if (vehicle == null)
        {
            Debug.LogError(
                "[NetworkPlayer] " +
                "Vehicle Spawn failed.");

            return;
        }

        // SceneÇ…àÀë∂ÇµÇ»Ç¢
        Runner.MakeDontDestroyOnLoad(
            vehicle.gameObject);

        VehicleObject = vehicle;

        Debug.Log(
            $"[NetworkPlayer] " +
            $"Vehicle spawned " +
            $"Type={type} " +
            $"Id={vehicle.Id}");
    }

    public void SpawnVehicle(
        GameModes mode)
    {
        SpawnVehicleForMode(mode);
    }

    // ==================================================
    // Vehicle Despawn
    // ==================================================

    public void DespawnVehicle()
    {
        if (!Object.HasStateAuthority)
            return;

        if (VehicleObject == null)
            return;

        if (VehicleObject.IsValid)
        {
            Runner.Despawn(
                VehicleObject);
        }

        VehicleObject = null;
    }

    // ==================================================
    // Input
    // ==================================================

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!GetInput(
            out PlayerInputData data))
        {
            return;
        }

        if (VehicleObject == null)
            return;

        IAttackVehicle attackVehicle =
            VehicleObject.GetComponent<
                IAttackVehicle>();

        if (attackVehicle == null)
            return;

        NetworkButtons pressed =
            data.Buttons.GetPressed(
                previousButtons);

        NetworkButtons released =
            data.Buttons.GetReleased(
                previousButtons);

        if (pressed.IsSet(
            NetworkInputButtons.Fire))
        {
            attackVehicle.BeginCharge();
        }

        if (released.IsSet(
            NetworkInputButtons.Fire))
        {
            attackVehicle.ReleaseCharge();
        }

        previousButtons =
            data.Buttons;
    }

    // ==================================================
    // Update
    // ==================================================

    void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        if (InputManager.Instance != null &&
            InputManager.Instance
                .ConsumeInteractPressed())
        {
            TryInteract();
        }

        UpdateCameraTarget();
    }

    // ==================================================
    // Vehicle
    // ==================================================

    public VehicleType GetVehicle(
        GameModes mode)
    {
        return mode == GameModes.Tank
            ? TankVehicleType
            : RaceVehicleType;
    }

    void ChangeCurrentVehicle(
        GameModes gameMode)
    {
        if (!Object.HasStateAuthority)
            return;

        Vector3 spawnPosition =
            Vector3.zero;

        Quaternion spawnRotation =
            Quaternion.identity;

        if (VehicleObject != null &&
            VehicleObject.IsValid)
        {
            spawnPosition =
                VehicleObject.transform.position;

            spawnRotation =
                VehicleObject.transform.rotation;

            Runner.Despawn(
                VehicleObject);

            VehicleObject = null;
        }

        VehicleType type =
            GetVehicle(gameMode);

        NetworkPrefabRef prefab =
            VehicleDatabase.Instance
                .GetPrefab(type);

        if (!prefab.IsValid)
        {
            Debug.LogError(
                $"[NetworkPlayer] " +
                $"Invalid Vehicle Prefab : {type}");

            return;
        }

        NetworkObject vehicle =
            Runner.Spawn(
                prefab,
                spawnPosition,
                spawnRotation,
                Object.InputAuthority);

        if (vehicle == null)
        {
            Debug.LogError(
                "[NetworkPlayer] " +
                "Vehicle replacement failed.");

            return;
        }

        Runner.MakeDontDestroyOnLoad(
            vehicle.gameObject);

        VehicleObject = vehicle;
    }

    public void SetVehicle(
        NetworkObject vehicle)
    {
        if (!Object.HasStateAuthority)
            return;

        VehicleObject = vehicle;
    }

    // ==================================================
    // Camera
    // ==================================================

    void UpdateCameraTarget()
    {
        if (!Object.HasInputAuthority)
            return;

        if (VehicleObject == null)
            return;

        if (cam == null)
        {
            cam =
                Camera.main?
                    .GetComponent<CameraFollow>();
        }

        if (cam == null)
            return;

        cam.SetTarget(
            VehicleObject.transform);
    }

    // ==================================================
    // Interact
    // ==================================================

    void TryInteract()
    {
        if (VehicleObject == null)
            return;

        Vector3 origin =
            VehicleObject.transform.position +
            Vector3.up;

        Vector3 direction =
            VehicleObject.transform.forward;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            interactDistance))
        {
            IInteractable interactable =
                hit.collider
                    .GetComponentInParent<
                        IInteractable>();

            if (interactable != null)
            {
                interactable.Interact(this);
            }
        }
    }

    // ==================================================
    // Network callbacks
    // ==================================================

    void OnReadyChanged()
    {
        PlayerListManager.Instance?.Refresh();
    }

    void OnVehicleChanged()
    {
        UpdateCameraTarget();

        if (VehicleObject == null)
            return;

        Tank_Vehicle tankVehicle =
            VehicleObject.GetComponent<
                Tank_Vehicle>();

        if (tankVehicle != null)
        {
            TankAimUI.Instance?
                .SetVehicleShot(
                    tankVehicle);
        }
    }

    // ==================================================
    // RPC
    // ==================================================

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    public void RPC_SetName(
        string name)
    {
        PlayerName = name;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    public void RPC_Ready(
        bool ready)
    {
        IsReady = ready;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    public void RPC_Vote(
        GameModes mode)
    {
        Vote = mode;
        HasVoted = true;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    public void RPC_ChangeVehicle(
        GameModes gameMode,
        VehicleType type)
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

        ChangeCurrentVehicle(
            gameMode);
    }
}