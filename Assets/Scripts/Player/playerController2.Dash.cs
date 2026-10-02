using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    private Coroutine dashCoroutine;
    private sealed class DashAction : IPlayerAction
    {
        private readonly playerController2 player;
        public DashAction(playerController2 player) { this.player = player; }
        public void Cancel()
        {
            if (player.dashCoroutine != null) player.StopCoroutine(player.dashCoroutine);
            player.dashCoroutine = null;
            if (player.isDashing) player.rb.linearVelocity = Vector2.zero;
            player.isDashing = false;
            player.dashDirection = Vector2.zero;
            player.canDash = player.canJump = player.canwalk = true;
            player.setGravityScale(player.originalGravity);
            player.animator.ResetTrigger("dash");
        }
    }
    public void onDash(InputAction.CallbackContext context)
    {
        if(!CanPlay) return;

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
        rb.linearVelocity = dashDirection.normalized * stats.dashSpeed;

        dashCoroutine = StartCoroutine(stopDashing());

    }


    private IEnumerator stopDashing()
    {
        yield return new WaitForSeconds(stats.dashDuration);
        dashCoroutine = null;
        isDashing = false;
        setGravityScale(originalGravity);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y) * stats.dashEndSpeed;
        Debug.Log("stopdashing triggered");
        canJump = true;
        canwalk = true;

    }
}
