using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    public void onMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();

        isMoving = moveInput != Vector2.zero;

        setFacingDirection(moveInput);
    }

    private void setFacingDirection(Vector2 moveInput)
    {
        if (moveInput.x > 0 && !isFacingRight)
        {
            isFacingRight = true;
        }
        else if (moveInput.x < 0 && isFacingRight)
        {
            isFacingRight = false;
        }
    }
}
