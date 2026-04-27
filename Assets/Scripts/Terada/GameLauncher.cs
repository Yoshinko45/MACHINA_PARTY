using Fusion;
using UnityEngine;

public class GameLauncher : MonoBehaviour
{
    [SerializeField]
    private NetworkRunner networkRunnnerPrefab;

    private async void Start()
    {
        // NetworkRunner生成.
        var networkRunner = Instantiate(networkRunnnerPrefab);
        // 共有モードのセッションに参加.
        var result = await networkRunner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared
        });

        // 結果を出力.
        Debug.Log(result);
    }
}
