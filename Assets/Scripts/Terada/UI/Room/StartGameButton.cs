using Fusion;
using UnityEngine;

public class StartGameButton : MonoBehaviour
{
    NetworkRunner runner;

    void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();
    }

    public void StartGame()
    {
        if (!runner.IsServer) return;

        runner.LoadScene(("GameScene_T"));
    }
}
