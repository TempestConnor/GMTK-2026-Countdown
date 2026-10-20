using System.Collections.Generic;
using UnityEngine;

// Targeting circle for playerController2's banish ability -- tracks whichever Banishables
// are currently overlapping it, so firing just snapshots whatever's touching at that instant.
// Overlaps are queried every frame instead of using trigger callbacks: the circle is teleported
// and resized each frame, and missed/unbalanced enter-exit events made the targeted glow flicker.
[RequireComponent(typeof(CircleCollider2D))]
public class BanishReticule : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Color armingColor = new Color(1f, 0.3f, 0.05f, 1f);
    [SerializeField] private Color armedColor = new Color(1f, 0.84f, 0f, 1f);
    [SerializeField] private float outlineWidth = 0.1f;
    [SerializeField] private int outlineSegments = 48;
    [Tooltip("Outline color every Banishable glows with while the reticule is up.")]
    [SerializeField] private Color banishableGlowColor = new Color(1f, 0.84f, 0f, 1f);
    [Tooltip("Outline color for Banishables currently inside the reticule.")]
    [SerializeField] private Color targetedGlowColor = Color.red;

    [Header("Crosshair ticks (fractions of the radius)")]
    [SerializeField] private float tickInner = 0.45f;
    [SerializeField] private float tickOuter = 1.35f;

    private static readonly Vector3[] TickDirections = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };

    private CircleCollider2D circleCollider;
    private LineRenderer[] ticks;
    private bool armed;
    private HashSet<Banishable> touching = new HashSet<Banishable>();
    private HashSet<Banishable> overlapping = new HashSet<Banishable>();
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private float previousTimeScale;
    private float previousFixedDeltaTime;

    private void OnEnable()
    {
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = previousTimeScale * 0.25f;
        Time.fixedDeltaTime = previousFixedDeltaTime * 0.25f;
        Banishable.SetAimGlow(true, banishableGlowColor, targetedGlowColor);
    }

    private void OnDisable()
    {
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        ClearTouching();
        Banishable.SetAimGlow(false, banishableGlowColor, targetedGlowColor);
        armed = false;
        ApplyColor(armingColor);
    }

    public IEnumerable<Banishable> TouchingMembers
    {
        get
        {
            touching.RemoveWhere(m => m == null);
            return touching;
        }
    }

    private void Awake()
    {
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.isTrigger = true;

        if (outline == null)
        {
            outline = GetComponent<LineRenderer>();
        }

        if (outline != null)
        {
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = outlineSegments;
            outline.widthMultiplier = outlineWidth;
            CreateTicks();
        }

        ApplyColor(armingColor);
    }

    // A LineRenderer is one continuous strip, so each crosshair tick gets its own child renderer
    // mirroring the outline's material/sorting. Built at runtime so the prefab only owns the outline.
    private void CreateTicks()
    {
        ticks = new LineRenderer[TickDirections.Length];
        for (int i = 0; i < ticks.Length; i++)
        {
            var go = new GameObject("CrosshairTick" + i);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var tick = go.AddComponent<LineRenderer>();
            tick.sharedMaterials = outline.sharedMaterials;
            tick.sortingLayerID = outline.sortingLayerID;
            tick.sortingOrder = outline.sortingOrder;
            tick.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tick.receiveShadows = false;
            tick.numCapVertices = outline.numCapVertices;
            tick.useWorldSpace = false;
            tick.loop = false;
            tick.positionCount = 2;
            tick.widthMultiplier = outlineWidth;
            ticks[i] = tick;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        ClearTouching();
        gameObject.SetActive(false);
    }

    public void SetRadius(float radius)
    {
        circleCollider.radius = radius;
        DrawOutline(radius);
    }

    public void SetArmed(bool isArmed)
    {
        if (armed == isArmed) return;

        armed = isArmed;
        ApplyColor(armed ? armedColor : armingColor);
    }

    private void ApplyColor(Color color)
    {
        if (outline == null) return;

        outline.startColor = outline.endColor = color;
        if (ticks == null) return;
        foreach (var tick in ticks)
        {
            tick.startColor = tick.endColor = color;
        }
    }

    private void DrawOutline(float radius)
    {
        if (outline == null) return;

        for (int i = 0; i < outlineSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / outlineSegments;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }

        if (ticks == null) return;
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i].SetPosition(0, TickDirections[i] * (radius * tickInner));
            ticks[i].SetPosition(1, TickDirections[i] * (radius * tickOuter));
        }
    }

    private void ClearTouching()
    {
        foreach (var member in touching)
        {
            if (member != null) member.SetTargeted(false);
        }
        touching.Clear();
    }

    // LateUpdate so the query sees this frame's position/radius from playerController2.Update.
    private void LateUpdate()
    {
        RefreshTouching();
    }

    private void RefreshTouching()
    {
        overlapping.Clear();

        Vector3 scale = transform.lossyScale;
        float radius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (radius > 0f)
        {
            // Same layers the trigger would have contacted, per the collision matrix.
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            Physics2D.OverlapCircle(transform.TransformPoint(circleCollider.offset), radius, filter, hits);

            foreach (var hit in hits)
            {
                if (hit == circleCollider || Excludes(hit)) continue;
                var member = hit.GetComponentInParent<Banishable>();
                if (member != null) overlapping.Add(member);
            }
        }

        foreach (var member in touching)
        {
            if (member != null && !overlapping.Contains(member)) member.SetTargeted(false);
        }
        foreach (var member in overlapping)
        {
            member.SetTargeted(true);
        }

        (touching, overlapping) = (overlapping, touching);
    }

    // Per-collider/body layer overrides the collision matrix doesn't cover (e.g. SafeBox's
    // player-boundary edges only contact the player), mirroring what a trigger contact would allow.
    private bool Excludes(Collider2D hit)
    {
        int bit = 1 << gameObject.layer;
        if ((hit.excludeLayers.value & bit) != 0 && (hit.includeLayers.value & bit) == 0) return true;

        var body = hit.attachedRigidbody;
        return body != null && (body.excludeLayers.value & bit) != 0 && (body.includeLayers.value & bit) == 0;
    }
}
