using UnityEngine;

public class VehicleSelectUI : MonoBehaviour
{
    public static VehicleSelectUI Instance;

    [SerializeField] GameObject panel;

    private void Awake()
    {
        Instance = this;
    }

    public void Open()
    {
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    public void SelectVehicle(int id)
    {
        NetworkPlayer.LocalPlayer.RPC_ChangeVehicle(id);

        Close();
    }
}
