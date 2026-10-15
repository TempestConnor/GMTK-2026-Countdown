using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    private Coroutine wallJumpCoroutine;
    private int wallJumpGraceFrames;
    private sealed class JumpAction : IPlayerAction
    {
        private readonly playerController2 player;
        public JumpAction(playerController2 player) { this.player = player; }
        public void Cancel()
        {
            if (player.wallJumpCoroutine != null) player.StopCoroutine(player.wallJumpCoroutine);
            player.wallJumpCoroutine = null;
            player.wallJumpGraceFrames = 0;
            player.wallJumpDirection = Vector2.zero;
            player.isWallJumping = player.isSliding = player.canWallJump = player.wallJumpPose = false;
            player.canJump = player.canwalk = true;
            player.rb.linearVelocity = new Vector2(player.rb.linearVelocity.x, 0);
            player.setGravityScale(player.originalGravity);
            player.animator.ResetTrigger("jump");
            player.animator.SetFloat("yVelocity", 0);
        }
    }
    public void onJump(InputAction.CallbackContext context)
    {
        if (!CanPlay) return;
        
        touchingDirection.RefreshContacts();
        if (context.started && canJump && touchingDirection.isGrounded)
        {
            animator.SetTrigger("jump");
            setGravityScale(originalGravity);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpImpulse * GravityUpSign);
            JumpGrabbedBody();
            Debug.Log("has jumped");
        }


        // Wall Jump Logic
        else if (context.started && canJump && canWallJump)
        {
            if (wallJumpCoroutine != null) StopCoroutine(wallJumpCoroutine);
            wallJumpCoroutine = StartCoroutine(performWallJump());

            // A grace-window jump uses up the window so it can't be repeated mid-air.
            if (!isSliding)
            {
                wallJumpGraceFrames = 0;
                canWallJump = false;
            }
        }


        // Holding Jump makes MC jump higher
        if (context.canceled && !isDashing)
        {
            setGravityScale(originalGravity * stats.gravityMultiplier);
            isWallJumping = false;

        }


    }

    private IEnumerator performWallJump()
    {
        setGravityScale(originalGravity * stats.wallJumpGravity);

        isWallJumping = true;
        wallJumpPose = true;
        wallJumpFacesRight = wallJumpDirection.x > 0;

        canwalk = false;


        rb.linearVelocity = new Vector2(wallJumpDirection.x * stats.wallJumpBounceForce, stats.jumpImpulse * GravityUpSign);
        JumpGrabbedBody();

        yield return new WaitForSeconds(stats.wallJumpBounceDuration);
        wallJumpCoroutine = null;
        wallJumpPose = false;
        canwalk = true;
        setGravityScale(originalGravity);
        Debug.Log("wall jumped");
    }

    private void performWallSlide()
    {
        isSliding = true;
        if (!isWallJumping)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, GravityUpSign * Mathf.Max(rb.linearVelocity.y * GravityUpSign, -stats.slideSpeed));

        }

    }
}
