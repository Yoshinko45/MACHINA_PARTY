
using UnityEngine;

public class CarController : MonoBehaviour
{
    public enum CarMode
    {
        Distance,
        ChickenRace,
        CarBowling,
        Destruction
    }

    [Header("Mode Settings")]
    [SerializeField] private CarMode currentMode = CarMode.Distance;

    // CarMovementから現在のモードを参照する
    public CarMode CurrentMode => currentMode;

    // 外部からモードを変更する場合に使用
    public void SetMode(CarMode mode)
    {
        currentMode = mode;
    }
}
