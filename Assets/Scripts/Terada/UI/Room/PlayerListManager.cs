using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// プレイヤー一覧UI全体を管理.
public class PlayerListManager : MonoBehaviour
{
    public static PlayerListManager Instance;

    [SerializeField] PlayerListItem itemPrefab;
    [SerializeField] Transform listParent;

    Dictionary<PlayerRef, PlayerListItem> items = new();

    void Awake()
    {
        Instance = this;
    }

    public void Refresh()
    {
        var runner = NetworkManager.Instance.Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (!items.TryGetValue(player, out var ui))
            {
                ui = Instantiate(itemPrefab, listParent);
                items[player] = ui;

                ui.SetPlaceholder(player);
            }

            if (runner.TryGetPlayerObject(player, out var obj))
            {
                var networkPlayer = obj.GetComponent<NetworkPlayer>();
                ui.Bind(networkPlayer);
            }
        }

        foreach (var key in new List<PlayerRef>(items.Keys))
        {
            if (!runner.ActivePlayers.Contains(key))
            {
                Destroy(items[key].gameObject);
                items.Remove(key);
            }
        }
    }
}
