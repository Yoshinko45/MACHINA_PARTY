using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] Text nameText;
    [SerializeField] Text readyText;

    NetworkPlayer player;

    public void Set(NetworkPlayer p)
    {
        player = p;
    }

    void Update()
    {
        if (player == null) return;

        nameText.text = player.PlayerName.ToString();
        readyText.text = player.IsReady ? "Ready" : "Not Ready";
    }
}
