using Fusion;
using Tanks.Complete;

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

        if (Object.HasStateAuthority)
        {
            VoteStarted = false;
            GameStarted = false;
            CurrentMode = GameModes.Tank;
        }

        UnityEngine.Debug.Log(
            $"[RoomState] Spawned " +
            $"Mode={CurrentMode}");
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }
}