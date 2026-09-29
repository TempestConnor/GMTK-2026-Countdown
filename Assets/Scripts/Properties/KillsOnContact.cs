using UnityEngine;

/// <summary>Kills damageable bodies touching this object's collider.</summary>
[DisallowMultipleComponent]
public sealed class KillsOnContact : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other) => KillOther(other);
    private void OnTriggerStay2D(Collider2D other) => KillOther(other);
    private void OnCollisionEnter2D(Collision2D collision) => KillOther(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => KillOther(collision.collider);

    private void KillOther(Collider2D other)
    {
        // Unity can deliver collision messages to disabled MonoBehaviours.
        if (!isActiveAndEnabled) return;
        var target = Damageable.FromCollider(other);
        if (target != null && target != GetComponentInParent<Damageable>()) target.Kill();
    }
}
