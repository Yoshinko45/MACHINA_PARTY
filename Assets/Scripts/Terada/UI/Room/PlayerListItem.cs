using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] Text nameText;

    [SerializeField] Image readyIcon;
    [SerializeField] Sprite readySprite;
    [SerializeField] Sprite notReadySprite;

    NetworkPlayer player;

    public void Set(NetworkPlayer p)
    {
        player = p;
    }

    void Update()
    {
        if (player == null) return;

        nameText.text = player.PlayerName.ToString();

        // 全員共通のReadyアイコン
        readyIcon.sprite = player.IsReady ? readySprite : notReadySprite;
    }
}
