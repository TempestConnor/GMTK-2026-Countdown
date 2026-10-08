using UnityEngine;

/// <summary>
/// Shared trigger volume for tutorial prompt zones. The root is the bottom-left corner of
/// <see cref="areaSize"/> (in cells), like RoomTransition. Fires once per room load when the
/// living player's body enters on the chosen <see cref="plane"/> (either, by default).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public abstract class TutorialZone : MonoBehaviour
{
    public enum PlaneFilter { Either, A, B }

    public Vector2 areaSize = new Vector2(2, 4);
    [Tooltip("Plane the player must be on. Either = A or B. A plane swap inside the area also counts as entering.")]
    public PlaneFilter plane = PlaneFilter.Either;

    protected bool Fired { get; set; }

    protected abstract void OnPlayerEntered();
    protected abstract Color GizmoColor { get; }
    /// <summary>False when the area is ignored (the collider is then disabled).</summary>
    protected virtual bool UsesArea => true;

    private void Reset() => ConfigureArea();
    private void OnValidate() => ConfigureArea();

    public void ConfigureArea()
    {
        areaSize = Vector2.Max(Vector2.one * 0.25f, areaSize);
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        // Like RoomTransition: always contact the player on either plane.
        box.includeLayers = LayerMask.GetMask("Player", "PlayerB");
        box.layerOverridePriority = 10;
        box.size = areaSize;
        box.offset = areaSize / 2;
        box.enabled = UsesArea;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryFire(other);

    // Only plane-filtered zones need this: the player can change plane while already inside.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (plane != PlaneFilter.Either) TryFire(other);
    }

    private void TryFire(Collider2D other)
    {
        if (Fired || !isActiveAndEnabled || other.isTrigger || other.attachedRigidbody == null) return;
        var player = other.attachedRigidbody.GetComponent<PlayerLife>();
        if (player == null || !player.isAlive || !OnRequiredPlane(player)) return;
        Fired = true;
        OnPlayerEntered();
    }

    private bool OnRequiredPlane(PlayerLife player)
    {
        if (plane == PlaneFilter.Either) return true;
        var member = player.GetComponent<Banishable>();
        var current = member != null ? member.CurrentPlane : Banishable.Plane.A;
        return current == (plane == PlaneFilter.A ? Banishable.Plane.A : Banishable.Plane.B);
    }

    protected virtual void OnDrawGizmos()
    {
        if (!UsesArea) return;
        var color = GizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.15f);
        Gizmos.DrawCube(areaSize / 2, areaSize);
        Gizmos.color = color;
        Gizmos.DrawWireCube(areaSize / 2, areaSize);
        Gizmos.matrix = Matrix4x4.identity;
    }

    /// <summary>Gizmo label prefix naming the plane filter, e.g. "[B] ".</summary>
    protected string PlaneTag => plane == PlaneFilter.Either ? string.Empty : $"[{plane}] ";

    public Vector3 AreaCenter => transform.TransformPoint(areaSize / 2);
}
