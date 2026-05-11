using UnityEngine;
using UnityEngine.InputSystem;

public class test : MonoBehaviour
{
    public float speed = 5f;

    private Vector2 moveInput;

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);
        transform.position += move * speed * Time.deltaTime;
    }
}