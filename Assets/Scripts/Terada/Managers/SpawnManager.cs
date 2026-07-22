using Fusion;
using Tanks.Complete;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Unity.Collections.Unicode;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    public GameModes CurrentMode { get; set; }

    [SerializeField] NetworkPrefabRef playerPrefab;
    [SerializeField] NetworkPrefabRef roomStatePrefab;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

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

        if (RoomState.Instance == null)
        {
            runner.Spawn(roomStatePrefab, Vector3.zero, Quaternion.identity);
        }

        Vector3 pos = Vector3.zero;
        // プレイヤーと車を生成（プレイヤーは実体なし）.
        var playerObj = runner.Spawn(playerPrefab, pos, Quaternion.identity, player);

        //車両の選択、紐づけのためにプレイヤーを取得.
       var networkPlayer = playerObj.GetComponent<NetworkPlayer>();

        // PlayerObjectを登録.
        runner.SetPlayerObject(player, playerObj);

        networkPlayer.SpawnVehicle(CurrentMode);

        Debug.Log("== SpawnPlayer ==");
        Debug.Log($"Flags = {playerObj.Flags}");
        Debug.Log($"Valid = {playerObj.IsValid}");
        Debug.Log($"Id = {playerObj.Id}");
        Debug.Log($"Scene = {playerObj.gameObject.scene.name}");

        Debug.Log($"TryGet = {runner.TryGetPlayerObject(player, out var check)}");
        Debug.Log($"Same = {ReferenceEquals(check, playerObj)}");

        Debug.Log($"Runner scene={runner.gameObject.scene.name}");
        Debug.Log($"Player scene={playerObj.gameObject.scene.name}");
    }

    /// <summary>
    /// Playerを取得して削除する.
    /// </summary>
    /// <param name="player"></param>
    void RemovePlayer(PlayerRef player)
    {
        Debug.Log("RemovePlayer");
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

    public void SpawnAllPlayers()
    {
        foreach (var player in NetworkManager.Instance.Runner.ActivePlayers)
        {
            if (!NetworkManager.Instance.Runner.TryGetPlayerObject(player, out var obj))
                continue;

            var networkPlayer = obj.GetComponent<NetworkPlayer>();

            if (networkPlayer.VehicleObject == null)
                networkPlayer.SpawnVehicle(RoomState.Instance.CurrentMode);
        }
    }
}
