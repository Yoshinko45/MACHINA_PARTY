using UnityEngine;

public class ReadyButton : MonoBehaviour
{
    public void OnReady()
    {
        var player = NetworkPlayer.LocalPlayer;

        if (player == null) return;

        player.RPC_Ready(!player.IsReady);
    }

}
