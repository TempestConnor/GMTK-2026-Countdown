using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    public void onJump(InputAction.CallbackContext context)
    {
        // Need to check if alive
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
            StartCoroutine(performWallJump());
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
        canWallJump = false;
        //Debug.Log("Window has expired");
    }
}
