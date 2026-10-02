using Fusion;
using System.Linq;
using Tanks.Complete;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField]
    float startDelay = 3f;

    [SerializeField]
    float endDelay = 3f;

    [SerializeField]
    int roundsToWin = 5;

    [SerializeField]
    SpawnPoint[] tankSpawnPoints;

    [SerializeField]
    SpawnPoint[] raceSpawnPoints;

    [SerializeField]
    Text titleText;

    [Networked]
    int Round { get; set; }

    [Networked]
    TickTimer StateTimer { get; set; }

    [Networked]
    GameState State { get; set; }

    [Networked]
    bool ReturningToRoom { get; set; }

    enum GameState
    {
        Waiting,
        RoundStarting,
        Playing,
        RoundEnding
    }

    // ==================================================
    // Spawned
    // ==================================================

    public override void Spawned()
    {
        Instance = this;

        Debug.Log(
            $"[GameManager] Spawned " +
            $"Objects=" +
            $"{Runner.GetAllNetworkObjects().Count()}");

        if (titleText == null)
        {
            GameObject obj =
                GameObject.Find(
                    "TitleText");

            if (obj != null)
            {
                titleText =
                    obj.GetComponent<Text>();
            }
        }

        if (HasStateAuthority)
        {
            Round = 0;

            State =
                GameState.Waiting;

            ReturningToRoom = false;
        }
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================================================
    // Game Start
    // ==================================================

    public void BeginGame()
    {
        if (!HasStateAuthority)
            return;

        if (RoomState.Instance == null)
            return;

        if (!RoomState.Instance.GameStarted)
            return;

        if (State != GameState.Waiting)
            return;

        if (ReturningToRoom)
            return;

        Debug.Log(
            "[GameManager] BeginGame");

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

            networkPlayer.Wins = 0;
        }

        Round = 0;

        StateTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                0.5f);

        // 0.5ïbå„Ç…StartRound
        State =
            GameState.Waiting;
    }

    // ==================================================
    // Network
    // ==================================================

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (RoomState.Instance == null)
            return;

        // ÉQÅ[ÉÄèIóπå„ÇÃRoomïúãAë“Çø
        if (ReturningToRoom)
        {
            if (StateTimer.Expired(Runner))
            {
                ReturnToRoom();
            }

            return;
        }


        // RoomíÜÇÕGameManagerÇí‚é~
        if (!RoomState.Instance.GameStarted)
            return;

        switch (State)
        {
            case GameState.Waiting:

                if (StateTimer.Expired(Runner))
                {
                    StartRound();
                }

                break;

            case GameState.RoundStarting:

                UpdateRoundStarting();

                break;

            case GameState.Playing:

                UpdatePlaying();

                break;

            case GameState.RoundEnding:

                UpdateRoundEnding();

                break;
        }
    }

    // ==================================================
    // Round
    // ==================================================

    void StartRound()
    {
        if (!RoomState.Instance.GameStarted)
            return;

        Round++;

        ResetPlayers();

        State =
            GameState.RoundStarting;

        StateTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                startDelay);

        RPC_ShowMessage(
            $"ROUND {Round}");
    }

    void UpdateRoundStarting()
    {
        if (!StateTimer.Expired(Runner))
            return;

        State =
            GameState.Playing;

        RPC_ShowMessage("");
    }

    void UpdatePlaying()
    {
        if (!OnePlayerLeft())
            return;

        State =
            GameState.RoundEnding;

        StateTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                endDelay);

        NetworkPlayer winner =
            GetRoundWinner();

        if (winner != null)
        {
            winner.Wins++;
        }

        RPC_ShowMessage(
            winner == null
                ? "DRAW"
                : $"{winner.PlayerName} Wins");
    }

    void UpdateRoundEnding()
    {
        if (!StateTimer.Expired(Runner))
            return;

        NetworkPlayer gameWinner =
            GetGameWinner();

        if (gameWinner != null)
        {
            RPC_ShowGameWinner(
                gameWinner.PlayerName);

            // Åö ÉQÅ[ÉÄèIóπå„5ïb
            ReturningToRoom = true;

            StateTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    5f);

            return;
        }

        StartRound();
    }

    // ==================================================
    // Return To Room
    // ==================================================

    void ReturnToRoom()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log(
            "[GameManager] " +
            "Returning to Room");

        SpawnManager.Instance?
            .ReturnPlayersToRoom();

        // GameèIóπ
        RoomState.Instance.GameStarted =
            false;

        RoomState.Instance.VoteStarted =
            false;

        ReturningToRoom = false;

        Round = 0;

        State =
            GameState.Waiting;

        StateTimer = default;

        // Ready / VoteÇÉäÉZÉbÉg
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

            networkPlayer.IsReady = false;
            networkPlayer.HasVoted = false;
            networkPlayer.Wins = 0;
        }

        RPC_ShowMessage("");
    }

    // ==================================================
    // Players
    // ==================================================

    bool OnePlayerLeft()
    {
        int alive = 0;

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

            NetworkObject vehicle =
                networkPlayer.VehicleObject;

            if (vehicle == null)
                continue;

            Tank_Health health =
                vehicle.GetComponent<
                    Tank_Health>();

            if (health == null)
                continue;

            if (health.IsDead)
                continue;

            alive++;
        }

        return alive <= 1;
    }

    NetworkPlayer GetRoundWinner()
    {
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

            if (networkPlayer == null ||
                networkPlayer.VehicleObject ==
                null)
            {
                continue;
            }

            Tank_Health health =
                networkPlayer.VehicleObject
                    .GetComponent<
                        Tank_Health>();

            if (health == null)
                continue;

            if (!health.IsDead)
                return networkPlayer;
        }

        return null;
    }

    NetworkPlayer GetGameWinner()
    {
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

            if (networkPlayer != null &&
                networkPlayer.Wins >= roundsToWin)
            {
                return networkPlayer;
            }
        }

        return null;
    }

    // ==================================================
    // Reset Players
    // ==================================================

    void ResetPlayers()
    {
        int index = 0;

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

            if (networkPlayer == null ||
                networkPlayer.VehicleObject ==
                null)
            {
                continue;
            }

            Transform spawn =
                GetSpawn(index);

            if (spawn == null)
                continue;

            networkPlayer.VehicleObject
                .transform
                .SetPositionAndRotation(
                    spawn.position,
                    spawn.rotation);

            Rigidbody rb =
                networkPlayer.VehicleObject
                    .GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity =
                    Vector3.zero;

                rb.angularVelocity =
                    Vector3.zero;
            }

            Tank_Health health =
                networkPlayer.VehicleObject
                    .GetComponent<Tank_Health>();

            if (health != null)
            {
                health.ResetTank();
            }

            index++;
        }
    }

    Transform GetSpawn(int index)
    {
        if (RoomState.Instance ==
            null)
        {
            return null;
        }

        if (RoomState.Instance.CurrentMode ==
            GameModes.Tank)
        {
            if (tankSpawnPoints == null ||
                tankSpawnPoints.Length == 0)
            {
                return transform;
            }

            if (index >= tankSpawnPoints.Length)
                index = 0;

            return tankSpawnPoints[index]
                .transform;
        }

        if (raceSpawnPoints == null ||
            raceSpawnPoints.Length == 0)
        {
            return transform;
        }

        if (index >= raceSpawnPoints.Length)
            index = 0;

        return raceSpawnPoints[index]
            .transform;
    }

    // ==================================================
    // UI
    // ==================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All)]
    void RPC_ShowMessage(
        string message)
    {
        if (titleText != null)
        {
            titleText.text =
                message;
        }
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All)]
    void RPC_ShowGameWinner(
        NetworkString<_16> winner)
    {
        if (titleText != null)
        {
            titleText.text =
                $"{winner} Wins the Game!";
        }
    }
}