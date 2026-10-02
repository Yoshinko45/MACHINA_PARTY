using Fusion;
using Tanks.Complete;
using UnityEngine;

public class RoomManager : NetworkBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField]
    GameObject voteCanvas;

    [Networked]
    public bool Voting { get; set; }

    // ==================================================
    // Unity
    // ==================================================

    void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        Instance = this;

        Debug.Log(
            "[RoomManager] Spawned");
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================================================
    // Network
    // ==================================================

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

    // ==================================================
    // Ready
    // ==================================================

    public bool AllReady()
    {
        foreach (var player in
            Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                player,
                out var obj))
            {
                return false;
            }

            NetworkPlayer networkPlayer =
                obj.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                return false;

            if (!networkPlayer.IsReady)
                return false;
        }

        return true;
    }

    // ==================================================
    // Vote
    // ==================================================

    public void BeginVote()
    {
        if (!HasStateAuthority)
            return;

        if (Voting)
            return;

        Voting = true;

        foreach (var player in
            Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                player,
                out var obj))
            {
                continue;
            }

            NetworkPlayer networkPlayer =
                obj.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                continue;

            networkPlayer.HasVoted = false;
        }

        RPC_ShowVoteUI(true);
    }

    bool AllVoted()
    {
        foreach (var player in
            Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                player,
                out var obj))
            {
                return false;
            }

            NetworkPlayer networkPlayer =
                obj.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                return false;

            if (!networkPlayer.HasVoted)
                return false;
        }

        return true;
    }

    void FinishVote()
    {
        if (!HasStateAuthority)
            return;

        int tankVotes = 0;
        int raceVotes = 0;

        foreach (var player in
            Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                player,
                out var obj))
            {
                continue;
            }

            NetworkPlayer networkPlayer =
                obj.GetComponent<
                    NetworkPlayer>();

            if (networkPlayer == null)
                continue;

            if (networkPlayer.Vote ==
                GameModes.Tank)
            {
                tankVotes++;
            }
            else
            {
                raceVotes++;
            }
        }

        GameModes result =
            tankVotes >= raceVotes
                ? GameModes.Tank
                : GameModes.Race;

        // ==================================================
        // RoomState
        // ==================================================

        if (RoomState.Instance == null)
        {
            Debug.LogError(
                "[RoomManager] " +
                "RoomState is null.");

            return;
        }

        RoomState.Instance.CurrentMode =
            result;

        RoomState.Instance.VoteStarted =
            false;

        // Åö SceneëJà⁄Ç≈ÇÕÇ»Ç≠ÉQÅ[ÉÄäJén
        RoomState.Instance.GameStarted =
            true;

        Voting = false;

        RPC_ShowVoteUI(false);

        Debug.Log(
            $"[RoomManager] " +
            $"Vote finished : {result}");

        // Åö VehicleÇGameStageÇ÷à⁄ìÆ
        SpawnManager.Instance?
            .StartGame();
    }

    // ==================================================
    // UI
    // ==================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All)]
    void RPC_ShowVoteUI(
        bool show)
    {
        if (voteCanvas != null)
            voteCanvas.SetActive(show);
    }
}