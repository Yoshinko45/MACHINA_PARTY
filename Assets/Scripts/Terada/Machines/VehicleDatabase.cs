using Fusion;
using Tanks.Complete;
using UnityEngine;

[CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Scriptable Objects/VehicleDatabase")]
public class VehicleDatabase : ScriptableObject
{
    [System.Serializable]
    public class VehicleData
    {
        public GameModes gameModes;
        public VehicleType type;
        public string vehicleName;
        public Sprite icon;
        public NetworkPrefabRef prefab;
        public float maxSpeed;
    }

    [SerializeField]
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
            }
            return _instance;
        }
    }

    /// <summary>
    /// 車両の取得.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public VehicleData Get(VehicleType type)
    {
        foreach (var vehicle in vehicles)
        {
            if (vehicle.type == type)
            {
                return vehicle;
            }
        }
        return null;
    }

    public NetworkPrefabRef GetPrefab(VehicleType type)
    {
        VehicleData data = Get(type);

        return data != null ? data.prefab : default;
    }
}
