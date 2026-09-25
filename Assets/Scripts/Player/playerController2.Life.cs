using UnityEngine;

public partial class playerController2
{
    private PlayerLife playerLife;
    private bool CanPlay => playerLife != null && playerLife.isAlive;

    public void onPlayerDeath()
    {
        StopAllCoroutines();
        returnCoroutine = null;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        ReturnBanished();

        moveInput = Vector2.zero;
        dashDirection = Vector2.zero;
        wallJumpDirection = Vector2.zero;

        canwalk = false;
        canDash = false;
        canJump = false;
        canWallJump = false;

        isMoving = false;
        isDashing = false;
        isSliding = false;
        isWallJumping = false;

        isTargeting = false;
        armElapsed = 0f;
        banishElapsed = 0f;

        if(reticule != null) reticule.Hide();
        if(countdownIndicator != null) countdownIndicator.Hide();

        isPreviewHeld = false;
        previewRadius = 0f;
        Shader.SetGlobalFloat(PreviewRadiusId, 0f);
        UpdateCameraVisibility(planeMember.CurrentPlane);

        animator.ResetTrigger("jump");
        animator.ResetTrigger("dash");
        animator.SetFloat("yVelocity", 0f);

        //Begin Death Animation/Sound?
    }
}
