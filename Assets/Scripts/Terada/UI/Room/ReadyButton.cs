using UnityEngine;
using UnityEngine.UI;

public class ReadyButton : MonoBehaviour
{
    [SerializeField] Text readyText;

    void Update()
    {
        var player = NetworkPlayer.LocalPlayer;
        if (player == null) return;

        readyText.text = player.IsReady ? "Ready!!" : "Ready??";
    }

    public void OnReady()
    {
        var player = NetworkPlayer.LocalPlayer;

        if (player == null) return;

        player.RPC_Ready(!player.IsReady);
    }

}