using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Walk Settings")]
    [Tooltip("Horizontal ground movement speed, in units/second.")]
    public float walkSpeed = 5f;

    [Space]
    [Header("Jump Settings")]
    [Tooltip("Upward velocity applied the instant the player jumps (also used as the vertical kick for wall jumps).")]
    public float jumpImpulse = 11.5f;
    [Tooltip("Multiplier applied to gravity while falling, to make falls feel snappier than the rise.")]
    public float gravityMultiplier = 2f;
    [Tooltip("Fastest downward speed the player can reach while falling (terminal velocity).")]
    public float maxFallSpeed = 7f;

    [Space]
    [Header("Wall Jump/Slide Settings")]
    [Tooltip("Maximum downward speed while sliding down a wall.")]
    public float slideSpeed = 2f;
    [Tooltip("Gravity multiplier applied during a wall jump's rise, separate from the normal jump gravity.")]
    public float wallJumpGravity = 1.5f;
    [Tooltip("How long (in seconds) horizontal movement input is locked out after a wall jump, so the bounce away from the wall isn't immediately cancelled.")]
    public float wallJumpBounceDuration = 0.15f;
    [Tooltip("Horizontal velocity applied away from the wall when performing a wall jump.")]
    public float wallJumpBounceForce = 11f;
    [Tooltip("How long (in seconds) after leaving a wall the player can still perform a wall jump (coyote-time style grace window).")]
    public float wallJumpWindow = 0f;

    [Space]
    [Header("Dash Settings")]
    [Tooltip("Velocity applied in the dash direction when a dash starts.")]
    public float dashSpeed = 14f;
    [Tooltip("How long (in seconds) the dash lasts before ending.")]
    public float dashDuration = 0.25f;
    [Tooltip("Speed cap applied to vertical velocity while dashing, to prevent the dash from launching the player upward.")]
    public float maxDashSpeed = 14f;
    [Tooltip("Multiplier applied to velocity when the dash ends, to control how abruptly momentum is cut.")]
    public float dashEndSpeed = 0.25f;

    [Space]
    [Header("Attack Settings")]
    [Tooltip("Minimum time (in seconds) between attacks.")]
    public float attackCooldown = 0.55f;
}
