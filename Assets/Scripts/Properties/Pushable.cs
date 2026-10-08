using UnityEngine;

/// <summary>Opt a dynamic 2D body into the player's grab interaction.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
public sealed class Pushable : MonoBehaviour
{
    private Rigidbody2D body;
    private playerController2 holder;
    public Rigidbody2D Body => body != null ? body : (body = GetComponent<Rigidbody2D>());
    public bool CanGrab => isActiveAndEnabled && Body.simulated &&
        Body.bodyType == RigidbodyType2D.Dynamic && holder == null;

    public bool TryGrab(playerController2 player)
    {
        if (player == null || !CanGrab) return false;
        var safe = GetComponent<SafeRegion>();
        if (safe != null && safe.Contains(player.GetComponent<Collider2D>().bounds.center)) return false;
        holder = player;
        Body.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        return true;
    }

    public void Release(playerController2 player)
    {
        if (holder != player) return;
        holder = null;
        LockHorizontalMovement();
    }

    private void OnEnable() => LockHorizontalMovement();

    private void LockHorizontalMovement()
    {
        Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
        Body.constraints |= RigidbodyConstraints2D.FreezePositionX;
    }

    private void OnDisable()
    {
        if (holder != null) holder.ReleaseGrab();
        holder = null;
        LockHorizontalMovement();
    }
}
