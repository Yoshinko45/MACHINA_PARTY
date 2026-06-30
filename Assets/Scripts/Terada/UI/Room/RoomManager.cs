using Fusion;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [SerializeField] PlayerListManager playerList;

    void Start()
    {
        NetworkManager.Instance.OnPlayerLeftEvent += Refresh;
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.OnPlayerLeftEvent -= Refresh;
    }

    void OnPlayerLeft(PlayerRef player)
    {
        playerList.Refresh();
    }

    bool AllReady()
    {
        var runner = NetworkManager.Instance.Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(player, out var obj))
            {
                var networkPlayer = obj.GetComponent<NetworkPlayer>();

                if (!networkPlayer.IsReady) return false;
            }
        }

        return true;
    }

    void Refresh(Fusion.PlayerRef player)
    {
        playerList.Refresh();
    }
}
