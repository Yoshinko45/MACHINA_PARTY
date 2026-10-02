using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    public NetworkRunner Runner { get; private set; }

    [SerializeField]
    NetworkRunner networkRunnerPrefab;

    [Header("Initial Room Scene")]
    [SerializeField]
    int roomSceneIndex = 1;

    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnLobbyJoined;

    public event Action<PlayerRef> OnPlayerJoinedEvent;
    public event Action<PlayerRef> OnPlayerLeftEvent;

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

    // ==================================================
    // Unity
    // ==================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
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

    // ==================================================
    // Public
    // ==================================================

    public async void JoinLobby()
    {
        Debug.Log("[NetworkManager] JoinLobby");

        lastConnectMode = ConnectMode.Lobby;

        SetState(ConnectionState.ConnectingLobby);

        await ChangeConnectionAsync();
    }

    public async void CreateRoom(string roomName)
    {
        Debug.Log(
            $"[NetworkManager] CreateRoom : {roomName}");

        currentRoom = roomName;

        lastConnectMode = ConnectMode.RoomHost;

        SetState(ConnectionState.ConnectingRoom);

        await ChangeConnectionAsync();
    }

    public async void JoinRoom(string roomName)
    {
        Debug.Log(
            $"[NetworkManager] JoinRoom : {roomName}");

        currentRoom = roomName;

        lastConnectMode = ConnectMode.RoomClient;

        SetState(ConnectionState.ConnectingRoom);

        await ChangeConnectionAsync();
    }

    // ==================================================
    // Connection
    // ==================================================

    async Task ChangeConnectionAsync()
    {
        connectCTS?.Cancel();

        connectCTS =
            new CancellationTokenSource();

        await ShutdownRunnerAsync();

        bool success =
            await ConnectAsync(
                connectCTS.Token);

        if (!success)
            StartReconnect();
    }

    async Task<bool> ConnectAsync(
        CancellationToken token)
    {
        if (isConnecting)
            return false;

        isConnecting = true;

        Debug.Log(
            $"[NetworkManager] ConnectAsync : " +
            $"{lastConnectMode}");

        try
        {
            NetworkRunner newRunner =
                Instantiate(networkRunnerPrefab);

            // RunnerÇ‡SceneÇÇ‹ÇΩÇ¢Ç≈ï€éù
            DontDestroyOnLoad(
                newRunner.gameObject);

            Runner = newRunner;

            newRunner.AddCallbacks(this);

            Task<StartGameResult> connectTask;

            // ==================================================
            // Lobby
            // ==================================================

            if (lastConnectMode ==
                ConnectMode.Lobby)
            {
                connectTask =
                    Runner.JoinSessionLobby(
                        SessionLobby.ClientServer);
            }
            else
            {
                NetworkSceneManagerDefault sceneManager =
                    Runner.GetComponent<
                        NetworkSceneManagerDefault>();

                if (sceneManager == null)
                {
                    Debug.LogError(
                        "[NetworkManager] " +
                        "NetworkSceneManagerDefault is missing.");

                    return false;
                }

                StartGameArgs args;

                switch (lastConnectMode)
                {
                    // ==================================================
                    // Host
                    // ==================================================

                    case ConnectMode.RoomHost:

                        args = new StartGameArgs
                        {
                            GameMode =
                                GameMode.Host,

                            SessionName =
                                currentRoom,

                            PlayerCount = 4,

                            IsVisible = true,

                            IsOpen = true,

                            SceneManager =
                                sceneManager
                        };

                        break;

                    // ==================================================
                    // Client
                    // ==================================================

                    case ConnectMode.RoomClient:

                        args = new StartGameArgs
                        {
                            GameMode =
                                GameMode.Client,

                            SessionName =
                                currentRoom,

                            SceneManager =
                                sceneManager
                        };

                        break;

                    default:

                        return false;
                }

                connectTask =
                    Runner.StartGame(args);
            }

            // ==================================================
            // Timeout
            // ==================================================

            Task timeoutTask =
                Task.Delay(
                    TimeSpan.FromSeconds(
                        timeoutSeconds),
                    token);

            Task completed =
                await Task.WhenAny(
                    connectTask,
                    timeoutTask);

            if (completed == timeoutTask)
            {
                Debug.LogError(
                    "[NetworkManager] " +
                    "Connection timeout.");

                await newRunner.Shutdown();

                newRunner.RemoveCallbacks(this);

                if (Runner == newRunner)
                    Runner = null;

                Destroy(
                    newRunner.gameObject);

                return false;
            }

            StartGameResult result =
                await connectTask;

            Debug.Log(
                $"[NetworkManager] StartGameResult " +
                $"Ok={result.Ok} " +
                $"Reason={result.ShutdownReason}");

            if (!result.Ok)
            {
                newRunner.RemoveCallbacks(this);

                if (Runner == newRunner)
                    Runner = null;

                Destroy(
                    newRunner.gameObject);

                return false;
            }

            reconnectCount = 0;
            isReconnecting = false;

            // ==================================================
            // Connected
            // ==================================================

            switch (lastConnectMode)
            {
                case ConnectMode.Lobby:

                    SetState(
                        ConnectionState.InLobby);

                    OnLobbyJoined?.Invoke();

                    break;

                case ConnectMode.RoomHost:

                    // HostÇæÇØÇ™ç≈èâÇÃRoomSceneÇÉçÅ[Éh
                    await Runner.LoadScene(
                        SceneRef.FromIndex(
                            roomSceneIndex),
                        LoadSceneMode.Additive);

                    SetState(
                        ConnectionState.InRoom);

                    break;

                case ConnectMode.RoomClient:

                    // ClientÇÕHostÇÃSceneèÛë‘Ç…ìØä˙Ç≥ÇÍÇÈ
                    SetState(
                        ConnectionState.InRoom);

                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log(
                "[NetworkManager] " +
                "Connect cancelled.");

            return false;
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

    // ==================================================
    // Shutdown
    // ==================================================

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

        if (oldRunner != null)
            Destroy(oldRunner.gameObject);

        await Task.Delay(500);

        isSelfShutdown = false;
    }

    // ==================================================
    // Reconnect
    // ==================================================

    async void StartReconnect()
    {
        if (isReconnecting)
            return;

        isReconnecting = true;

        reconnectCTS?.Cancel();
        reconnectCTS?.Dispose();

        reconnectCTS =
            new CancellationTokenSource();

        SetState(
            ConnectionState.Reconnecting);

        try
        {
            while (reconnectCount < maxReconnect)
            {
                reconnectCount++;

                float waitTime =
                    Mathf.Min(
                        2f *
                        Mathf.Pow(
                            2f,
                            reconnectCount - 1),
                        30f);

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        waitTime),
                    reconnectCTS.Token);

                connectCTS?.Cancel();
                connectCTS?.Dispose();

                connectCTS =
                    new CancellationTokenSource();

                if (Runner != null)
                    await ShutdownRunnerAsync();

                bool success =
                    await ConnectAsync(
                        connectCTS.Token);

                if (success)
                {
                    reconnectCount = 0;
                    return;
                }
            }

            SetState(
                ConnectionState.Failed);
        }
        catch (OperationCanceledException)
        {
            Debug.Log(
                "[NetworkManager] " +
                "Reconnect cancelled.");
        }
        finally
        {
            isReconnecting = false;
        }
    }

    // ==================================================
    // State
    // ==================================================

    void SetState(
        ConnectionState state)
    {
        if (ConnectionManager.Instance != null)
        {
            ConnectionManager.Instance.SetState(
                state);
        }
    }

    // ==================================================
    // Fusion callbacks
    // ==================================================

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(
            sessionList);
    }

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
        Debug.Log(
            $"[NetworkManager] " +
            $"PlayerJoined {player}");

        OnPlayerJoinedEvent?.Invoke(
            player);
    }

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        Debug.Log(
            $"[NetworkManager] " +
            $"PlayerLeft {player}");

        OnPlayerLeftEvent?.Invoke(
            player);
    }

    public void OnSceneLoadDone(
        NetworkRunner runner)
    {
        Debug.Log(
            $"[NetworkManager] " +
            $"SceneLoadDone " +
            $"Scene={runner.SimulationUnityScene.name}");

        foreach (var obj in
            runner.GetAllNetworkObjects())
        {
            Debug.Log(
                $"NetworkObject " +
                $"Id={obj.Id} " +
                $"Name={obj.name} " +
                $"Scene={obj.gameObject.scene.name}");
        }
    }

    public void OnSceneLoadStart(
        NetworkRunner runner)
    {
        Debug.Log(
            "[NetworkManager] SceneLoadStart");
    }

    // ==================================================
    // Input
    // ==================================================

    void INetworkRunnerCallbacks.OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        var data =
            new PlayerInputData();

        if (InputManager.Instance != null)
        {
            data.Throttle =
                InputManager.Instance.Move.y;

            data.Steering =
                InputManager.Instance.Move.x;

            Camera mainCamera =
                Camera.main;

            if (mainCamera != null)
            {
                data.CameraForward =
                    mainCamera.transform.forward;

                data.CameraRight =
                    mainCamera.transform.right;
            }

            data.Buttons.Set(
                NetworkInputButtons.Jump,
                InputManager.Instance.Jump);

            data.Buttons.Set(
                NetworkInputButtons.Fire,
                InputManager.Instance.Fire);
        }

        input.Set(data);
    }

    // ==================================================
    // Empty callbacks
    // ==================================================

    void INetworkRunnerCallbacks.OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    void INetworkRunnerCallbacks.OnConnectedToServer(
        NetworkRunner runner)
    {
        Debug.Log(
            $"Connected To Server. " +
            $"GameMode={runner.GameMode}");
    }

    void INetworkRunnerCallbacks.OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(
        NetworkRunner runner,
        SimulationMessagePtr message)
    {
    }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    void INetworkRunnerCallbacks.OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
    }

    void INetworkRunnerCallbacks.OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data)
    {
    }

    void INetworkRunnerCallbacks.OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    void INetworkRunnerCallbacks.OnShutdown(
        NetworkRunner runner,
        ShutdownReason shutdownReason)
    {
        Debug.Log(
            $"Shutdown : {shutdownReason}");

        if (shutdownReason ==
            ShutdownReason.Ok)
        {
            return;
        }

        if (isSelfShutdown ||
            isReconnecting)
        {
            return;
        }

        StartReconnect();
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
        Debug.LogWarning(
            $"Disconnected : {reason}");

        if (isSelfShutdown ||
            isReconnecting)
        {
            return;
        }

        StartReconnect();
    }

    void INetworkRunnerCallbacks.OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogError(
            $"Connect Failed : {reason}");

        if (isSelfShutdown ||
            isReconnecting)
        {
            return;
        }

        StartReconnect();
    }

    void INetworkRunnerCallbacks.OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    void INetworkRunnerCallbacks.OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }
}

public enum ConnectMode
{
    Lobby,
    RoomHost,
    RoomClient
}