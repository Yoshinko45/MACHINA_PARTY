using Fusion;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] NetworkPrefabRef playerPrefab;
    [SerializeField] NetworkPrefabRef testCarPrefab;

    void Start()
    {
        NetworkManager.Instance.OnPlayerJoinedEvent += SpawnPlayer;
        NetworkManager.Instance.OnPlayerLeftEvent += RemovePlayer;
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.OnPlayerJoinedEvent -= SpawnPlayer;
        NetworkManager.Instance.OnPlayerLeftEvent -= RemovePlayer;
    }

    void SpawnPlayer(PlayerRef player)
    {
        var runner = NetworkManager.Instance.Runner;

        if (!runner.IsServer)
            return;

        Vector3 pos = Random.insideUnitSphere * 3f;
        pos.y = 1;
        var playerObj = 
            runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        var car = 
            runner.Spawn(testCarPrefab, Vector3.forward * 2,  Quaternion.identity, player);

        runner.SetPlayerObject(player, playerObj);

        var networkPlayer = playerObj.GetComponent<NetworkPlayer>();

        networkPlayer.SetVehicle(car);
    }

    void RemovePlayer(PlayerRef player)
    {
        var runner = NetworkManager.Instance.Runner;

        if (!runner.IsServer)
            return;

        if (runner.TryGetPlayerObject(player, out var playerObj))
        {
            runner.Despawn(playerObj);
        }
    }
}
