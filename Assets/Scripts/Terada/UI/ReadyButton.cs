using Fusion;
using UnityEngine;

public class ReadyButton : MonoBehaviour
{
    NetworkPlayer localPlayer;

    void FindPlayer()
    {
        foreach (var p in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (p.Object.HasInputAuthority)
            {
                localPlayer = p;
                break;
            }
        }
    }

    public void OnReady()
    {
        if (localPlayer == null) return;

        localPlayer.RPC_Ready(!localPlayer.IsReady);
    }

    private void Update()
    {
        if (localPlayer != null) return;

        FindPlayer();
    }
}
