using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Scriptable Objects/VehicleDatabase")]
public class VehicleDatabase : ScriptableObject
{
    [System.Serializable]
    public class VehicleData
    {
        public string vehicleName;
        public Sprite icon;
        public NetworkPrefabRef prefab;
        public float maxSpeed;
    }

    public VehicleData[] vehicles;     // マシンのリスト.
    static VehicleDatabase _instance;

    public static VehicleDatabase Instance
    {
        get
        {
            // シングルトンを取得.
            if (_instance == null)
            {
                _instance = Resources.Load<VehicleDatabase>("VehicleDatabase");

                Debug.Log(_instance);
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
    public VehicleData Get(int vehicleID)
    {
        return vehicles[vehicleID];
    }

    public NetworkPrefabRef GetPrefab(int vehicleID)
    {
        return vehicles[vehicleID].prefab;
    }
}
