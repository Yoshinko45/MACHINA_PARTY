using Fusion;
using UnityEngine;

public enum GameModes
{
    Tank,
    Race
}

public class RoomState : NetworkBehaviour
{
    public static RoomState Instance { get; private set; }

    [Networked]
    public bool VoteStarted { get; set; }

    [Networked]
    public bool GameStarted { get; set; }

    [Networked]
    public GameModes CurrentMode { get; set; }

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        CheckVoteFinished();
    }

    void CheckVoteFinished()
    {
        if (!VoteStarted)
            return;

        var runner = Runner;

        foreach (var player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out var obj))
                return;

            var p = obj.GetComponent<NetworkPlayer>();

            if (!p.HasVoted)
                return;
        }

        DecideMode();
    }

    void DecideMode()
    {
        int tank = 0;
        int race = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            Runner.TryGetPlayerObject(player, out var obj);

            var p = obj.GetComponent<NetworkPlayer>();

            switch (p.Vote)
            {
                case GameModes.Tank:
                    tank++;
                    break;

                case GameModes.Race:
                    race++;
                    break;
            }
        }

        CurrentMode =
            tank >= race
            ? GameModes.Tank
            : GameModes.Race;

        VoteStarted = false;
        GameStarted = true;

        Runner.LoadScene(SceneRef.FromIndex(2));
    }
}
