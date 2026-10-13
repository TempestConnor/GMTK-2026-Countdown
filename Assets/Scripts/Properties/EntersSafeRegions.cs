using UnityEngine;

/// <summary>Lets this body share safe regions with the player: a region is solid while the body is
/// outside it, and hollow once the body is inside, so it can move through regions joined by open
/// seams. Add it to any dynamic body that should behave this way (Box and SafeBox carry it).
/// Collision is filtered per body by SafeBodyCollision.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
public sealed class EntersSafeRegions : MonoBehaviour
{
    private void OnEnable() => SafeBodyCollision.Register(GetComponent<Rigidbody2D>());
    private void OnDisable() => SafeBodyCollision.Unregister(GetComponent<Rigidbody2D>());
}
