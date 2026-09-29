using UnityEngine;
using UnityEngine.Events;

/// <summary>Shared instant-death receiver. Configure OnKilled for each object's death behavior.</summary>
[DisallowMultipleComponent]
public class Damageable : MonoBehaviour
{
    [SerializeField] private UnityEvent onKilled = new UnityEvent();
    public UnityEvent OnKilled => onKilled;
    public bool IsDead { get; private set; }

    public void Kill()
    {
        if (!isActiveAndEnabled || IsDead) return;
        // Set first: listeners may trigger another hazard or a plane transition.
        IsDead = true;
        onKilled.Invoke();
    }

    public void ResetLife() => IsDead = false;

    public void DestroyObject() => Destroy(gameObject);

    public static Damageable FromCollider(Collider2D collider)
    {
        if (collider == null || collider.isTrigger) return null;
        var body = collider.attachedRigidbody;
        return body != null ? body.GetComponentInParent<Damageable>() : collider.GetComponentInParent<Damageable>();
    }
}
