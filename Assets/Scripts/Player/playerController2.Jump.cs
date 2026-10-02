using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    private Coroutine wallJumpCoroutine;
    private Coroutine wallWindowCoroutine;
    private sealed class JumpAction : IPlayerAction
    {
        private readonly playerController2 player;
        public JumpAction(playerController2 player) { this.player = player; }
        public void Cancel()
        {
            if (player.wallJumpCoroutine != null) player.StopCoroutine(player.wallJumpCoroutine);
            if (player.wallWindowCoroutine != null) player.StopCoroutine(player.wallWindowCoroutine);
            player.wallJumpCoroutine = player.wallWindowCoroutine = null;
            player.wallJumpDirection = Vector2.zero;
            player.isWallJumping = player.isSliding = player.canWallJump = false;
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
            Debug.Log("has jumped");
        }


        // Wall Jump Logic
        else if (context.started && canJump && canWallJump)
        {
            if (wallJumpCoroutine != null) StopCoroutine(wallJumpCoroutine);
            wallJumpCoroutine = StartCoroutine(performWallJump());
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

        canwalk = false;


        rb.linearVelocity = new Vector2(wallJumpDirection.x * stats.wallJumpBounceForce, stats.jumpImpulse * GravityUpSign);

        yield return new WaitForSeconds(stats.wallJumpBounceDuration);
        wallJumpCoroutine = null;
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

    // Wait a certain time before disabling wall jumps
    private IEnumerator wallJumpWait()
    {
        yield return new WaitForSeconds(stats.wallJumpWindow);
        wallWindowCoroutine = null;
        canWallJump = false;
        //Debug.Log("Window has expired");
    }
}
