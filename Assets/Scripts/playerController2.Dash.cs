using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    public void onDash(InputAction.CallbackContext context)
    {
        if (context.started && canDash)
        {

            setGravityScale(0);
            animator.SetTrigger("dash");

            perFormDash();

            isDashing = true;
            canDash = false;



        }
    }

    private void perFormDash()
    {
        // Set gravity to 0
        setGravityScale(0);

        // Disable Jumping and walking while dashing
        canwalk = false;
        canJump = false;

        // Determine dash direction and performs the dash
        rb.linearVelocity = dashDirection.normalized * dashSpeed;

        StartCoroutine(stopDashing());

    }


    private IEnumerator stopDashing()
    {
        yield return new WaitForSeconds(dashDuration);
        isDashing = false;
        setGravityScale(originalGravity);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y) * dashEndSpeed;
        Debug.Log("stopdashing triggered");
        canJump = true;
        canwalk = true;

    }
}
