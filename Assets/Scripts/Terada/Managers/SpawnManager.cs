using Fusion;
using Tanks.Complete;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameModes CurrentMode { get; set; }

    [SerializeField] NetworkPrefabRef playerPrefab;
    //[SerializeField] NetworkPrefabRef carPrefab;

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
    /// Playerの生成.
    /// </summary>
    /// <param name="player"></param>
    void SpawnPlayer(PlayerRef player)
    {
        var runner = NetworkManager.Instance.Runner;

        // サーバーが生成（クライアント側では同期が壊れる）.
        if (!runner.IsServer)
            return;

        Vector3 pos = Vector3.zero;
        // プレイヤーと車を生成（プレイヤーは実体なし）.
        var playerObj = runner.Spawn(playerPrefab, pos, Quaternion.identity, player);

        //車両の選択、紐づけのためにプレイヤーを取得.
       var networkPlayer = playerObj.GetComponent<NetworkPlayer>();

        // PlayerObjectを登録.
        runner.SetPlayerObject(player, playerObj);

        networkPlayer.SpawnVehicle(CurrentMode);
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
