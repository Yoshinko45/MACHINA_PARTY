using Fusion;
using UnityEngine;

public class PlayerInputSource : NetworkBehaviour, IInputSource
{
    public InputState GetInput()
    {
        if (GetInput(out PlayerInputData data))
        {
            return new InputState
            {
                Tick = Runner.Tick,
                Throttle = data.Throttle,
                Steering = data.Steering
            };
        }

        return default;
    }
}
