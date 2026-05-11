using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Scriptable Objects/VehicleDatabase")]
public class VehicleDatabase : ScriptableObject
{
    public NetworkPrefabRef[] vehicles;
    static VehicleDatabase _instance;

    public static VehicleDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<VehicleDatabase>("VehicleDatabase");
            }
            return _instance;
        }
    }

    public NetworkPrefabRef Get(int vehicleID)
    {
        return vehicles[vehicleID];
    }
}
