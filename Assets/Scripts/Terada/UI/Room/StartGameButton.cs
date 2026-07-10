using Fusion;
using UnityEngine;

public class StartGameButton : MonoBehaviour
{
    void Start()
    {
        gameObject.SetActive(NetworkManager.Instance.Runner.IsServer);
    }

    public void StartGame()
    {
        var room = RoomManager.Instance;

        if (room == null)
            return;

        if (!room.AllReady())
            return;

        room.BeginVote();
    }
}