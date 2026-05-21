using UnityEngine;
using PlayerInputActions;

/// <summary>
/// ƒvƒŒƒCƒ„[“ü—Íˆ—.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    PlayerInput_Actions actions;

    public Vector2 Move { get; private set; }
    public bool Jump { get; private set; }
    public bool Interact { get; private set; }
    public bool InteractPressed { get; private set; }

    void Awake()
    {
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
    }

    void OnEnable()
    {
        Debug.Log("Enable called");
        actions.Enable();
    }

    void OnDisable()
    {
        actions.Disable();
    }

    //private void LateUpdate()
    //{
    //    InteractPressed = false;
    //}

    public bool ConsumeInteractPressed()
    {
        if ((!InteractPressed))
            return false;

        InteractPressed = false;
        return true;
    }
}