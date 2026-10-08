using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>Spatially indexed boundary subtraction; only moved/invalidated edges are rebuilt.</summary>
[DefaultExecutionOrder(-500)]
public sealed class SafeBoundarySystem : MonoBehaviour
{
    // Covers the small separation retained by Box2D's contact solver, not a player-sized gap.
    public const float ContactTolerance = .025f;
    private const float BucketSize = 4;
    private static SafeBoundarySystem instance;
    private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("SafeRegion.UpdateBoundaries");
    private readonly List<SafeRegion> regions = new List<SafeRegion>();
    private readonly Dictionary<Vector2Int, HashSet<Edge>> buckets = new Dictionary<Vector2Int, HashSet<Edge>>();
    private readonly HashSet<Edge> dirty = new HashSet<Edge>();
    private readonly HashSet<Edge> nearby = new HashSet<Edge>();
    private readonly List<Vector2> openings = new List<Vector2>();
    private readonly List<EdgeCollider2D> changed = new List<EdgeCollider2D>();
    private readonly HashSet<SafeRegion> seamRegions = new HashSet<SafeRegion>();
    private readonly List<Vector2> currentPoints = new List<Vector2>(2);
    private readonly Vector2[] sectionPoints = new Vector2[2];
    private static readonly System.Comparison<Vector2> ByStart = (a, b) => a.x.CompareTo(b.x);
    private static int planeAMask = -1, planeBMask;
    public int LastUpdatedEdges { get; private set; }
    public int LastCandidatePairs { get; private set; }

    /// <summary>A span of an edge, in distance from its start, opened by a coincident partner edge.</summary>
    public struct Opening
    {
        public float start, end;
        /// <summary>Signed distance from this edge out to the partner edge (contact gap).</summary>
        public float gap;
        public SafeRegion partner;
    }

    public sealed class Edge
    {
        public SafeRegion owner;
        public SafeRegion.Boundary local;
        public Vector2 a, b, normal;
        public bool indexed;
        public readonly List<Vector2Int> keys = new List<Vector2Int>();
        public readonly List<EdgeCollider2D> colliders = new List<EdgeCollider2D>();
        public readonly List<Opening> openings = new List<Opening>();
    }

    public static void Register(SafeRegion region)
    {
        if (instance == null)
        {
            var go = new GameObject("Safe region boundaries");
            instance = go.AddComponent<SafeBoundarySystem>();
        }
        if (instance.regions.Contains(region)) return;
        instance.regions.Add(region);
        for (int i = region.Edges.Count; i < region.Boundaries.Length; i++)
        {
            var edge = new Edge { owner = region, local = region.Boundaries[i] };
            edge.colliders.Add(region.GetBakedEdge(i));
            region.Edges.Add(edge);
        }
        instance.UpdateRegion(region);
    }

    public static void Unregister(SafeRegion region)
    {
        if (instance == null) return;
        foreach (var edge in region.Edges)
        {
            instance.TouchNeighbours(edge);
            instance.Remove(edge);
            instance.dirty.Remove(edge);
        }
        instance.regions.Remove(region);
        SafeSeams.Release(region);
    }

    private void OnDestroy() { if (instance == this) instance = null; }
    private void FixedUpdate() => RefreshNow();
    public static void RefreshNow()
    {
        if (instance != null) instance.Refresh();
    }

    private void Refresh()
    {
        using (UpdateMarker.Auto())
        {
            LastUpdatedEdges = LastCandidatePairs = 0;
            foreach (var region in regions)
            {
                region.SyncBoundaryBody();
                bool indexed = region.Edges.Count > 0 && region.Edges[0].indexed;
                if (indexed != region.Available || region.LastLayer != region.gameObject.layer ||
                    (region.Movable && region.LastMatrix != region.transform.localToWorldMatrix)) UpdateRegion(region);
            }
            changed.Clear();
            foreach (var edge in dirty)
            {
                if (edge.owner == null) continue;
                Rebuild(edge);
                if (edge.owner.Movable) seamRegions.Add(edge.owner);
            }
            dirty.Clear();
            SafePlayerCollision.ResolveClosures(changed);
            // Seams are visual only; they follow the openings once collision is settled.
            foreach (var region in seamRegions) SafeSeams.Refresh(region);
            seamRegions.Clear();
        }
    }

    private void UpdateRegion(SafeRegion region)
    {
        foreach (var edge in region.Edges)
        {
            TouchNeighbours(edge);
            Remove(edge);
            edge.a = region.transform.TransformPoint(edge.local.start);
            edge.b = region.transform.TransformPoint(edge.local.end);
            edge.normal = region.transform.TransformDirection(edge.local.outward);
            foreach (var col in edge.colliders)
                if (col.gameObject.layer != region.gameObject.layer) col.gameObject.layer = region.gameObject.layer;
            if (region.Available) Insert(edge);
            TouchNeighbours(edge);
            dirty.Add(edge);
        }
        region.LastLayer = region.gameObject.layer;
        region.LastMatrix = region.transform.localToWorldMatrix;
    }

    private void Insert(Edge edge)
    {
        Vector2 min = Vector2.Min(edge.a, edge.b) - Vector2.one * ContactTolerance;
        Vector2 max = Vector2.Max(edge.a, edge.b) + Vector2.one * ContactTolerance;
        for (int y = Mathf.FloorToInt(min.y / BucketSize); y <= Mathf.FloorToInt(max.y / BucketSize); y++)
        for (int x = Mathf.FloorToInt(min.x / BucketSize); x <= Mathf.FloorToInt(max.x / BucketSize); x++)
        {
            var key = new Vector2Int(x, y);
            if (!buckets.TryGetValue(key, out var bucket)) buckets.Add(key, bucket = new HashSet<Edge>());
            bucket.Add(edge);
            edge.keys.Add(key);
        }
        edge.indexed = true;
    }

    private void Remove(Edge edge)
    {
        foreach (var key in edge.keys)
            if (buckets.TryGetValue(key, out var bucket))
            {
                bucket.Remove(edge);
                if (bucket.Count == 0) buckets.Remove(key);
            }
        edge.keys.Clear();
        edge.indexed = false;
    }

    // Only an opposed, coincident edge can open a section, so only those partners need rebuilding.
    private void TouchNeighbours(Edge edge)
    {
        foreach (var key in edge.keys)
            if (buckets.TryGetValue(key, out var bucket))
                foreach (var other in bucket) if (other != edge && CanPair(edge, other)) dirty.Add(other);
    }

    private static bool CanPair(Edge edge, Edge other)
    {
        if (Vector2.Dot(edge.normal, other.normal) > -.99f) return false;
        Vector2 min = Vector2.Min(edge.a, edge.b), max = Vector2.Max(edge.a, edge.b);
        Vector2 otherMin = Vector2.Min(other.a, other.b), otherMax = Vector2.Max(other.a, other.b);
        return otherMin.x <= max.x + ContactTolerance && otherMax.x >= min.x - ContactTolerance &&
               otherMin.y <= max.y + ContactTolerance && otherMax.y >= min.y - ContactTolerance;
    }

    private void Rebuild(Edge edge)
    {
        LastUpdatedEdges++;
        openings.Clear();
        edge.openings.Clear();
        nearby.Clear();
        foreach (var key in edge.keys) if (buckets.TryGetValue(key, out var bucket)) nearby.UnionWith(bucket);
        Vector2 direction = (edge.b - edge.a).normalized;
        float length = Vector2.Distance(edge.a, edge.b);
        foreach (var other in nearby)
        {
            if (other == edge || other.owner == null || !CanPair(edge, other) ||
                other.owner.gameObject.scene != edge.owner.gameObject.scene ||
                !SamePlane(edge.owner.gameObject.layer, other.owner.gameObject.layer)) continue;
            LastCandidatePairs++;
            if (Mathf.Abs(Vector2.Dot(other.a - edge.a, edge.normal)) > ContactTolerance ||
                Mathf.Abs(Vector2.Dot(other.b - edge.a, edge.normal)) > ContactTolerance) continue;
            float a = Vector2.Dot(other.a - edge.a, direction), b = Vector2.Dot(other.b - edge.a, direction);
            float start = Mathf.Max(0, Mathf.Min(a, b)), end = Mathf.Min(length, Mathf.Max(a, b));
            if (end - start > .001f)
            {
                openings.Add(new Vector2(start, end));
                edge.openings.Add(new Opening { start = start, end = end, gap = Vector2.Dot(other.a - edge.a, edge.normal), partner = other.owner });
            }
        }
        openings.Sort(ByStart);
        int used = 0;
        float cursor = 0;
        if (edge.indexed)
        {
            // Resting bodies sit up to ContactTolerance off the grid, so a touching partner can leave a
            // sliver at an opening's end. Drop those: a sub-tolerance lip would snag the player's feet.
            foreach (var opening in openings)
            {
                if (opening.x > cursor + ContactTolerance) SetSection(edge, used++, cursor / length, opening.x / length);
                cursor = Mathf.Max(cursor, opening.y);
            }
            if (cursor < length - ContactTolerance) SetSection(edge, used++, cursor / length, 1);
        }
        // Disabling only opens passages; it can never close through a player.
        for (int i = used; i < edge.colliders.Count; i++)
            if (edge.colliders[i].enabled) edge.colliders[i].enabled = false;
    }

    private void SetSection(Edge edge, int index, float start, float end)
    {
        if (index == edge.colliders.Count) edge.colliders.Add(edge.owner.CreateEdge());
        var collider = edge.colliders[index];
        Vector2 a = Vector2.Lerp(edge.local.start, edge.local.end, start);
        Vector2 b = Vector2.Lerp(edge.local.start, edge.local.end, end);
        // SetPoints only for changed geometry. Moving an intact edge merely moves its body.
        bool modified = false;
        collider.GetPoints(currentPoints);
        if (currentPoints.Count != 2 || currentPoints[0] != a || currentPoints[1] != b)
        {
            sectionPoints[0] = a; sectionPoints[1] = b;
            collider.points = sectionPoints;
            modified = true;
        }
        if (collider.gameObject.layer != edge.owner.gameObject.layer) { collider.gameObject.layer = edge.owner.gameObject.layer; modified = true; }
        if (!collider.enabled) { collider.enabled = true; modified = true; }
        if (collider.isTrigger) collider.isTrigger = false;
        // Only sections that grew, appeared, or changed plane can close through a player.
        if (modified) changed.Add(collider);
    }

    public static bool SamePlane(int a, int b)
    {
        if (planeAMask == -1)
        {
            planeAMask = (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("Entities"));
            planeBMask = (1 << LayerMask.NameToLayer("GroundB")) | (1 << LayerMask.NameToLayer("EntityB"));
        }
        return ((planeAMask & (1 << a)) != 0 && (planeAMask & (1 << b)) != 0) ||
               ((planeBMask & (1 << a)) != 0 && (planeBMask & (1 << b)) != 0);
    }
}
