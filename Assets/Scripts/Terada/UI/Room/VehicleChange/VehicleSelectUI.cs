using UnityEngine;

public class VehicleSelectUI : MonoBehaviour
{
    public static VehicleSelectUI Instance;

    [SerializeField] GameObject panel;
    [SerializeField] VehicleSelectItem itemPrefab;
    [SerializeField] Transform contentParent;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CreateVehicleList();
    }

    void CreateVehicleList()
    {
        VehicleDatabase db = VehicleDatabase.Instance;

        for (int i = 0; i < db.vehicles.Length; i++)
        {
            VehicleSelectItem item = Instantiate(itemPrefab, contentParent);

            item.Setup(i, db.vehicles[i]);
        }
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
