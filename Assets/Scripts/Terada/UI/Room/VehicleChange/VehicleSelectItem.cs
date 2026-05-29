using UnityEngine;
using UnityEngine.UI;

public class VehicleSelectItem : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] Text nameText;
    [SerializeField] Button button;

    int vehicleID;

    public void Setup(int id, VehicleDatabase.VehicleData data)
    {
        vehicleID = id;

        iconImage.sprite = data.icon;
        nameText.text = data.vehicleName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        VehicleSelectUI.Instance.SelectVehicle(vehicleID);
    }
}
