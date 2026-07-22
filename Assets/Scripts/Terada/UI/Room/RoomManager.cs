using Fusion;
using Tanks.Complete;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomManager : NetworkBehaviour
{
    public static RoomManager Instance;

    [SerializeField] GameObject voteCanvas;

    [Networked]
    public bool Voting { get; set; }

    void Awake()
    {
        Instance = this;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!Voting)
            return;

        if (!AllVoted())
            return;

        FinishVote();
    }

    public bool AllReady()
    {
        var runner = Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out var obj))
                return false;

            var networkPlayer =
                obj.GetComponent<NetworkPlayer>();

            if (!networkPlayer.IsReady)
                return false;
        }

        return true;
    }

    bool AllVoted()
    {
        var runner = Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out var obj))
                return false;

            var networkPlayer =
                obj.GetComponent<NetworkPlayer>();

            if (!networkPlayer.HasVoted)
                return false;
        }

        return true;
    }

    public void BeginVote()
    {
        if (!HasStateAuthority)
            return;

        Voting = true;

        foreach (var player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out var obj))
            {
                var p = obj.GetComponent<NetworkPlayer>();

                p.HasVoted = false;
            }
        }

        RPC_ShowVoteUI(true);
    }

    void FinishVote()
    {
        Voting = false;

        int tank = 0;
        int race = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            Runner.TryGetPlayerObject(player, out var obj);

            var p = obj.GetComponent<NetworkPlayer>();

            if (p.Vote == GameModes.Tank)
                tank++;
            else
                race++;
        }

        GameModes result =
            tank >= race
            ? GameModes.Tank
            : GameModes.Race;

        RoomState.Instance.CurrentMode = result;

        RPC_ShowVoteUI(false);

        foreach (var player in Runner.ActivePlayers)
        {
            Debug.Log($"=== Before LoadScene {player} ===");

            if (Runner.TryGetPlayerObject(player, out var obj))
            {
                Debug.Log($"Id={obj.Id}");
                Debug.Log($"Valid={obj.IsValid}");
                Debug.Log($"Scene={obj.gameObject.scene.name}");
            }
            else
            {
                Debug.Log("PlayerObject NOT FOUND");
            }
        }
        // index2(GameScene)
        NetworkManager.Instance.Runner.LoadScene(SceneRef.FromIndex(2));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ShowVoteUI(bool show)
    {
        if (voteCanvas != null)
            voteCanvas.SetActive(show);
    }
}