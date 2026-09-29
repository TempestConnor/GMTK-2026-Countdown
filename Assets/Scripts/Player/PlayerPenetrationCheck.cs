using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

/// <summary>Checks solid overlap before physics can resolve a plane transition.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLife))]
[DefaultExecutionOrder(100)]
public sealed class PlayerPenetrationCheck : MonoBehaviour
{
    [Tooltip("The player's solid movement collider, not the banish reticule or a sensor.")]
    [SerializeField] private Collider2D bodyCollider;

    [SerializeField, Min(0f)] private float terrainPenetrationTolerance = 0.03f;

    private PlayerLife life;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    private BoxCollider2D tileProbe;

    private void OnDestroy()
    {
        if (tileProbe == null) return;
        if (Application.isPlaying) Destroy(tileProbe.gameObject);
        else DestroyImmediate(tileProbe.gameObject);
    }

    private void Reset()
    {
        foreach (var candidate in GetComponents<Collider2D>())
        {
            if (candidate.isTrigger) continue;
            bodyCollider = candidate;
            break;
        }
    }

    private void Awake()
    {
        life = GetComponent<PlayerLife>();
        if (bodyCollider == null) Reset();
        if (bodyCollider == null || bodyCollider.isTrigger)
        {
            Debug.LogError("Assign a solid movement collider to PlayerPenetrationCheck.", this);
            enabled = false;
        }
    }

    private void FixedUpdate() => CheckNow();

    public void CheckNow()
    {
        // Death returns the banished group; that nested check must do nothing.
        if (!isActiveAndEnabled || life == null || !life.isAlive) return;
        if (TryGetLethalOverlap(out _)) life.Kill();
    }

    /// <summary>Queries without killing, also allowing editor validation of geometry.</summary>
    public bool TryGetLethalOverlap(out Collider2D obstacle)
    {
        obstacle = null;
        if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger ||
            !bodyCollider.gameObject.activeInHierarchy) return false;

        var body = bodyCollider.attachedRigidbody;
        if (body != null && !body.simulated) return false;

        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(bodyCollider.gameObject.layer));

        overlaps.Clear();
        bodyCollider.Overlap(filter, overlaps);
        foreach (var other in overlaps)
        {
            if (other == bodyCollider || (body != null && other.attachedRigidbody == body))
                continue;
            if (Physics2D.GetIgnoreCollision(bodyCollider, other)) continue;

            var tilemap = other.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                if (OverlapsLethalTile(tilemap))
                {
                    obstacle = other;
                    return true;
                }
                continue;
            }

            var hazard = other.GetComponentInParent<KillsOnPenetration>();
            if (hazard == null || !hazard.isActiveAndEnabled) continue;

            var separation = bodyCollider.Distance(other);
            if (!separation.isValid || separation.distance >= -hazard.PenetrationTolerance)
                continue;

            obstacle = other;
            return true;
        }
        return false;
    }

    private bool OverlapsLethalTile(Tilemap map)
    {
        // Only inspect cells around this player's bounds, not the whole level.
        Bounds bounds = bodyCollider.bounds;
        Vector3Int min = map.WorldToCell(bounds.min);
        Vector3Int max = min;
        for (int i = 0; i < 4; i++)
        {
            var cell = map.WorldToCell(new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y, map.transform.position.z));
            min = Vector3Int.Min(min, cell);
            max = Vector3Int.Max(max, cell);
        }
        Vector3 size = map.layoutGrid.cellSize;
        Vector3 axisX = map.transform.TransformVector(new Vector3(size.x, 0, 0));
        Vector3 axisY = map.transform.TransformVector(new Vector3(0, size.y, 0));
        Vector2 querySize = new Vector2(axisX.magnitude, axisY.magnitude);
        if (querySize.x <= 0f || querySize.y <= 0f) return false;
        float angle = Mathf.Atan2(axisX.y, axisX.x) * Mathf.Rad2Deg;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            if (!(map.GetTile(cell) is TerrainTile tile) || !tile.killsOnPenetration) continue;
            Vector3 center = map.CellToWorld(cell) + (axisX + axisY) * 0.5f;
            if (tileProbe == null)
            {
                var probeObject = new GameObject("Terrain penetration query") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(probeObject, gameObject.scene);
                probeObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                probeObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                tileProbe = probeObject.AddComponent<BoxCollider2D>();
                tileProbe.enabled = false;
                tileProbe.isTrigger = true;
                tileProbe.excludeLayers = ~0;
            }
            tileProbe.size = querySize;
            // Enable only during this synchronous distance query; no physics step or
            // trigger callback can occur, and no helper enters the level collision geometry.
            tileProbe.enabled = true;
            ColliderDistance2D distance;
            try
            {
                var playerBody = bodyCollider.attachedRigidbody;
                if (playerBody == null) return false;
                distance = Physics2D.Distance(bodyCollider, playerBody.position,
                    playerBody.rotation, tileProbe, center, angle);
            }
            finally { tileProbe.enabled = false; }
            if (distance.isValid && distance.distance < -Mathf.Max(0f, terrainPenetrationTolerance)) return true;
        }
        return false;
    }
}
