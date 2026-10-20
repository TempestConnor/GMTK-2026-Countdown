using System.Collections.Generic;
using UnityEngine;

/// <summary>Inside/outside collision for bodies marked EntersSafeRegions, mirroring the player's. Outside a
/// safe region a body hits its solid support and ignores its boundary edges, so stacks rest normally.
/// Inside (it fits the region and its center is within it, or it still overlaps a region it was inside)
/// it ignores the support of that region and of every region joined to it by open seams, and collides
/// with their edges instead, so it moves through the merged hollow space. Runs from SafeBoundarySystem
/// after edge sections are rebuilt, before the physics step. The player is registered too, so a SafeBox
/// rests on an outside player like an ordinary box; fixed regions keep the player's own rule (Apply).</summary>
public static class SafeBodyCollision
{
    private sealed class Occupant
    {
        public Rigidbody2D body;
        public SafeRegion self; // The body's own region when it is a SafeBox.
        public readonly List<Collider2D> colliders = new List<Collider2D>();
        // Regions the body is in, after spreading through open seams; the previous step's set too.
        public HashSet<SafeRegion> inside = new HashSet<SafeRegion>(), previous = new HashSet<SafeRegion>();
        // Regions that directly contain the body (before spreading), and the previous step's.
        public HashSet<SafeRegion> containers = new HashSet<SafeRegion>(), previousContainers = new HashSet<SafeRegion>();
        // Last applied ignore state per obstacle collider; missing means unknown, so it is always applied.
        public readonly Dictionary<Collider2D, bool> ignored = new Dictionary<Collider2D, bool>();
    }

    // Covers a body's travel before its next pass, so nearby edges are filtered before it reaches them.
    private const float QueryMargin = .5f;
    // Speed difference that marks one of two mutually containing SafeBoxes as the one moving in.
    private const float MovingIn = .05f;
    private static readonly List<Occupant> occupants = new List<Occupant>();
    private static readonly Dictionary<SafeRegion, Occupant> byRegion = new Dictionary<SafeRegion, Occupant>();
    private static readonly Queue<SafeRegion> frontier = new Queue<SafeRegion>();
    private static readonly HashSet<SafeBoundarySystem.Edge> nearby = new HashSet<SafeBoundarySystem.Edge>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        occupants.Clear();
        byRegion.Clear();
    }

    public static void Register(Rigidbody2D body)
    {
        if (body == null || occupants.Exists(o => o.body == body)) return;
        var occupant = new Occupant { body = body, self = body.GetComponent<SafeRegion>() };
        var attached = new List<Collider2D>();
        body.GetAttachedColliders(attached);
        foreach (var col in attached) if (!col.isTrigger) occupant.colliders.Add(col);
        occupants.Add(occupant);
        if (occupant.self != null) byRegion[occupant.self] = occupant;
    }

    // Applied ignore pairs are left in place: they persist while colliders are disabled, and a body
    // registering again starts with unknown state, so every pair is reapplied on its first pass.
    public static void Unregister(Rigidbody2D body)
    {
        int index = occupants.FindIndex(o => o.body == body);
        if (index < 0) return;
        var occupant = occupants[index];
        occupants.RemoveAt(index);
        if (occupant.self != null && byRegion.TryGetValue(occupant.self, out var current) && current == occupant)
            byRegion.Remove(occupant.self);
    }

    internal static void Resolve(List<SafeRegion> regions)
    {
        if (occupants.Count == 0) return;
        // Every body's inside set first: support-to-support pairs depend on both bodies.
        foreach (var occupant in occupants) FindContainers(occupant, regions);
        ResolveMutualContainment();
        foreach (var occupant in occupants) SpreadThroughSeams(occupant);
        foreach (var occupant in occupants) Apply(occupant, regions);
    }

    private static bool Active(Occupant occupant) => occupant.body != null && occupant.body.simulated &&
        occupant.body.gameObject.activeInHierarchy && occupant.colliders.Count > 0;

    private static Bounds BodyBounds(Occupant occupant)
    {
        var bounds = occupant.colliders[0].bounds;
        for (int i = 1; i < occupant.colliders.Count; i++) bounds.Encapsulate(occupant.colliders[i].bounds);
        return bounds;
    }

    private static void FindContainers(Occupant occupant, List<SafeRegion> regions)
    {
        (occupant.previous, occupant.inside) = (occupant.inside, occupant.previous);
        (occupant.previousContainers, occupant.containers) = (occupant.containers, occupant.previousContainers);
        occupant.inside.Clear();
        occupant.containers.Clear();
        if (!Active(occupant)) return;
        var bounds = BodyBounds(occupant);
        int layer = occupant.body.gameObject.layer;
        foreach (var region in regions)
        {
            if (region == occupant.self || !region.Available || region.gameObject.scene != occupant.body.gameObject.scene ||
                !SafeBoundarySystem.SamePlane(layer, region.gameObject.layer)) continue;
            // Hysteresis: a body stays inside while it still overlaps, so it cannot flip to "outside"
            // halfway through a wall and be shoved out by the support it was passing.
            if (region.Fits(bounds.size, SafeBoundarySystem.ContactTolerance) && (region.Contains(bounds.center) ||
                (occupant.previous.Contains(region) && region.Overlaps(bounds, SafeBoundarySystem.ContactTolerance))))
                occupant.containers.Add(region);
        }
    }

    // Equal-size SafeBoxes can each hold the other's center. Containment must run one way only: a box
    // counted inside its own occupant would treat that occupant's whole merged space as hollow, dropping
    // its contact with whatever it supports, and a stack resting on it would fall through.
    private static void ResolveMutualContainment()
    {
        for (int i = 0; i < occupants.Count; i++)
        {
            var a = occupants[i];
            if (a.self == null || a.containers.Count == 0) continue;
            for (int j = i + 1; j < occupants.Count; j++)
            {
                var b = occupants[j];
                if (b.self == null || !a.containers.Contains(b.self) || !b.containers.Contains(a.self)) continue;
                if (MovesInto(a, b)) b.containers.Remove(a.self);
                else a.containers.Remove(b.self);
            }
        }
    }

    /// <summary>Which of two mutually containing boxes is the occupant: the one moving in, else the one
    /// already inside, else a stable arbitrary choice.</summary>
    private static bool MovesInto(Occupant a, Occupant b)
    {
        float speedA = a.body.linearVelocity.magnitude, speedB = b.body.linearVelocity.magnitude;
        if (Mathf.Abs(speedA - speedB) > MovingIn) return speedA > speedB;
        bool wasA = a.previousContainers.Contains(b.self), wasB = b.previousContainers.Contains(a.self);
        if (wasA != wasB) return wasA;
        return true; // Earlier registered (a precedes b in occupants).
    }

    // Regions joined by open seams form one hollow space, so the body may cross into all of them.
    private static void SpreadThroughSeams(Occupant occupant)
    {
        frontier.Clear();
        foreach (var region in occupant.containers)
            if (occupant.inside.Add(region)) frontier.Enqueue(region);
        while (frontier.Count > 0)
        {
            var region = frontier.Dequeue();
            foreach (var edge in region.Edges)
                foreach (var opening in edge.openings)
                {
                    var partner = opening.partner;
                    if (partner != null && partner != occupant.self && partner.Available && occupant.inside.Add(partner))
                        frontier.Enqueue(partner);
                }
        }
    }

    private static void Apply(Occupant occupant, List<SafeRegion> regions)
    {
        if (!Active(occupant)) return;
        // Players keep their own rule for fixed (terrain) regions: the support excludes the player layers
        // and the edges are always solid. Only movable SafeBoxes switch between support and edges.
        bool player = (SafeRegion.PlayerMask & (1 << occupant.body.gameObject.layer)) != 0;
        foreach (var region in regions)
        {
            if (region == occupant.self || region.Support == null || (player && !region.Movable)) continue;
            // Two SafeBoxes share one support pair; ignore it if either is inside the other.
            bool ignore = occupant.inside.Contains(region) || (occupant.self != null &&
                byRegion.TryGetValue(region, out var other) && other.inside.Contains(occupant.self));
            Set(occupant, region.Support, ignore);
        }

        var bounds = BodyBounds(occupant);
        Vector2 reach = Vector2.one * QueryMargin + Abs(occupant.body.linearVelocity) * (Time.fixedDeltaTime * 2);
        SafeBoundarySystem.QueryEdges((Vector2)bounds.min - reach, (Vector2)bounds.max + reach, nearby);
        foreach (var edge in nearby)
        {
            if (player && !edge.owner.Movable) continue;
            // Own edges never touch their own body; others' edges only contain bodies inside them.
            bool ignore = edge.owner == occupant.self || !occupant.inside.Contains(edge.owner);
            foreach (var col in edge.colliders) Set(occupant, col, ignore);
        }
        nearby.Clear();
    }

    private static void Set(Occupant occupant, Collider2D obstacle, bool ignore)
    {
        if (obstacle == null || (occupant.ignored.TryGetValue(obstacle, out bool current) && current == ignore)) return;
        foreach (var col in occupant.colliders)
            if (col != null) Physics2D.IgnoreCollision(col, obstacle, ignore);
        occupant.ignored[obstacle] = ignore;
    }

    private static Vector2 Abs(Vector2 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
}
