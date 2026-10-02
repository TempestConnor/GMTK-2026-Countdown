using UnityEngine;

public partial class playerController2
{
    private PlayerLife playerLife;
    private PlayerInputLock inputLock;
    private PlayerActionCancellation actions;
    private BanishAction banishAction;
    private bool CanPlay => playerLife != null && playerLife.isAlive && !inputLock.IsLocked;

    private void InitializeActions()
    {
        inputLock = GetComponent<PlayerInputLock>();
        actions = GetComponent<PlayerActionCancellation>();
        actions.Register(new MovementAction(this));
        actions.Register(new DashAction(this));
        actions.Register(new JumpAction(this));
        actions.Register(new AimAction(this));
        actions.Register(new PreviewAction(this));
        banishAction = new BanishAction(this);
        actions.Register(banishAction);
        inputLock.Locked += actions.ClearInputState;
    }

    private void OnDestroy()
    {
        if (inputLock != null && actions != null) inputLock.Locked -= actions.ClearInputState;
    }

    public void CancelActionsForTransition() => actions.CancelAll(banishAction);

    public void onPlayerDeath()
    {
        inputLock.Acquire(); // Held for this player's remaining lifetime.
        actions.CancelAll();
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        //Begin Death Animation/Sound?
    }
}
