using Fusion;
using UnityEngine;

// 入力構造体.
public struct PlayerInputData : INetworkInput
{
    public float Throttle;
    public float Steering;
    public NetworkButtons Buttons;
}

// 入力のボタンの種類は、列挙型(enum)で定義しておく.
public enum NetworkInputButtons
{
    Jump,
    Boost,
    Drift,
    Item
}