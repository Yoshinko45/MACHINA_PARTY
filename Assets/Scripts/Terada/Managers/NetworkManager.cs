using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// Fusionを直接触る.
//  通信接続, Runner生成
//  Lobby接続, Room接続
//  Fusion Callback管理
/// </summary>
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;
    public NetworkRunner Runner { get; private set; }

    [SerializeField] private NetworkRunner networkRunnerPrefab;

    // イベント定義.
    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnLobbyJoined;
    public event Action<PlayerRef> OnPlayerJoinedEvent;
    public event Action<PlayerRef> OnPlayerLeftEvent;

    string currentRoom;
    bool isReconnecting;
    bool wasInRoom;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void JoinLobby()
    {
        ConnectionManager.Instance.ConnectingLobby();
        StartCoroutine(JoinLobbyRoutine());
    }

    IEnumerator JoinLobbyRoutine()
    {
        if (Runner != null)
        {
            Runner.Shutdown();
            Destroy(Runner.gameObject);
        }
        Runner = Instantiate(networkRunnerPrefab);  // NetworkRunner作成.
        Runner.AddCallbacks(this);  // NetworkRunnnerのコールバック対象に追加.
        Task<StartGameResult> task = Runner.JoinSessionLobby(SessionLobby.Shared);  // Lobby接続.
        while (!task.IsCompleted)
            yield return null;

        StartGameResult result = task.Result;
        if (result.Ok)
        {
            Debug.Log("Connected to lobby");
            ConnectionManager.Instance.SetState(ConnectionState.InLobby);
            OnLobbyJoined?.Invoke();    // イベント通知.
        }
        else
        {
            Debug.Log("Failed to connect to lobby");
        }
    }

    public void CreateRoom(string roomName)
    {
        currentRoom = roomName;
        wasInRoom = true;

        ConnectionManager.Instance.ConnectingRoom();
        StartCoroutine(CreateRoomRoutine(roomName));
    }

    IEnumerator CreateRoomRoutine(string roomName)
    {
        var task = Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = 4,
            Scene = SceneRef.FromIndex(1)
        });

        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
        {
            Debug.Log("Room Created: " + roomName);
            ConnectionManager.Instance.JoinedRoom();
        }
    }

    public void JoinRoom(string roomName)
    {
        currentRoom = roomName;
        wasInRoom = true;

        ConnectionManager.Instance.ConnectingRoom();
        StartCoroutine(JoinRoomRoutine(roomName));
    }

    IEnumerator JoinRoomRoutine(string roomName)
    {
        var task = Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = roomName
        });

        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
        {
            Debug.Log("Joined Room: " + roomName);
            ConnectionManager.Instance.JoinedRoom();
        }
    }

    // 再接続.
    IEnumerator ReconnectRoutine()
    {
        ConnectionManager.Instance.SetState(ConnectionState.Reconnecting);

        yield return new WaitForSeconds(2f);

        if (Runner != null)
        {
            Runner.Shutdown();
            Destroy(Runner.gameObject);
        }

        Runner = Instantiate(networkRunnerPrefab);
        Runner.AddCallbacks(this);

        if (wasInRoom && !string.IsNullOrEmpty(currentRoom))
        {
            JoinRoom(currentRoom);
        }
        else
        {
            JoinLobby();
        }

        isReconnecting = false;
    }

    // ↓Fusionの各コールバック関数.
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(sessionList);  // RoomList更新を通知.
    }

    // プレイヤー参加
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        OnPlayerJoinedEvent?.Invoke(player);
    }

    // プレイヤー退出
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        OnPlayerLeftEvent?.Invoke(player);
    }

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    /// <summary>
    /// Fusionの入力同期.
    /// </summary>
    /// <param name="runner"></param>
    /// <param name="input"></param>
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerInputData();

        if (InputManager.Instance != null)
        {
            // InputManagerの入力を取得.
            data.Throttle = InputManager.Instance.Move.y;   // 前後.
            data.Steering = InputManager.Instance.Move.x;   // 左右.
            data.Buttons.Set(NetworkInputButtons.Jump, InputManager.Instance.Jump); // ジャンプ.
        }

        input.Set(data);    // Fusionへ送信.
    }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.LogWarning($"Shutdown: {shutdownReason}");
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectRoutine());
        }
    }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"Disconnected: {reason}");
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectRoutine());
        }
    }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connection Failed: {reason}");
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectRoutine());
        }
    }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
}
