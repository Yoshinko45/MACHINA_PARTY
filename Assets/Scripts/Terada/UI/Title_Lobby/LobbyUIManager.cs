using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] CanvasGroup lobbyPanel;    // ロビーのキャンバスグループ.
    [SerializeField] RoomListUI roomPrefab;     // ルームリスト内の１つ１つのUI.
    [SerializeField] Transform parent;          // RoomListUIの親座標.

    List<RoomListUI> roomUIs = new();   // リストで生成したUIを保存.

    void Start()
    {
        // イベントの登録とロビーを非表示.
        NetworkManager.Instance.OnSessionListChanged += RefreshRoomList;    // ルーム更新時に呼ばれる.
        NetworkManager.Instance.OnLobbyJoined += OnLobbyJoined;     // ロビー接続成功時に呼ばれる.
        CloseLobby();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;
        // イベント解除.
        NetworkManager.Instance.OnSessionListChanged -= RefreshRoomList;
        NetworkManager.Instance.OnLobbyJoined -= OnLobbyJoined;
    }

    // ボタン押下でロビーに接続（成功したらパネル表示）.
    public void OpenLobby()
    {
        NetworkManager.Instance.JoinLobby();
    }

    // ロビーを非表示にする.
    public void CloseLobby()
    {
        lobbyPanel.alpha = 0;
        lobbyPanel.interactable = false;
        lobbyPanel.blocksRaycasts = false;
    }

    // ロビーを表示する.
    void OnLobbyJoined()
    {
        lobbyPanel.alpha = 1;
        lobbyPanel.interactable = true;
        lobbyPanel.blocksRaycasts = true;
    }

    // ボタン押下でランダムな名前のルームを作成.
    public void CreateRoomUI()
    {
        string roomName = "Room_" + Random.Range(1000, 9999);

        NetworkManager.Instance.CreateRoom(roomName);
        CloseLobby();
    }

    // ルームの数だけUIを作成もしくは更新する.
    void RefreshRoomList(List<SessionInfo> sessions)
    {
        for (int i = 0; i < sessions.Count; i++)
        {
            // UIを取得。なければ生成.
            var ui = GetItem(i);

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
        var ui = Instantiate(roomPrefab, parent);
        roomUIs.Add(ui);
        return ui;
    }
}
