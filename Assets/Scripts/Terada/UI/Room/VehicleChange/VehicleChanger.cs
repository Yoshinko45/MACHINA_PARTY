using UnityEngine;

public class VehicleChanger : MonoBehaviour, IInteractable
{
    public void Interact(NetworkPlayer player)
    {
        VehicleSelectUI.Instance.Open();
    }

}
