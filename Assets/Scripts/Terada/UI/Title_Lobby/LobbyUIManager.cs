using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] CanvasGroup lobbyPanel;
    [SerializeField] RoomListUI roomPrefab;
    [SerializeField] Transform parent;

    List<RoomListUI> roomUIs = new();

    void Start()
    {
        NetworkManager.Instance.OnSessionListChanged += RefreshRoomList;
        NetworkManager.Instance.OnLobbyJoined += OnLobbyJoined;
        CloseLobby();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.OnSessionListChanged -= RefreshRoomList;
        NetworkManager.Instance.OnLobbyJoined -= OnLobbyJoined;
    }

    public void OpenLobby()
    {
        NetworkManager.Instance.JoinLobby();
    }

    public void CloseLobby()
    {
        lobbyPanel.alpha = 0;
        lobbyPanel.interactable = false;
        lobbyPanel.blocksRaycasts = false;
    }

    void OnLobbyJoined()
    {
        lobbyPanel.alpha = 1;
        lobbyPanel.interactable = true;
        lobbyPanel.blocksRaycasts = true;
    }

    public void CreateRoomUI()
    {
        string roomName = "Room_" + Random.Range(1000, 9999);

        NetworkManager.Instance.CreateRoom(roomName);
        CloseLobby();
    }

    void RefreshRoomList(List<SessionInfo> sessions)
    {
        for (int i = 0; i < sessions.Count; i++)
        {
            var ui = GetItem(i);

            ui.Init(
                sessions[i].Name,
                sessions[i].PlayerCount,
                sessions[i].MaxPlayers,
                sessions[i].IsOpen
                );
        }

        for (int i = sessions.Count; i < roomUIs.Count; i++)
        {
            roomUIs[i].gameObject.SetActive(false);
        }
    }

    RoomListUI GetItem(int index)
    {
        if (index < roomUIs.Count)
            return roomUIs[index];

        var ui = Instantiate(roomPrefab, parent);
        roomUIs.Add(ui);
        return ui;
    }
}
