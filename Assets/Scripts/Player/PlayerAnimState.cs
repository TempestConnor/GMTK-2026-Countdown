/// <summary>
/// One value per player animation clip. playerController2 picks the state each frame and writes it
/// to the Animator's "state" parameter; PlayerSpriteGenerator builds one clip and one Any State
/// transition per value. Append new values at the end so existing numbers stay stable.
/// </summary>
public enum PlayerAnimState
{
    Idle,
    Walk,
    Rise,
    Fall,
    WallSlide,
    WallJump,
    GrabIdle,
    GrabPush,
    GrabPull,
    GrabAir,
    AimIdle,
    AimWalk,
    AimAir,
    AimSlide,
    SnapIdle,
    SnapWalk,
    SnapAir,
    SnapSlide,
}
