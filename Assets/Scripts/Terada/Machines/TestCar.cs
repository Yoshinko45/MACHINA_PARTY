using Fusion;
using UnityEngine;

public class TestCar : NetworkBehaviour, IVehicle
{
    public float speed = 10f;

    public void SetInput(PlayerInputData data)
    {
        Vector3 move = transform.forward * data.Throttle;

        transform.position += move * speed * Runner.DeltaTime;

        transform.Rotate(0, data.Steering * 120f * Runner.DeltaTime, 0);
    }

    public Transform GetTransform()
    {
        return transform;
    }
}
