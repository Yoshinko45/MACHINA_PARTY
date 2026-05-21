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
    bool isSelfShutdown;    // Runner切り替え時の手動シャットダウン中のフラグ.
    bool isConnecting;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void JoinLobby()
    {
        ConnectionManager.Instance.ConnectingLobby();
        StartCoroutine(ChangeToLobby());
    }

    IEnumerator JoinLobbyRoutine()
    {
        Runner = Instantiate(networkRunnerPrefab);  // NetworkRunner作成.
        Runner.AddCallbacks(this);  // NetworkRunnnerのコールバック対象に追加.
        Task<StartGameResult> task = Runner.JoinSessionLobby(SessionLobby.Shared);  // Lobby接続.
        while (!task.IsCompleted)
            yield return null;

        StartGameResult result = task.Result;
        if (result.Ok)
        {
            isReconnecting = false;
            Debug.Log("Connected to lobby");
            ConnectionManager.Instance.SetState(ConnectionState.InLobby);
            OnLobbyJoined?.Invoke();    // イベント通知.
        }
        else
        {
            Debug.Log("Failed to connect to lobby");
        }
    }

    IEnumerator ChangeToLobby()
    {
        yield return ShutdownRunner();
        yield return JoinLobbyRoutine();
    }

    public void CreateRoom(string roomName)
    {
        currentRoom = roomName;
        wasInRoom = true;

        ConnectionManager.Instance.ConnectingRoom();
        StartCoroutine(ChangeToCreateRoom(roomName));
    }

    IEnumerator CreateRoomRoutine(string roomName)
    {
        Runner = Instantiate(networkRunnerPrefab);
        Runner.AddCallbacks(this);

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
            isReconnecting = false;
            Debug.Log("Room Created: " + roomName);
            ConnectionManager.Instance.JoinedRoom();
        }
    }

    IEnumerator ChangeToCreateRoom(string roomName)
    {
        yield return ShutdownRunner();
        yield return CreateRoomRoutine(roomName);
    }

    public void JoinRoom(string roomName)
    {
        currentRoom = roomName;
        wasInRoom = true;

        ConnectionManager.Instance.ConnectingRoom();
        StartCoroutine(ChangeToJoinRoom(roomName));
    }

    IEnumerator JoinRoomRoutine(string roomName)
    {
        if (isConnecting)
            yield break;

        isConnecting = true;

        // Room用のRunnerを作成.
        Runner = Instantiate(networkRunnerPrefab);
        Runner.AddCallbacks(this);

        var task = Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = roomName
        });

        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
        {
            isReconnecting = false;
            Debug.Log("Joined Room: " + roomName);
            ConnectionManager.Instance.JoinedRoom();
        }
        else
        {
            Debug.Log(task.Result.ShutdownReason);
        }

        isConnecting = false;
    }

    IEnumerator ChangeToJoinRoom(string roomName)
    {
        yield return ShutdownRunner();
        yield return JoinRoomRoutine(roomName);
    }

    IEnumerator ShutdownRunner()
    {
        if (Runner == null)
        {
            yield break;
        }

        isSelfShutdown = true;

        var oldRunner = Runner;
        Runner = null;

        var task = oldRunner.Shutdown();

        while (!task.IsCompleted)
            yield return null;
    }

    // 再接続.
    IEnumerator ReconnectRoutine()
    {
        ConnectionManager.Instance.SetState(ConnectionState.Reconnecting);

        yield return new WaitForSeconds(2f);

        yield return ShutdownRunner();

        if (wasInRoom && !string.IsNullOrEmpty(currentRoom))
        {
            yield return JoinRoomRoutine(currentRoom);
        }
        else
        {
            yield return JoinLobbyRoutine();
        }

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
        Debug.Log($"Shutdown: {shutdownReason}");

        // Runner切り替えように閉じてる場合は無視.
        if (isSelfShutdown)
        {
            isSelfShutdown = false;
            return;
        }

        if (!isReconnecting)
        {
            isReconnecting = true;
            StartCoroutine(ReconnectRoutine());
        }
    }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"Disconnected: {reason}");
        // Runner切り替えように閉じてる場合は無視.
        if (isSelfShutdown)
        {
            isSelfShutdown = false;
            return;
        }

        if (!isReconnecting)
        {
            isReconnecting = true;
            StartCoroutine(ReconnectRoutine());
        }
    }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connection Failed: {reason}");
        // Runner切り替えように閉じてる場合は無視.
        if (isSelfShutdown)
        {
            isSelfShutdown = false;
            return;
        }

        if (!isReconnecting)
        {
            isReconnecting = true;
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
