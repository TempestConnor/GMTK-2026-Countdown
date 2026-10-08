using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared query filtering and nonlethal correction for closing safe boundaries.</summary>
public static class SafePlayerCollision
{
    private static readonly HashSet<playerController2> players = new HashSet<playerController2>();
    private static readonly List<Collider2D> overlaps = new List<Collider2D>();
    private static readonly List<EdgeCollider2D> pending = new List<EdgeCollider2D>();
    public static void Register(playerController2 player) => players.Add(player);
    public static void Unregister(playerController2 player) => players.Remove(player);

    // Collider casts do not apply per-collider layer overrides. Keep physical support out of player queries.
    public static bool Blocks(Collider2D player, Collider2D other) => other != null &&
        other.enabled && (other.excludeLayers.value & (1 << player.gameObject.layer)) == 0 &&
        !Physics2D.GetIgnoreLayerCollision(player.gameObject.layer, other.gameObject.layer) &&
        !Physics2D.GetIgnoreCollision(player, other);

    public static void ResolveClosures(List<EdgeCollider2D> changed)
    {
        foreach (var edge in pending)
            if (edge != null && edge.gameObject.activeInHierarchy)
            {
                edge.enabled = true;
                if (!changed.Contains(edge)) changed.Add(edge);
            }
        pending.Clear();
        if (changed.Count == 0 || players.Count == 0) return;
        Physics2D.SyncTransforms();
        foreach (var player in players)
        {
            if (player == null || !player.isActiveAndEnabled) continue;
            var body = player.GetComponent<Rigidbody2D>();
            var capsule = player.GetComponent<CapsuleCollider2D>();
            if (!body.simulated || !capsule.enabled) continue;
            bool closes = false;
            foreach (var edge in changed)
            {
                if (edge == null || edge.gameObject.scene != player.gameObject.scene || !Blocks(capsule, edge)) continue;
                var distance = capsule.Distance(edge);
                if (distance.isValid && distance.distance < -.005f) { closes = true; break; }
            }
            if (!closes) continue;
            Vector2 origin = body.position;
            bool found = false;
            // Nearest sampled clear pose, checked against ALL solid geometry on this plane,
            // including filled lethal terrain and ordinary boxes, not just the closing edge.
            for (float radius = .05f; radius <= 4f && !found; radius += .05f)
            for (int i = 0; i < 32 && !found; i++)
            {
                float angle = i * Mathf.PI / 16;
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!Clear(capsule, body, candidate)) continue;
                player.ReleaseGrab();
                body.position = candidate;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                found = true;
            }
            if (found) continue;
            // A completely packed level may have no valid nearby pose. Defer only the
            // intersecting sections and retry; never solve a safe closure by killing/crushing.
            foreach (var edge in changed)
            {
                if (edge == null || edge.gameObject.scene != player.gameObject.scene || !Blocks(capsule, edge)) continue;
                var distance = capsule.Distance(edge);
                if (!distance.isValid || distance.distance >= -.005f) continue;
                edge.enabled = false;
                pending.Add(edge);
            }
        }
    }

    private static bool Clear(CapsuleCollider2D capsule, Rigidbody2D body, Vector2 position)
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(capsule.gameObject.layer));
        // Test the candidate pose with a capsule shrunk by solver slop, so resting contact counts as clear.
        Vector2 center = (Vector2)capsule.bounds.center + position - body.position;
        Vector2 size = (Vector2)capsule.bounds.size - Vector2.one * .02f;
        capsule.gameObject.scene.GetPhysicsScene2D().OverlapCapsule(center, size, capsule.direction, 0, filter, overlaps);
        foreach (var other in overlaps)
        {
            if (other.attachedRigidbody == body || !Blocks(capsule, other)) continue;
            if (other.isTrigger && other.GetComponentInParent<KillsOnContact>() == null) continue;
            return false;
        }
        return true;
    }
}
