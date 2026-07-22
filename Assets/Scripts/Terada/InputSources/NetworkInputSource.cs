using Fusion;
using UnityEngine;

public class NetworkInputSource : NetworkBehaviour, IInputSource
{
    public InputState GetInput()
    {
        bool success = GetInput(out PlayerInputData data);

        if (!success)
            return default;

        return new InputState 
        { 
            Tick = Runner.Tick,
            Throttle = data.Throttle,
            Steering = data.Steering
        };

    }
}
