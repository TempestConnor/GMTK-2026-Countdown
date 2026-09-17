using System.Collections.Generic;
using UnityEngine;

/// <summary>Reverses vertical gravity while a dynamic body overlaps this area.</summary>
[RequireComponent(typeof(BoxCollider2D))]
[DefaultExecutionOrder(-100)]
public sealed class GravityReversalArea : MonoBehaviour
{
    [Tooltip("Local width and height, measured from the root's bottom-left corner.")]
    public Vector2 areaSize = new Vector2(4f, 4f);
    public LayerMask affectedLayers = ~0;
    [SerializeField] private SpriteRenderer areaVisual;

    private BoxCollider2D area;
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private readonly HashSet<Rigidbody2D> inside = new HashSet<Rigidbody2D>();
    private readonly HashSet<Rigidbody2D> current = new HashSet<Rigidbody2D>();
    private readonly List<Rigidbody2D> leaving = new List<Rigidbody2D>();

    private sealed class Effect
    {
        public int count;
        public float originalScale;
        public playerController2 player;
    }
    private static readonly Dictionary<Rigidbody2D, Effect> effects = new Dictionary<Rigidbody2D, Effect>();

    private void Awake() => ConfigureArea();
    private void OnValidate() => ConfigureArea();

    public void ConfigureArea()
    {
        area = GetComponent<BoxCollider2D>();
        areaSize = new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y));
        area.isTrigger = true;
        area.size = areaSize;
        area.offset = areaSize * 0.5f;
        if (areaVisual != null)
        {
            areaVisual.transform.localPosition = area.offset;
            areaVisual.transform.localScale = new Vector3(areaSize.x, areaSize.y, 1f);
        }
    }

    private void FixedUpdate()
    {
        current.Clear();
        if (area.enabled)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(affectedLayers);
            filter.useTriggers = false;
            area.Overlap(filter, hits);
            foreach (var hit in hits)
            {
                var body = hit.attachedRigidbody;
                if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic) continue;
                if (Physics2D.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer)) continue;
                if (Physics2D.GetIgnoreCollision(area, hit)) continue;
                current.Add(body);
                if (inside.Add(body)) Enter(body);
            }
        }
        leaving.Clear();
        foreach (var body in inside)
            if (!current.Contains(body)) leaving.Add(body);
        foreach (var body in leaving)
        {
            Exit(body);
            inside.Remove(body);
        }
    }

    private static void Enter(Rigidbody2D body)
    {
        if (!effects.TryGetValue(body, out var effect))
        {
            effect = new Effect { originalScale = body.gravityScale, player = body.GetComponent<playerController2>() };
            effects.Add(body, effect);
            if (effect.player != null) effect.player.SetGravityReversed(true);
            else body.gravityScale = -effect.originalScale;
            body.WakeUp();
        }
        effect.count++;
    }

    private static void Exit(Rigidbody2D body)
    {
        if (!effects.TryGetValue(body, out var effect) || --effect.count > 0) return;
        effects.Remove(body);
        if (body == null) return;
        if (effect.player != null) effect.player.SetGravityReversed(false);
        else body.gravityScale = effect.originalScale;
        body.WakeUp();
    }

    private void OnDisable()
    {
        foreach (var body in inside) Exit(body);
        inside.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(areaSize * 0.5f, areaSize);
    }
}
