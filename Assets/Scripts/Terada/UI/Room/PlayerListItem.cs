using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] Text nameText;
    [SerializeField] Text winsText;

    [SerializeField] Image readyIcon;
    [SerializeField] Sprite readySprite;
    [SerializeField] Sprite notReadySprite;

    NetworkPlayer player;

    public void SetPlaceholder(PlayerRef player)
    {
        nameText.text = $"Player {player.PlayerId}";
        readyIcon.sprite = notReadySprite;
    }

    public void Bind(NetworkPlayer p)
    {
        if (p == null)
            return;

        player = p;
        UpdateView();
    }

    public void UpdateView()
    {
        if (player == null)
            return;

        nameText.text = player.PlayerName.ToString();
        winsText.text = player.Wins.ToString();
        readyIcon.sprite = player.IsReady ? readySprite : notReadySprite;
    }

}
