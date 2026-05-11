using UnityEngine;
using System.Collections.Generic;

// プレイヤー一覧UI全体を管理.
public class PlayerListManager : MonoBehaviour
{
    public static PlayerListManager Instance;

    [SerializeField] PlayerListItem itemPrefab;
    [SerializeField] Transform listParent;

    List<PlayerListItem> items = new();

    void Awake()
    {
        Instance = this;
    }

    public void Refresh()
    {
        foreach (var item in items)
            Destroy(item.gameObject);

        items.Clear();

        var runner = NetworkManager.Instance.Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(player, out var obj))
            {
                var networkPlayer = obj.GetComponent<NetworkPlayer>();

                var ui = Instantiate(itemPrefab, listParent);
                ui.Set(networkPlayer);

                items.Add(ui);
            }
        }
    }
}
