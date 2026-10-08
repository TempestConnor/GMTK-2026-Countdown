using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    [Header("Grab")]
    [SerializeField, Min(0.01f)] private float grabReach = 0.35f;
    private Pushable grabbed;
    private Collider2D grabbedCollider;
    private FixedJoint2D grabJoint;
    private readonly List<RaycastHit2D> grabHits = new List<RaycastHit2D>();
    public Rigidbody2D GrabbedBody => grabbed != null ? grabbed.Body : null;

    private sealed class GrabAction : IPlayerAction, IPlayerInputState
    {
        private readonly playerController2 player;
        public GrabAction(playerController2 player) { this.player = player; }
        public void Cancel() => player.ReleaseGrab();
        public void ClearInput() => Cancel();
    }

    public void onInteract(InputAction.CallbackContext context)
    {
        if (!context.performed || !CanPlay || !isActiveAndEnabled) return;
        if (grabbed != null) { ReleaseGrab(); return; }
        if (isDashing) return;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        filter.useTriggers = false;
        var playerCollider = GetComponent<CapsuleCollider2D>();
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        playerCollider.Cast(direction, filter, grabHits, grabReach);
        // The nearest solid blocks interaction, so boxes cannot be grabbed through walls.
        RaycastHit2D nearest = default;
        float distance = float.PositiveInfinity;
        foreach (var hit in grabHits)
        {
            if (hit.rigidbody == rb || hit.distance >= distance || Vector2.Dot(hit.normal, direction) > -0.5f ||
                !SafePlayerCollision.Blocks(playerCollider, hit.collider)) continue;
            nearest = hit;
            distance = hit.distance;
        }
        var target = nearest.collider != null ? nearest.collider.GetComponentInParent<Pushable>() : null;
        if (target == null || !target.TryGrab(this)) return;

        grabbed = target;
        // A SafeRegion's player edges are rebuilt as connections change; track its stable support instead.
        var safe = target.GetComponent<SafeRegion>();
        grabbedCollider = safe != null && safe.Support != null ? safe.Support : nearest.collider;
        grabJoint = gameObject.AddComponent<FixedJoint2D>();
        grabJoint.connectedBody = target.Body;
        grabJoint.autoConfigureConnectedAnchor = true;
        grabJoint.enableCollision = true;
        grabJoint.frequency = 0f; // Rigid grip; keep the initial offset without teleporting.
        target.Body.WakeUp();
        if (playerAudio != null) playerAudio.PlayGrab();
    }

    private void UpdateGrab()
    {
        if (grabbed == null) { ReleaseGrab(); return; }
        if (!CanPlay || isDashing || grabJoint == null || !grabbed.isActiveAndEnabled ||
            !grabbed.Body.simulated || grabbed.Body.bodyType != RigidbodyType2D.Dynamic ||
            grabbedCollider == null || !grabbedCollider.enabled || grabbedCollider.isTrigger ||
            Physics2D.GetIgnoreLayerCollision(gameObject.layer, grabbedCollider.gameObject.layer))
        {
            ReleaseGrab();
            return;
        }
        // Drive both bodies together; the joint and solid colliders still resolve obstacles.
        grabbed.Body.linearVelocity = rb.linearVelocity;
    }

    private void JumpGrabbedBody()
    {
        if (grabbed != null) grabbed.Body.linearVelocity = rb.linearVelocity;
    }

    public void ReleaseGrab()
    {
        if (grabJoint != null)
        {
            grabJoint.enabled = false; // Destroy is deferred until the end of the frame.
            Destroy(grabJoint);
        }
        grabJoint = null;
        if (grabbed != null) grabbed.Release(this);
        grabbed = null;
        grabbedCollider = null;
    }

    private void OnDisable()
    {
        SafePlayerCollision.Unregister(this);
        ReleaseGrab();
    }
}
