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
        joinButton.interactable = isOpen;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(Join);
        roomNameLabel.text = this.roomName = roomName;
        playerCount.text = $"{players}/{maxPlayers}";
    }

    // ルーム入室処理.
    public void Join()
    {
        NetworkManager.Instance.JoinRoom(roomName);
    }
}
