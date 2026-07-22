using Tanks.Complete;
using UnityEngine;
using UnityEngine.UI;

public class VehicleSelectItem : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] Text nameText;
    [SerializeField] Button button;

    VehicleType vehicleType;

    public void Setup(VehicleDatabase.VehicleData data)
    {
        vehicleType = data.type;

        iconImage.sprite = data.icon;
        nameText.text = data.vehicleName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        VehicleSelectUI.Instance.SelectVehicle(vehicleType);
    }
}
