using UnityEngine;

namespace Tanks.Complete
{
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance;

        public GameModes CurrentMode;

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
