using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;

    public NetworkRunner Runner { get; private set; }

    [SerializeField]
    NetworkRunner networkRunnerPrefab;

    // -------------------------
    // イベント
    // -------------------------

    public event Action<List<SessionInfo>> OnSessionListChanged;

    public event Action OnLobbyJoined;

    public event Action<PlayerRef> OnPlayerJoinedEvent;

    public event Action<PlayerRef> OnPlayerLeftEvent;

    // -------------------------
    // State
    // -------------------------

    string currentRoom;

    bool isConnecting;

    bool isSelfShutdown;

    bool isReconnecting;
    int reconnectCount;

    const int maxReconnect = 5;

    const float timeoutSeconds = 10f;

    ConnectMode lastConnectMode;

    CancellationTokenSource connectCTS;
    CancellationTokenSource reconnectCTS;

    // -------------------------
    // Unity
    // -------------------------

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    async void OnDestroy()
    {
        connectCTS?.Cancel();
        reconnectCTS?.Cancel();
        reconnectCTS?.Dispose();

        await ShutdownRunnerAsync();
    }

    // -------------------------
    // Public
    // -------------------------

    public async void JoinLobby()
    {
        Debug.Log("[NetworkManager] JoinLobby() called");
        lastConnectMode = ConnectMode.Lobby;

        SetState(ConnectionState.ConnectingLobby);

        await ChangeConnectionAsync();
    }

    public async void CreateRoom(string roomName)
    {
        Debug.Log($"[NetworkManager] CreateRoom() called name={roomName}");
        currentRoom = roomName;

        lastConnectMode = ConnectMode.RoomHost;

        SetState(ConnectionState.ConnectingRoom);

        await ChangeConnectionAsync();
    }

    public async void JoinRoom(string roomName)
    {
        currentRoom = roomName;

        lastConnectMode = ConnectMode.RoomClient;

        SetState(ConnectionState.ConnectingRoom);

        await ChangeConnectionAsync();
    }

    // -------------------------
    // Main Connect
    // -------------------------

    async Task ChangeConnectionAsync()
    {
        connectCTS?.Cancel();

        connectCTS = new CancellationTokenSource();

        await ShutdownRunnerAsync();

        bool success = await ConnectAsync(connectCTS.Token);
        if (!success)
        {
            StartReconnect();
        }
    }

    async Task<bool> ConnectAsync(
        CancellationToken token)
    {
        Debug.Log("[NetworkManager] ConnectAsync() start");
        if (isConnecting)
        {
            Debug.LogWarning("[NetworkManager] Already connecting");
            return false;
        }
        isConnecting = true;

        try
        {
            NetworkRunner newRunner = Instantiate(networkRunnerPrefab);
            Runner = newRunner;
            newRunner.AddCallbacks(this);

            Task<StartGameResult> connectTask;

            // -------------------------
            // Lobby
            // -------------------------

            if (lastConnectMode ==
                ConnectMode.Lobby)
            {
                connectTask =
                    Runner.JoinSessionLobby(
                        SessionLobby.ClientServer);
            }

            // -------------------------
            // Room
            // -------------------------

            else
            {
                StartGameArgs args =
                    new StartGameArgs();

                switch (lastConnectMode)
                {
                    case ConnectMode.RoomHost:

                        args = new StartGameArgs()
                        {
                            GameMode = GameMode.Host,
                            SessionName = currentRoom,
                            PlayerCount = 4,
                            IsVisible = true,
                            IsOpen = true,
                            Scene = SceneRef.FromIndex(1)
                        };

                        break;

                    case ConnectMode.RoomClient:

                        args = new StartGameArgs()
                        {
                            GameMode =
                                GameMode.Client,

                            SessionName = currentRoom
                        };

                        break;
                }

                connectTask = Runner.StartGame(args);
            }

            // -------------------------
            // Timeout
            // -------------------------

            Task timeoutTask =
                Task.Delay(
                    TimeSpan.FromSeconds(
                        timeoutSeconds),
                    token);

            Task completed =
                await Task.WhenAny(
                    connectTask,
                    timeoutTask);

            // timeout
            if (completed == timeoutTask)
            {
                Debug.LogError(
                    "Connection Timeout");

                newRunner.RemoveCallbacks(this);

                if (Runner == newRunner)
                {
                    Runner = null;
                }

                Destroy(newRunner.gameObject);

                return false;
            }

            // after StartGame or JoinSessionLobby completed
            StartGameResult result =
                await connectTask;
            Debug.Log($"[NetworkManager] StartGameResult: Ok={result.Ok} Reason={result.ShutdownReason}");

            Debug.Log($"SessionName = {Runner.SessionInfo?.Name}");

            Debug.Log($"IsVisible = {Runner.SessionInfo?.IsVisible}");

            Debug.Log($"IsOpen = {Runner.SessionInfo?.IsOpen}");
            // failed
            if (!result.Ok)
            {
                Debug.Log("[NetworkManager] Connect failed: " + result.ShutdownReason);
                // cleanup...

                newRunner.RemoveCallbacks(this);

                if (Runner == newRunner)
                {
                    Runner = null;
                }

                Destroy(newRunner.gameObject);

                return false;
            }

            // -------------------------
            // Success
            // -------------------------

            Debug.Log("[NetworkManager] ConnectAsync success, mode=" + lastConnectMode);
            Debug.Log($"CloudRegion = {Runner.SessionInfo?.Region}");
            reconnectCount = 0;

            isReconnecting = false;

            switch (lastConnectMode)
            {
                case ConnectMode.Lobby:

                    Debug.Log(
                        "Lobby Connected");

                    SetState(
                        ConnectionState.InLobby);

                    OnLobbyJoined?.Invoke();

                    break;

                case ConnectMode.RoomHost:
                case ConnectMode.RoomClient:

                    Debug.Log(
                        "Room Connected");

                    SetState(
                        ConnectionState.InRoom);

                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log(
                "[NetworkManager] ConnectAsync cancelled");
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            return false;
        }
        finally
        {
            isConnecting = false;
        }

        return true;
    }

    // -------------------------
    // Shutdown
    // -------------------------

    async Task ShutdownRunnerAsync()
    {
        if (Runner == null)
            return;

        SetState(
            ConnectionState.Disconnecting);

        isSelfShutdown = true;

        NetworkRunner oldRunner = Runner;

        Runner = null;
        try
        {
            await oldRunner.Shutdown();
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
        }

        Destroy(oldRunner.gameObject);

        await Task.Delay(500);

        isSelfShutdown = false;
    }

    // -------------------------
    // Reconnect
    // -------------------------

    async void StartReconnect()
    {
        if (isReconnecting)
            return;

        isReconnecting = true;

        reconnectCTS?.Cancel();
        reconnectCTS?.Dispose();

        reconnectCTS = new CancellationTokenSource();

        SetState(ConnectionState.Reconnecting);

        try
        {
            while (reconnectCount < maxReconnect)
            {
                reconnectCount++;

                float waitTime =
                    Mathf.Min(
                        2f * Mathf.Pow(
                            2f, reconnectCount - 1),
                        30f);

                Debug.Log($"Reconnect Wait: {waitTime}");

                await Task.Delay(
                    TimeSpan.FromSeconds(waitTime),
                    reconnectCTS.Token);

                connectCTS?.Cancel();
                connectCTS?.Dispose();

                connectCTS = new CancellationTokenSource();

                if (Runner != null)
                {
                    await ShutdownRunnerAsync();
                }

                bool success = await ConnectAsync(connectCTS.Token);

                if (success)
                {
                    reconnectCount = 0;
                    return;
                }
            }

            SetState(ConnectionState.Failed);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Reconnect Canceled");
        }
        finally
        {
            isReconnecting = false;
        }
    }

    // -------------------------
    // State
    // -------------------------

    void SetState(ConnectionState state)
    {
        ConnectionManager.Instance
            .SetState(state);
    }

    // -------------------------
    // Fusion Callback
    // -------------------------

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        Debug.Log($"[NetworkManager] OnSessionListUpdated: sessions={sessionList?.Count ?? 0}");
        if (sessionList != null)
        {
            foreach (var s in sessionList)
                Debug.Log($"[NetworkManager] Session: Name={s.Name} Players={s.PlayerCount}/{s.MaxPlayers} Open={s.IsOpen}");
        }
        OnSessionListChanged?.Invoke(
            sessionList);
    }

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
        OnPlayerJoinedEvent?.Invoke(player);
    }

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        OnPlayerLeftEvent?.Invoke(player);
    }

    void INetworkRunnerCallbacks.OnShutdown(
        NetworkRunner runner,
        ShutdownReason shutdownReason)
    {
        Debug.Log(
            $"Shutdown: {shutdownReason}");

        if (shutdownReason == ShutdownReason.Ok)
            return;

        if (isSelfShutdown || isReconnecting)
            return;

        StartReconnect();
    }

    void INetworkRunnerCallbacks
        .OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
        Debug.LogWarning(
            $"Disconnected: {reason}");

        if (isSelfShutdown || isReconnecting)
            return;

        StartReconnect();
    }

    void INetworkRunnerCallbacks
        .OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogError(
            $"Connect Failed: {reason}");

        if (isSelfShutdown || isReconnecting)
            return;

        StartReconnect();
    }

    // -------------------------
    // Input
    // -------------------------

    void INetworkRunnerCallbacks.OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        var data = new PlayerInputData();

        if (InputManager.Instance != null)
        {
            data.Throttle = InputManager.Instance.Move.y;
            data.Steering = InputManager.Instance.Move.x;

            data.Buttons.Set(
                NetworkInputButtons.Jump,
                InputManager.Instance.Jump);

            // タンクショット用.
            if (InputManager.Instance.Fire)
            {
                data.Buttons.Set(
                    NetworkInputButtons.Fire,
                    true);
            }

            // 旧バージョンのTankMove用（CharacterControlerベースでバグり散らかしたのでなし）.
            //data.Move = InputManager.Instance.Move;
        }

        input.Set(data);
    }

    // -------------------------
    // Empty Callback
    // -------------------------

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) 
    {
        Debug.Log(
        $"Connected To Server. GameMode={runner.GameMode}");
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}

public enum ConnectMode
{
    Lobby,
    RoomHost,
    RoomClient
}
