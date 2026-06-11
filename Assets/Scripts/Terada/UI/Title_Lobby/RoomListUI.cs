using UnityEngine;
using UnityEngine.UI;

public class RoomListUI : MonoBehaviour
{
    [SerializeField] private Text roomNameLabel;
    [SerializeField] private Text playerCount;
    [SerializeField] private Button joinButton;
    [HideInInspector] public string roomName = null;

    // UI内のパラメータを反映させる
    public void Init(string roomName, int players, int maxPlayers, bool isOpen)
    {
        gameObject.SetActive(true);

        if (joinButton != null)
        {
            joinButton.interactable = isOpen;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(Join);
        }

        if (roomNameLabel != null)
            roomNameLabel.text = this.roomName = roomName;
        else
            this.roomName = roomName;
        
        if (playerCount != null)
            playerCount.text = $"{players}/{maxPlayers}";
    }

    // ルーム入室処理.
    public void Join()
    {
        NetworkManager.Instance.JoinRoom(roomName);
    }
}
