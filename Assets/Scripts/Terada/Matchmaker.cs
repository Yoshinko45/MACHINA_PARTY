using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;

public class Matchmaker : MonoBehaviour, INetworkRunnerCallbacks
{
    #region パブリック変数
    public static Matchmaker Instance { get; private set; }
    public NetworkRunner Runner { get; private set; }

    #endregion

    #region プライベート変数
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private CanvasGroup LobbyPanel;
    [SerializeField] private RoomListUI roomListPrefab;
    [SerializeField] RectTransform roomItemHolder;
    readonly List<RoomListUI> roomListUIs = new List<RoomListUI>();
    #endregion

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        HideRoomList();
    }

    // ロビー表示ボタン用.
    public void OnJoinLobby()
    {
        StartCoroutine(JoinLobbyRoutine());
    }

    // ロビー入室処理.
    IEnumerator JoinLobbyRoutine()
    {
        Runner = Instantiate(networkRunnerPrefab);
        Runner.AddCallbacks(this);
        Task<StartGameResult> task = Runner.JoinSessionLobby(SessionLobby.Shared);
        while (!task.IsCompleted)
            yield return null;

        StartGameResult result = task.Result;
        if (result.Ok)
        {
            Debug.Log("Connected to lobby");
            ShowRoomList();
        }else
        {
            Debug.Log("Failed to connect to lobby");
        }
    }

    // ルーム作成用.
    public void CreateRoom()
    {
        StartCoroutine(CreateRoomRoutine());
    }

    // ルーム作成処理.
    IEnumerator CreateRoomRoutine()
    {
        string roomName = "Room_" + UnityEngine.Random.Range(1000, 9999);
        Task<StartGameResult> task = Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = 4
        });

        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
        {
            Debug.Log("Room Created: " + roomName);
            // ルーム作成に成功したのでロビーからは退出(後々待機ルームに).
            HideRoomList();
        }
    }

    // RoomList用.
    public void JoinRoom(string roomName)
    {
        StartCoroutine(JoinRoomRoutine(roomName));
    }

    // ルーム参加処理.
    IEnumerator JoinRoomRoutine(string roomName)
    {
        Task<StartGameResult> task = Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = roomName
        });

        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
        {
            Debug.Log("Joined Room: " + roomName);
        }
    }

    // ルーム一覧(ロビーを表示)
    void ShowRoomList()
    {
        LobbyPanel.alpha = 1;
        LobbyPanel.interactable = true;
        LobbyPanel.blocksRaycasts = true;
    }

    // リストを非表示にするだけ.
    void HideRoomList()
    {
        LobbyPanel.alpha = 0;
        LobbyPanel.interactable = false;
        LobbyPanel.blocksRaycasts = false;
    }

    RoomListUI GetRoomItem(int i)
    {
        return roomListUIs.ElementAtOrDefault(i) ?? TrackItem(Instantiate(roomListPrefab, roomItemHolder));
    }

    RoomListUI TrackItem(RoomListUI list)
    {
        roomListUIs.Add(list);
        return list;
    }

    RoomListUI GetRoomItem(string roomname)
    {
        return roomListUIs.FirstOrDefault(item => item.roomName == roomname) ?? TrackItem(Instantiate(roomListPrefab, roomItemHolder));
    }

    #region INetworkRunnerCallbacks
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // 作成した全ルームを表示
        for (int i = 0; i < sessionList.Count; i++)
        {
            var session = sessionList[i];

            GetRoomItem(i).Init(
                session.Name,
                session.PlayerCount,
                session.MaxPlayers,
                session.IsOpen
            );
        }

        for (int i = sessionList.Count; i < roomListUIs.Count; i++)
        {
            roomListUIs[i].gameObject.SetActive(false);
        }

        Debug.Log("SessionList Updated: " + sessionList.Count);
    }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    #endregion
}