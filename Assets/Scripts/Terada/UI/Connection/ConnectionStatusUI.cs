using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class ConnectionStatusUI : MonoBehaviour
{
     public static ConnectionStatusUI Instance;

    [SerializeField] Text label;
    [SerializeField] float visibleTime = 2f;

    int showVersion = 0;

    void Start()
    {
        ConnectionManager.Instance.OnStateChanged += UpdateUI;
        label.gameObject.SetActive(false);

        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (ConnectionManager.Instance != null)
        {
            ConnectionManager.Instance.OnStateChanged -= UpdateUI;
        }
    }

    void UpdateUI(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.ConnectingLobby:
                ShowPersistent("Connecting Lobby...");
                break;

            case ConnectionState.ConnectingRoom:
                ShowPersistent("Joining Room...");
                break;

            case ConnectionState.InLobby:
                ShowTemporary("Lobby Connected");
                break;

            case ConnectionState.InRoom:
                ShowTemporary("Joined Room");
                break;

            case ConnectionState.Disconnected:
                ShowPersistent("Disconnected");
                break;

            case ConnectionState.Reconnecting:
                ShowPersistent("Reconnecting");
                break;

            case ConnectionState.Failed:
                ShowPersistent("Connection Failed");
                break;
        }
    }

    void ShowPersistent(string text)
    {
        showVersion++;

        label.text = text;
        label.gameObject.SetActive(true);
    }

    async void ShowTemporary(string text)
    {
        showVersion++;

        int current = showVersion;

        label.text = text;
        label.gameObject.SetActive(true);

        await Task.Delay((int)(visibleTime * 1000));

        if (current == showVersion)
        {
            label.gameObject.SetActive(false);
        }
    }
}