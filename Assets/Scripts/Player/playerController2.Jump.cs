using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    public void onJump(InputAction.CallbackContext context)
    {
        // Need to check if alive
        if (context.started && canJump && (touchingDirection.isGrounded) ^ context.performed)
        {
            animator.SetTrigger("jump");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpImpulse);
            Debug.Log("has jumped");
        }


        // Wall Jump Logic
        if (context.started && canJump && canWallJump ^ context.performed)
        {
            StartCoroutine(performWallJump());
        }


        // Holding Jump makes MC jump higher
        if (context.canceled)
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


        rb.linearVelocity = new Vector2(wallJumpDirection.x * stats.wallJumpBounceForce, stats.jumpImpulse);

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
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -stats.slideSpeed, float.MaxValue));

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
