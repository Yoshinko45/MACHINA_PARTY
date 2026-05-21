using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Scriptable Objects/VehicleDatabase")]
public class VehicleDatabase : ScriptableObject
{
    public NetworkPrefabRef[] vehicles;     // マシンのリスト.
    static VehicleDatabase _instance;

    public static VehicleDatabase Instance
    {
        get
        {
            // シングルトンを取得.
            if (_instance == null)
            {
                _instance = Resources.Load<VehicleDatabase>("VehicleDatabase");
            }
            return _instance;
        }
    }

    /// <summary>
    /// 車の取得.
    /// 使用例：VehivleDatabase.Instance.Get(3);
    /// </summary>
    /// <param name="vehicleID"></param>
    /// <returns></returns>
    public NetworkPrefabRef Get(int vehicleID)
    {
        return vehicles[vehicleID];
    }
}
