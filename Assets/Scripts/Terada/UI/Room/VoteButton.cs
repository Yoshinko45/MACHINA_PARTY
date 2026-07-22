using UnityEngine;

public class VoteButton : MonoBehaviour
{
    [SerializeField]
    GameModes mode;

    public void Vote()
    {
        var player = NetworkPlayer.LocalPlayer;

        if (player == null)
            return;

        player.RPC_Vote(mode);
    }
}