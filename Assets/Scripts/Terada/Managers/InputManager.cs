using UnityEngine;
using PlayerInputActions;

/// <summary>
/// プレイヤー入力処理.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    PlayerInput_Actions actions;

    // 車両移動用.
    public Vector2 Move { get; private set; }
    public bool Fire { get; private set; }
    public bool Jump { get; private set; }

    // 車両選択オブジェクト用.
    public bool Interact { get; private set; }
    public bool InteractPressed { get; private set; }

    // UIページ切り替え用.
    public bool PageLeftPressed { get; private set; }
    public bool PageRightPressed { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        actions = new PlayerInput_Actions();

        actions.Player.Move.performed += ctx => Move = ctx.ReadValue<Vector2>();
        actions.Player.Move.canceled += ctx => Move = Vector2.zero;

        actions.Player.Jump.performed += ctx => Jump = true;
        actions.Player.Jump.canceled += ctx => Jump = false;

        actions.Player.Interact.performed += ctx => Interact = true;
        actions.Player.Interact.canceled += ctx => Interact = false;

        actions.Player.Interact.performed += ctx => InteractPressed = true;

        actions.Player.Fire.performed += ctx => Fire = true;
        actions.Player.Fire.canceled += ctx => Fire = false;

        actions.Player.PageLeft.performed += _ => PageLeftPressed = true;

        actions.Player.PageRight.performed += _ => PageRightPressed = true;
    }

    void OnEnable()
    {
        actions.Enable();
    }

    void OnDisable()
    {
        actions.Disable();
    }

    public bool ConsumeInteractPressed()
    {
        if ((!InteractPressed))
            return false;

        InteractPressed = false;
        return true;
    }

    public bool ConsumePageLeftPressed()
    {
        if (!PageLeftPressed)
            return false;

        PageLeftPressed = false;
        return true;
    }

    public bool ConsumePageRightPressed()
    {
        if (!PageRightPressed)
            return false;

        PageRightPressed = false;
        return true;
    }
}