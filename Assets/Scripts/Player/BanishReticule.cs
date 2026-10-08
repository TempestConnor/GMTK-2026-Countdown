using System.Collections.Generic;
using UnityEngine;

// Trigger volume for playerController2's banish ability -- tracks whichever Banishables
// are currently overlapping it, so firing just snapshots whatever's touching at that instant.
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

    [Header("Crosshair ticks (fractions of the radius)")]
    [SerializeField] private float tickInner = 0.45f;
    [SerializeField] private float tickOuter = 1.35f;

    private static readonly Vector3[] TickDirections = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };

    private CircleCollider2D circleCollider;
    private LineRenderer[] ticks;
    private bool armed;
    private readonly HashSet<Banishable> touching = new HashSet<Banishable>();
    private float previousTimeScale;
    private float previousFixedDeltaTime;

    private void OnEnable()
    {
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = previousTimeScale * 0.25f;
        Time.fixedDeltaTime = previousFixedDeltaTime * 0.25f;
        Banishable.SetAimGlow(true, banishableGlowColor);
    }

    private void OnDisable()
    {
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        Banishable.SetAimGlow(false, banishableGlowColor);
        touching.Clear();
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
        touching.Clear();
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        var member = other.GetComponentInParent<Banishable>();
        if (member != null)
        {
            touching.Add(member);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var member = other.GetComponentInParent<Banishable>();
        if (member != null)
        {
            touching.Remove(member);
        }
    }
}
