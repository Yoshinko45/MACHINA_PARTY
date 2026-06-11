using UnityEngine;
using System.Collections.Generic;
using Fusion;
using System.Threading.Tasks;
using System.Threading;
using System;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] CanvasGroup lobbyPanel;    // ロビーのキャンバスグループ.
    [SerializeField] RoomListUI roomPrefab;     // ルームリスト内の１つ１つのUI.
    [SerializeField] Transform parent;          // RoomListUIの親座標.

    List<RoomListUI> roomUIs = new();   // リストで生成したUIを保存.

    private CancellationTokenSource initCTS;

    async void Start()
    {
        // Inspector の割り当てチェック（早期にエラーを通知して処理停止）
        if (lobbyPanel == null)
        {
            Debug.LogError("[LobbyUIManager] lobbyPanel is not assigned in the Inspector. UI will not show.");
            enabled = false;
            return;
        }

        if (roomPrefab == null)
        {
            Debug.LogError("[LobbyUIManager] roomPrefab is not assigned in the Inspector. Room list entries cannot be created.");
            enabled = false;
            return;
        }

        if (parent == null)
        {
            Debug.LogError("[LobbyUIManager] parent (Room list container) is not assigned in the Inspector. Instantiate target missing.");
            enabled = false;
            return;
        }

        initCTS = new CancellationTokenSource();
        try
        {
            await WaitForNetAndInitAsync(initCTS.Token);
        }
        catch (OperationCanceledException)
        {
            // 初期化がキャンセルされた場合は何もしない
        }
    }

    void OnDestroy()
    {
        initCTS?.Cancel();
        if (NetworkManager.Instance == null) return;
        // イベント解除.
        NetworkManager.Instance.OnSessionListChanged -= RefreshRoomList;
        NetworkManager.Instance.OnLobbyJoined -= OnLobbyJoined;
    }

    async Task WaitForNetAndInitAsync(CancellationToken token)
    {
        while (NetworkManager.Instance == null)
        {
            token.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        RegisterEvents();
        // Lobby 初期状態は非表示
        CloseLobby();
    }

    void RegisterEvents()
    {
        // イベントの登録.
        NetworkManager.Instance.OnSessionListChanged += RefreshRoomList;    // ルーム更新時に呼ばれる.
        NetworkManager.Instance.OnLobbyJoined += OnLobbyJoined;     // ロビー接続成功時に呼ばれる.
        Debug.Log("[LobbyUIManager] Registered NetworkManager events");
    }

    // ボタン押下でロビーに接続（成功したらパネル表示）.
    public void OpenLobby()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogWarning("[LobbyUIManager] NetworkManager.Instance is null. Cannot open lobby.");
            return;
        }

        NetworkManager.Instance.JoinLobby();
    }

    // ロビーを非表示にする.
    public void CloseLobby()
    {
        if (lobbyPanel == null) return;
        lobbyPanel.alpha = 0;
        lobbyPanel.interactable = false;
        lobbyPanel.blocksRaycasts = false;
    }

    // ロビーを表示する.
    void OnLobbyJoined()
    {
        if (lobbyPanel == null) return;
        lobbyPanel.alpha = 1;
        lobbyPanel.interactable = true;
        lobbyPanel.blocksRaycasts = true;
    }

    // ボタン押下でランダムな名前のルームを作成.
    public void CreateRoomUI()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogWarning("[LobbyUIManager] NetworkManager.Instance is null. Cannot create room.");
            return;
        }

        string roomName = "Room_" + UnityEngine.Random.Range(1000, 9999);

        NetworkManager.Instance.CreateRoom(roomName);
        CloseLobby();
    }

    // ルームの数だけUIを作成もしくは更新する.
    void RefreshRoomList(List<SessionInfo> sessions)
    {
        if (sessions == null)
        {
            Debug.LogWarning("[LobbyUIManager] RefreshRoomList called with null sessions.");
            return;
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            // UIを取得。なければ生成.
            var ui = GetItem(i);
            if (ui == null)
            {
                Debug.LogWarning($"[LobbyUIManager] GetItem returned null for index {i}. Skipping.");
                continue;
            }

            // 生成時に表示.
            ui.gameObject.SetActive(true);

            // ルームの情報をUIに渡す.
            ui.Init(
                sessions[i].Name,
                sessions[i].PlayerCount,
                sessions[i].MaxPlayers,
                sessions[i].IsOpen
                );
        }

        // UIの数がルーム数より多ければ不要なUIを非表示に.
        for (int i = sessions.Count; i < roomUIs.Count; i++)
        {
            if (roomUIs[i] != null)
                roomUIs[i].gameObject.SetActive(false);
        }
    }

    // UI取得関数.
    RoomListUI GetItem(int index)
    {
        // 既に存在する場合は使いまわす.
        if (index < roomUIs.Count)
            return roomUIs[index];

        // なければ新しく生成.
        if (roomPrefab == null || parent == null)
        {
            Debug.LogError("[LobbyUIManager] roomPrefab or parent is null when trying to Instantiate. Check Inspector.");
            return null;
        }

        var ui = Instantiate(roomPrefab, parent);
        if (ui == null)
        {
            Debug.LogError("[LobbyUIManager] Instantiate returned null for roomPrefab. Is it a valid prefab with RoomListUI component?");
            return null;
        }

        roomUIs.Add(ui);
        return ui;
    }
}
