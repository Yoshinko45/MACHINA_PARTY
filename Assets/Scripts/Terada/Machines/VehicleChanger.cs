using UnityEngine;

//　VehicleSelectUIをつけているCanvasは出しておいてPanelのみ非表示にしとくあとでQuitボタンも表示させるように。
public class VehicleChanger : MonoBehaviour, IInteractable
{
    bool carInteract;

    public void Interact(NetworkPlayer player)
    {
        VehicleSelectUI.Instance.Open();
    }

}
