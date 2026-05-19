using System;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance;
    
    public ConnectionState State { get; private set; }

    public event Action<ConnectionState> OnStateChanged;
    
    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        NetworkManager.Instance.OnLobbyJoined += OnLobbyJoined;
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.OnLobbyJoined -= OnLobbyJoined;
    }

    public void SetState(ConnectionState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);

        Debug.Log($"Connection State : {state}");
    }

    public void ConnectingLobby()
    {
        SetState(ConnectionState.ConnectingLobby);
    }

    void OnLobbyJoined()
    {
        SetState(ConnectionState.InLobby);
    }

    public void ConnectingRoom()
    {
        SetState(ConnectionState.ConnectingRoom);
    }

    public void JoinedRoom()
    {
        SetState(ConnectionState.InRoom);
    }

    public void Disconnected()
    {
        SetState(ConnectionState.Disconnected);
    }

    public void Failed()
    {
        SetState(ConnectionState.Failed);
    }
}
