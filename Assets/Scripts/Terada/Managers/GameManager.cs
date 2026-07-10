using Fusion;
using System.Linq;
using Tanks.Complete;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [SerializeField] float startDelay = 3f;
    [SerializeField] float endDelay = 3f;
    [SerializeField] int roundsToWin = 5;

    [SerializeField]
    SpawnPoint[] tankSpawnPoints;

    [SerializeField]
    SpawnPoint[] raceSpawnPoints;

    [Networked]
    int Round { get; set; }

    [Networked]
    TickTimer StateTimer { get; set; }

    [Networked]
    GameState State { get; set; }

    [SerializeField] Text titleText;

    TickTimer delay;

    enum GameState
    {
        Waiting,
        RoundStarting,
        Playing,
        RoundEnding
    }

    public override void Spawned()
    {
        Debug.Log($"ObjectCount = {Runner.GetAllNetworkObjects().Count()}");
        foreach (var obj in Runner.GetAllNetworkObjects())
        {
            Debug.Log($"{obj.Id} {obj.name}");
        }

        Instance = this;

        titleText = GameObject
        .Find("TitleText")
        .GetComponent<Text>();

        if (HasStateAuthority)
        {
            delay = TickTimer.CreateFromSeconds(Runner, 1f);
            FindFirstObjectByType<SpawnManager>()
                .SpawnAllPlayers();

            Round = 0;
            State = GameState.Waiting;

            BeginGame();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (State)
        {
            case GameState.Waiting:
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

    public void BeginGame()
    {
        if (!HasStateAuthority)
            return;

        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                player,
                out var obj))
                continue;

            obj.GetComponent<NetworkPlayer>().Wins = 0;
        }

        Round = 0;

        StartRound();
    }

    void StartRound()
    {
        Round++;

        ResetPlayers();

        State = GameState.RoundStarting;

        StateTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                startDelay);

        RPC_ShowMessage($"ROUND {Round}");
    }

    void UpdateRoundStarting()
    {
        if (!StateTimer.Expired(Runner))
            return;

        State = GameState.Playing;

        RPC_ShowMessage("");
    }

    void UpdatePlaying()
    {
        if (!OnePlayerLeft())
            return;

        State = GameState.RoundEnding;

        StateTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                endDelay);

        var winner = GetRoundWinner();

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

        var gameWinner =
            GetGameWinner();

        if (gameWinner != null)
        {
            RPC_ShowGameWinner(gameWinner.PlayerName);

            StateTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    5f);

            Runner.LoadScene(SceneRef.FromIndex(1));

            return;
        }

        StartRound();
    }

    // ■ 生存者が1人以下か
    bool OnePlayerLeft()
    {
        int alive = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out var obj))
                continue;

            var vehicle =
                obj.GetComponent<NetworkPlayer>().VehicleObject;

            if (vehicle == null)
                continue;

            var health = vehicle.GetComponent<Tank_Health>();

            if (health == null)
                continue;

            if (health.IsDead)
                continue;

            alive++;
        }

        return alive <= 1;
    }

    // ■ ラウンド勝者取得
    NetworkPlayer GetRoundWinner()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out var obj))
                continue;

            var p = obj.GetComponent<NetworkPlayer>();

            if (p.VehicleObject == null)
                continue;

            var health =
                p.VehicleObject.GetComponent<Tank_Health>();

            if (health == null)
                continue;

            if (!health.IsDead)
                return p;
        }

        return null;
    }

    // ■ ゲーム勝者取得
    NetworkPlayer GetGameWinner()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out var obj))
                continue;

            var p = obj.GetComponent<NetworkPlayer>();

            if (p.Wins >= roundsToWin)
                return p;
        }

        return null;
    }
    
    // ■ 全タンク初期化
    void ResetPlayers()
    {
        int index = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out var obj))
                continue;

            var networkPlayer = obj.GetComponent<NetworkPlayer>();

            var vehicle = networkPlayer.VehicleObject;

            if (vehicle == null)
                continue;

            var spawn = GetSpawn(index);

            vehicle.transform.SetPositionAndRotation(
                spawn.position,
                spawn.rotation);

            var rb = vehicle.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var health = vehicle.GetComponent<Tank_Health>();

            if (health != null)
                health.ResetTank();

            index++;
        }
    }

    Transform GetSpawn(int index)
    {
        if (RoomState.Instance.CurrentMode == GameModes.Tank)
        {
            if (index >= tankSpawnPoints.Length)
                index = 0;

            return tankSpawnPoints[index].transform;
        }

        if (index >= raceSpawnPoints.Length)
            index = 0;

        return raceSpawnPoints[index].transform;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ShowMessage(string message)
    {
        titleText.text = message;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ShowGameWinner(NetworkString<_16> winner)
    {
        titleText.text =
            $"{winner} Wins the Game!";
    }
}