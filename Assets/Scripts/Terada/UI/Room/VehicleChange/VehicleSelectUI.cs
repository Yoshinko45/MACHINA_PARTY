using Tanks.Complete;
using UnityEngine;

public class VehicleSelectUI : MonoBehaviour
{
    public static VehicleSelectUI Instance;

    [SerializeField] GameObject panel;
    [SerializeField] VehicleSelectItem itemPrefab;
    [SerializeField]Transform[] pages; 

    int currentPage = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CreateVehicleList(GameModes.Tank, pages[0]);
        CreateVehicleList(GameModes.Race, pages[1]);
    }

    void Update()
    {
        if (!panel.activeSelf)
            return;

        if (InputManager.Instance.ConsumePageLeftPressed())
        {
            PrevPage();
        }

        if (InputManager.Instance.ConsumePageRightPressed())
        {
            NextPage();
        }
    }

    void CreateVehicleList(GameModes mode, Transform parent)
    {
        foreach (var data in VehicleDatabase.Instance.vehicles)
        {
            if (data.gameModes != mode)
                continue;

            var item = Instantiate(itemPrefab, parent);

            item.Setup(data);
        }
    }

    public void NextPage()
    {
        currentPage++;
        if (currentPage >= pages.Length)
            currentPage = 0;
        ShowPage(currentPage);
    }

    public void PrevPage()
    {
        currentPage--;

        if (currentPage < 0)
            currentPage = pages.Length -1;

        ShowPage(currentPage);
    }

    void ShowPage(int page)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].gameObject.SetActive(i == page);
        }
    }

    public void Open()
    {
        panel.SetActive(true);
        ShowPage(currentPage);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    public void SelectVehicle(VehicleType type)
    {
        var runner = NetworkManager.Instance.Runner;

        var player = NetworkPlayer.LocalPlayer;

        if (player == null)
            return;

        player.RPC_ChangeVehicle(
            GameSettings.Instance.CurrentMode,
            type
        );

        Close();
    }
}
