using UnityEngine;

/// <summary>
/// マシン用の共通インターフェース.
/// 新しくマシンを作る際はこれを継承しておく.
/// </summary>
public interface IVehicle
{
    // 入力を渡してTransformを取得.
    //void SetInput(PlayerInputData data);
    Transform GetTransform();
}
