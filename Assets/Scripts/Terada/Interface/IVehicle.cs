using UnityEngine;

/// <summary>
/// マシン用インターフェース.
/// </summary>
public interface IVehicle
{
    void SetInput(PlayerInputData data);
    Transform GetTransform();
}
