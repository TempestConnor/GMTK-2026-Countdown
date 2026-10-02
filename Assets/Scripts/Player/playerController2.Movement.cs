using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    private sealed class MovementAction : IPlayerAction, IPlayerInputState
    {
        private readonly playerController2 player;
        public MovementAction(playerController2 player) { this.player = player; }
        public void ClearInput()
        {
            player.moveInput = Vector2.zero;
            player.isMoving = false;
        }
        public void Cancel()
        {
            ClearInput();
            player.canwalk = true;
            player.rb.linearVelocity = new Vector2(0, player.rb.linearVelocity.y);
        }
    }

    public void onMove(InputAction.CallbackContext context)
    {
        if (!CanPlay) return;
        
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
