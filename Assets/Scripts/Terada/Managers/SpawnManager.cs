using Fusion;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] NetworkPrefabRef playerPrefab;
    [SerializeField] NetworkPrefabRef testCarPrefab;

    void Start()
    {
        // イベント登録.
        NetworkManager.Instance.OnPlayerJoinedEvent += SpawnPlayer;
        NetworkManager.Instance.OnPlayerLeftEvent += RemovePlayer;
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;

        // イベント解除.
        NetworkManager.Instance.OnPlayerJoinedEvent -= SpawnPlayer;
        NetworkManager.Instance.OnPlayerLeftEvent -= RemovePlayer;
    }
    
    /// <summary>
    /// Playerの生成、紐づけ等.
    /// </summary>
    /// <param name="player"></param>
    void SpawnPlayer(PlayerRef player)
    {
        var runner = NetworkManager.Instance.Runner;

        // サーバーが生成（クライアント側では同期が壊れる）.
        if (!runner.IsServer)
            return;

        Vector3 pos = Random.insideUnitSphere * 3f;
        pos.y = 1.5f;
        // プレイヤーと車を生成（プレイヤーは実体なし）.
        var playerObj =
            runner.Spawn(playerPrefab, pos, Quaternion.identity, player);
        var car = 
            runner.Spawn(testCarPrefab, pos,  Quaternion.identity, player);

        // PlayerObjectを登録.
        runner.SetPlayerObject(player, playerObj);

        // Playerと車を紐づけ.
        var networkPlayer = playerObj.GetComponent<NetworkPlayer>();
        networkPlayer.SetVehicle(car);
    }

    /// <summary>
    /// Playerを取得して削除する.
    /// </summary>
    /// <param name="player"></param>
    void RemovePlayer(PlayerRef player)
    {
        var runner = NetworkManager.Instance.Runner;

        if (!runner.IsServer)
            return;

        if (runner.TryGetPlayerObject(player, out var playerObj))
        {
            var networkPlayer = playerObj.GetComponent<NetworkPlayer>();

            // 車が存在すれば削除
            if (networkPlayer.VehicleObject != null)
            {
                runner.Despawn(networkPlayer.VehicleObject);
            }
            runner.Despawn(playerObj);
        }
    }
}
