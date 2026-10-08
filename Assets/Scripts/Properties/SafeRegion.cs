using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored hollow, nonlethal geometry. The original collider remains physical support for
/// non-players; players collide only with baked edge sections on a child body, which SafeBoundarySystem
/// splits where regions touch.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class SafeRegion : MonoBehaviour
{
    [Serializable]
    public struct Boundary
    {
        public Vector2 start, end, outward;
        public Boundary(Vector2 a, Vector2 b, Vector2 normal) { start = a; end = b; outward = normal; }
    }

    [SerializeField] private Collider2D support;
    [SerializeField] private Boundary[] boundaries = Array.Empty<Boundary>();
    [SerializeField] private bool movable;
    [SerializeField] private Rigidbody2D boundaryBody;
    [SerializeField] private List<EdgeCollider2D> bakedEdges = new List<EdgeCollider2D>();
    internal readonly List<SafeBoundarySystem.Edge> Edges = new List<SafeBoundarySystem.Edge>();
    internal Matrix4x4 LastMatrix;
    internal int LastLayer;
    public Collider2D Support => support;
    public bool Movable => movable;
    public Boundary[] Boundaries => boundaries;
    public static int PlayerMask => (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("PlayerB"));
    /// <summary>True for this region's player-boundary edge sections.</summary>
    public bool Owns(Collider2D collider) => collider != null && boundaryBody != null && collider.attachedRigidbody == boundaryBody;
    public bool Available => isActiveAndEnabled && support != null && support.enabled &&
        (support.attachedRigidbody == null || support.attachedRigidbody.simulated);

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        SafeBoundarySystem.Register(this);
    }
    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        SafeBoundarySystem.Unregister(this);
        foreach (var edge in bakedEdges) if (edge != null) edge.enabled = false;
    }

    private void FixedUpdate() => SyncBoundaryBody();
    internal void SyncBoundaryBody()
    {
        if (!movable || boundaryBody == null || support == null) return;
        // Edges live on their own kinematic child body, not the dynamic support body, so replacing an
        // edge section never touches the support's contacts or mass. Track the support every step.
        boundaryBody.position = transform.position;
        boundaryBody.rotation = transform.eulerAngles.z;
        var body = support.attachedRigidbody;
        boundaryBody.linearVelocity = body != null ? body.linearVelocity : Vector2.zero;
        boundaryBody.angularVelocity = 0;
        boundaryBody.simulated = Available;
    }

    public bool Contains(Vector2 worldPoint)
    {
        // Movable regions are rectangular Box variants; terrain does not participate in grabbing.
        if (!(support is BoxCollider2D box)) return false;
        Vector2 p = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(p.x - box.offset.x) < box.size.x * .5f &&
               Mathf.Abs(p.y - box.offset.y) < box.size.y * .5f;
    }

    internal EdgeCollider2D GetBakedEdge(int index) => index < bakedEdges.Count ? bakedEdges[index] : CreateEdge();
    internal EdgeCollider2D CreateEdge()
    {
        var child = new GameObject("Safe player boundary");
        child.transform.SetParent(boundaryBody != null ? boundaryBody.transform : transform, false);
        child.layer = gameObject.layer;
        var edge = child.AddComponent<EdgeCollider2D>();
        edge.excludeLayers = ~PlayerMask;
        edge.sharedMaterial = support.sharedMaterial;
        edge.edgeRadius = 0;
        bakedEdges.Add(edge);
        return edge;
    }

#if UNITY_EDITOR
    /// <summary>Editor bake only. Gameplay never reads or rebuilds Tilemaps. <paramref name="carrierParent"/>
    /// places the edge body outside this object's hierarchy: edges beneath a Manual CompositeCollider2D make
    /// Unity regenerate that composite (empty) at runtime whenever a section changes.</summary>
    public bool Bake(Collider2D physicalSupport, Boundary[] data, bool moves, Transform carrierParent = null)
    {
        var parent = carrierParent != null ? carrierParent : transform;
        if (boundaryBody != null && boundaryBody.transform.parent != parent)
        {
            DestroyImmediate(boundaryBody.gameObject);
            boundaryBody = null;
            bakedEdges.Clear();
        }
        bool same = support == physicalSupport && movable == moves && boundaries.Length == data.Length &&
            bakedEdges.Count == data.Length && boundaryBody != null && (physicalSupport.excludeLayers.value & PlayerMask) == PlayerMask &&
            boundaryBody.transform.position == transform.position && boundaryBody.transform.rotation == transform.rotation;
        for (int i = 0; same && i < data.Length; i++)
            same = boundaries[i].start == data[i].start && boundaries[i].end == data[i].end &&
                boundaries[i].outward == data[i].outward && bakedEdges[i] != null;
        if (same) return false;
        foreach (var edge in bakedEdges) if (edge != null) DestroyImmediate(edge.gameObject);
        bakedEdges.Clear();
        support = physicalSupport;
        movable = moves;
        if (boundaryBody == null)
        {
            var carrier = new GameObject(parent == transform ? "Player boundary body" : name + " player edges (generated)");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(carrier, gameObject.scene);
            carrier.transform.SetParent(parent, false);
            if (parent != transform) carrier.hideFlags = HideFlags.HideInHierarchy;
            boundaryBody = carrier.AddComponent<Rigidbody2D>();
            boundaryBody.bodyType = moves ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
            boundaryBody.useFullKinematicContacts = true;
        }
        // Edge points are in this region's local space; the carrier shares its world pose.
        boundaryBody.gameObject.layer = gameObject.layer;
        boundaryBody.transform.SetPositionAndRotation(transform.position, transform.rotation);
        boundaryBody.transform.localScale = Vector3.one;
        var lossy = boundaryBody.transform.lossyScale;
        var target = transform.lossyScale;
        boundaryBody.transform.localScale = new Vector3(target.x / lossy.x, target.y / lossy.y, 1);
        boundaries = data;
        support.excludeLayers |= PlayerMask;
        foreach (var line in boundaries)
        {
            var edge = CreateEdge();
            edge.points = new[] { line.start, line.end };
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(support);
        UnityEditor.EditorUtility.SetDirty(boundaryBody.gameObject);
        return true;
    }
#endif
}
