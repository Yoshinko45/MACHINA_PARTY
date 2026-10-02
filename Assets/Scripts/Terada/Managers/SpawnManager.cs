using Fusion;
using System;
using Tanks.Complete;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Network Prefabs")]
    [SerializeField]
    NetworkPrefabRef playerPrefab;

    [SerializeField]
    NetworkPrefabRef roomStatePrefab;

    [Header("Room Spawn Points")]
    [SerializeField]
    Transform[] roomSpawnPoints;

    [Header("Game Spawn Points")]
    [SerializeField]
    Transform[] tankSpawnPoints;

    [SerializeField]
    Transform[] raceSpawnPoints;

    public event Action OnAllPlayersReadyEvent;

    // ==================================================
    // Unity
    // ==================================================

    void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (NetworkManager.Instance == null)
            return;

        NetworkManager.Instance
            .OnPlayerJoinedEvent +=
            OnPlayerJoined;

        NetworkManager.Instance
            .OnPlayerLeftEvent +=
            OnPlayerLeft;

        // OnPlayerJoinedを取り逃した場合の保険
        var runner =
            NetworkManager.Instance.Runner;

        if (runner != null &&
            runner.IsServer)
        {
            EnsureAllPlayersSpawned();
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null)
            return;

        NetworkManager.Instance
            .OnPlayerJoinedEvent -=
            OnPlayerJoined;

        NetworkManager.Instance
            .OnPlayerLeftEvent -=
            OnPlayerLeft;
    }

    // ==================================================
    // Player
    // ==================================================

    void OnPlayerJoined(
        PlayerRef player)
    {
        var runner =
            NetworkManager.Instance.Runner;

        if (runner == null ||
            !runner.IsServer)
        {
            return;
        }

        EnsureRoomState(runner);

        if (runner.TryGetPlayerObject(
            player,
            out var existing))
        {
            Debug.Log(
                $"[SpawnManager] " +
                $"Player already exists " +
                $"Id={existing.Id}");

            return;
        }

        SpawnPlayer(
            runner,
            player);
    }

    void EnsureAllPlayersSpawned()
    {
        var runner =
            NetworkManager.Instance.Runner;

        if (runner == null ||
            !runner.IsServer)
        {
            return;
        }

        EnsureRoomState(runner);

        foreach (var player in
            runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(
                player,
                out _))
            {
                continue;
            }

            SpawnPlayer(
                runner,
                player);
        }
    }

    void SpawnPlayer(
        NetworkRunner runner,
        PlayerRef player)
    {
        Transform roomSpawn =
            GetRoomSpawnPoint(
                GetPlayerIndex(
                    runner,
                    player));

        Vector3 position =
            roomSpawn != null
                ? roomSpawn.position
                : Vector3.zero;

        Quaternion rotation =
            roomSpawn != null
                ? roomSpawn.rotation
                : Quaternion.identity;

        NetworkObject playerObject =
            runner.Spawn(
                playerPrefab,
                position,
                rotation,
                player);

        if (playerObject == null)
        {
            Debug.LogError(
                "[SpawnManager] " +
                "Player Spawn failed.");

            return;
        }

        runner.SetPlayerObject(
            player,
            playerObject);

        runner.MakeDontDestroyOnLoad(
            playerObject.gameObject);

        NetworkPlayer networkPlayer =
            playerObject.GetComponent<
                NetworkPlayer>();

        if (networkPlayer == null)
        {
            Debug.LogError(
                "[SpawnManager] " +
                "NetworkPlayer missing.");

            return;
        }

        // ★ Roomに入った時点でVehicle生成
        networkPlayer.SpawnInitialVehicle();

        // Vehicle生成後、Room位置へ配置
        MovePlayerToRoom(
            networkPlayer,
            roomSpawn);
    }

    // ==================================================
    // RoomState
    // ==================================================

    void EnsureRoomState(
        NetworkRunner runner)
    {
        if (RoomState.Instance != null)
            return;

        Debug.Log(
            "[SpawnManager] " +
            "Spawning RoomState.");

        NetworkObject roomState =
            runner.Spawn(
                roomStatePrefab,
                Vector3.zero,
                Quaternion.identity);

        if (roomState == null)
        {
            Debug.LogError(
                "[SpawnManager] " +
                "RoomState Spawn failed.");

            return;
        }

        runner.MakeDontDestroyOnLoad(
            roomState.gameObject);
    }

    // ==================================================
    // Player Left
    // ==================================================

    void OnPlayerLeft(
        PlayerRef player)
    {
        var runner =
            NetworkManager.Instance.Runner;

        if (runner == null ||
            !runner.IsServer)
        {
            return;
        }

        if (!runner.TryGetPlayerObject(
            player,
            out var playerObject))
        {
            return;
        }

        NetworkPlayer networkPlayer =
            playerObject.GetComponent<
                NetworkPlayer>();

        if (networkPlayer != null)
        {
            networkPlayer.DespawnVehicle();
        }

        runner.Despawn(
            playerObject);
    }

    // ==================================================
    // Start Game
    // ==================================================

    public void StartGame()
    {
        var runner =
            NetworkManager.Instance.Runner;

        if (runner == null ||
            !runner.IsServer)
        {
            return;
        }

        if (RoomState.Instance == null)
        {
            Debug.LogError(
                "[SpawnManager] " +
                "RoomState is null.");

            return;
        }

        GameModes mode =
            RoomState.Instance.CurrentMode;

        Debug.Log(
            $"[SpawnManager] " +
            $"StartGame Mode={mode}");

        int index = 0;

        foreach (var player in
            runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                player,
                out var playerObject))
            {
                continue;
            }

            NetworkPlayer networkPlayer =
                playerObject.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                continue;

            // Vehicleが無い場合だけ生成
            if (networkPlayer.VehicleObject ==
                null)
            {
                networkPlayer
                    .SpawnVehicleForMode(mode);
            }

            NetworkObject vehicle =
                networkPlayer.VehicleObject;

            if (vehicle == null)
                continue;

            Transform spawn =
                GetGameSpawnPoint(
                    mode,
                    index);

            if (spawn != null)
            {
                MoveVehicle(
                    vehicle,
                    spawn.position,
                    spawn.rotation);
            }

            index++;
        }

        Debug.Log(
            "[SpawnManager] " +
            "All vehicles moved to game stage.");

        OnAllPlayersReadyEvent?.Invoke();

        // GameManagerを開始
        GameManager.Instance?.BeginGame();
    }

    // ==================================================
    // Return To Room
    // ==================================================

    public void ReturnPlayersToRoom()
    {
        var runner =
            NetworkManager.Instance.Runner;

        if (runner == null ||
            !runner.IsServer)
        {
            return;
        }

        Debug.Log(
            "[SpawnManager] " +
            "ReturnPlayersToRoom");

        int index = 0;

        foreach (var player in
            runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                player,
                out var playerObject))
            {
                continue;
            }

            NetworkPlayer networkPlayer =
                playerObject.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                continue;

            NetworkObject vehicle =
                networkPlayer.VehicleObject;

            if (vehicle == null)
                continue;

            Transform roomSpawn =
                GetRoomSpawnPoint(index);

            if (roomSpawn != null)
            {
                MoveVehicle(
                    vehicle,
                    roomSpawn.position,
                    roomSpawn.rotation);
            }

            ResetVehicle(
                vehicle);

            index++;
        }

        Debug.Log(
            "[SpawnManager] " +
            "All vehicles returned to Room.");
    }

    // ==================================================
    // Position
    // ==================================================

    void MovePlayerToRoom(
        NetworkPlayer player,
        Transform spawn)
    {
        if (player == null ||
            player.VehicleObject == null ||
            spawn == null)
        {
            return;
        }

        MoveVehicle(
            player.VehicleObject,
            spawn.position,
            spawn.rotation);
    }

    void MoveVehicle(
        NetworkObject vehicle,
        Vector3 position,
        Quaternion rotation)
    {
        if (vehicle == null)
            return;

        // State Authority側で位置変更
        vehicle.transform.SetPositionAndRotation(
            position,
            rotation);

        Rigidbody rb =
            vehicle.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;
        }
    }

    void ResetVehicle(
        NetworkObject vehicle)
    {
        if (vehicle == null)
            return;

        Rigidbody rb =
            vehicle.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;
        }

        Tank_Health health =
            vehicle.GetComponent<Tank_Health>();

        if (health != null)
        {
            health.ResetTank();
        }
    }

    // ==================================================
    // Spawn Points
    // ==================================================

    Transform GetRoomSpawnPoint(
        int index)
    {
        if (roomSpawnPoints == null ||
            roomSpawnPoints.Length == 0)
        {
            return null;
        }

        if (index >= roomSpawnPoints.Length)
            index = 0;

        return roomSpawnPoints[index];
    }

    Transform GetGameSpawnPoint(
        GameModes mode,
        int index)
    {
        Transform[] points =
            mode == GameModes.Tank
                ? tankSpawnPoints
                : raceSpawnPoints;

        if (points == null ||
            points.Length == 0)
        {
            return null;
        }

        if (index >= points.Length)
            index = 0;

        return points[index];
    }

    int GetPlayerIndex(
        NetworkRunner runner,
        PlayerRef target)
    {
        int index = 0;

        foreach (var player in
            runner.ActivePlayers)
        {
            if (player == target)
                return index;

            index++;
        }

        return 0;
    }

    // ==================================================
    // Compatibility
    // ==================================================

    public void NotifyPlayersReady()
    {
        OnAllPlayersReadyEvent?.Invoke();
    }
}