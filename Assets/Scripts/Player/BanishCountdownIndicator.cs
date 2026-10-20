using UnityEngine;

// "Recall" reticule shown at the cursor while a volley is out on plane B: a still,
// anticlockwise dashed return arrow (the volley is waiting to come back; no crosshair, so no
// aiming) plus a left-click badge (how to recall). Deny() flashes and shakes it when the player
// tries to aim anyway, so the dropped right-click reads as intentional. PlayReturn() spins it
// up and fades it out once the volley comes back.
[RequireComponent(typeof(LineRenderer))]
public class BanishCountdownIndicator : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private float radius = 0.4f;
    [SerializeField] private int outlineSegments = 32;
    [SerializeField] private Color color = new Color(1f, 0.84f, 0f, 1f);

    [Header("Return arrow")]
    [Tooltip("How much of the circle the arrow's arc covers, in degrees; the rest is the gap.")]
    [SerializeField, Range(30f, 350f)] private float arcDegrees = 290f;
    [Tooltip("Arrowhead length along the arc, as a fraction of the radius.")]
    [SerializeField] private float headLength = 0.45f;
    [Tooltip("Arrowhead half-width across the arc, as a fraction of the radius.")]
    [SerializeField] private float headWidth = 0.35f;
    [Tooltip("Number of dashes making up the arc.")]
    [SerializeField, Min(1)] private int dashCount = 8;
    [Tooltip("Fraction of each dash slot that is drawn; the rest is the gap.")]
    [SerializeField, Range(0.1f, 1f)] private float dashFill = 0.6f;
    [Tooltip("Idle anticlockwise spin, in degrees per second. 0 = still; the return spin-out still plays.")]
    [SerializeField] private float spinSpeed = 0f;

    [Header("Return")]
    [Tooltip("How long the arrow stays up after a return, spinning fast and fading out.")]
    [SerializeField] private float returnDuration = 0.35f;
    [Tooltip("Extra spin at the start of the return, in degrees per second; eases back to idle.")]
    [SerializeField] private float returnSpinBoost = 1080f;

    [Header("Recall badge")]
    [Tooltip("Mouse icon with the left button highlighted -- recall is left-click.")]
    [SerializeField] private Sprite recallBadge;
    [Tooltip("Badge height in world units.")]
    [SerializeField] private float badgeSize = 0.525f;
    [Tooltip("Badge center relative to the ring center, as fractions of the radius.")]
    [SerializeField] private Vector2 badgeOffset = new Vector2(1.5f, -1.3f);
    [SerializeField] private float badgePulseSpeed = 4f;
    [SerializeField] private float badgePulseAmount = 0.08f;

    [Header("Denied feedback")]
    [SerializeField] private Color deniedColor = new Color(1f, 0.2f, 0.15f, 1f);
    [SerializeField] private float deniedDuration = 0.35f;
    [Tooltip("Peak horizontal shake distance in world units.")]
    [SerializeField] private float deniedShake = 0.08f;
    [SerializeField] private float deniedShakeFrequency = 40f;

    private LineRenderer[] dashes;
    private LineRenderer arrowhead;
    private SpriteRenderer badge;
    private float badgeScale = 1f;
    private float deniedTimer;
    private float returnTimer;
    private float spinAngle;

    private int PointsPerDash => Mathf.Max(2, Mathf.CeilToInt((float)outlineSegments / dashCount) + 1);

    private void Awake()
    {
        if (outline == null)
        {
            outline = GetComponent<LineRenderer>();
        }

        CreateDashes();
        CreateArrowhead();
        CreateBadge();
        ApplyColor(0f);
        Draw(Vector3.zero);

        gameObject.SetActive(false);
    }

    // A LineRenderer is one continuous strip, so each dash and the arrowhead get their own
    // renderer mirroring the prefab outline's settings -- built at runtime like BanishReticule's
    // crosshair ticks, so the prefab only owns the first dash.
    private void CreateDashes()
    {
        dashes = new LineRenderer[dashCount];
        dashes[0] = outline;
        ConfigureLine(outline, PointsPerDash);
        for (int i = 1; i < dashCount; i++)
        {
            dashes[i] = CreateLine("Dash" + i, PointsPerDash);
        }
    }

    private void CreateArrowhead()
    {
        arrowhead = CreateLine("Arrowhead", 3);
        arrowhead.numCornerVertices = 2;
    }

    private LineRenderer CreateLine(string lineName, int positionCount)
    {
        var go = new GameObject(lineName);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterials = outline.sharedMaterials;
        line.sortingLayerID = outline.sortingLayerID;
        line.sortingOrder = outline.sortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = outline.numCapVertices;
        line.widthMultiplier = outline.widthMultiplier;
        ConfigureLine(line, positionCount);
        return line;
    }

    private static void ConfigureLine(LineRenderer line, int positionCount)
    {
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = positionCount;
    }

    private void CreateBadge()
    {
        if (recallBadge == null) return;

        var go = new GameObject("RecallBadge");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        badge = go.AddComponent<SpriteRenderer>();
        badge.sprite = recallBadge;
        // The outline's material is unlit, so the badge reads the same regardless of 2D lights.
        badge.sharedMaterial = outline.sharedMaterial;
        badge.sortingLayerID = outline.sortingLayerID;
        badge.sortingOrder = outline.sortingOrder;

        float spriteHeight = recallBadge.bounds.size.y;
        badgeScale = spriteHeight > 0f ? badgeSize / spriteHeight : 1f;
        go.transform.localScale = Vector3.one * badgeScale;
    }

    public void Show()
    {
        deniedTimer = 0f;
        returnTimer = 0f;
        spinAngle = 0f;
        ApplyColor(0f);
        Draw(Vector3.zero);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Flash and shake to signal that aiming is blocked while the volley is out.</summary>
    public void Deny()
    {
        if (!gameObject.activeInHierarchy || returnTimer > 0f) return;
        deniedTimer = deniedDuration;
    }

    /// <summary>The volley came back: spin up, fade out, then hide.</summary>
    public void PlayReturn()
    {
        if (!gameObject.activeInHierarchy || returnDuration <= 0f)
        {
            Hide();
            return;
        }
        deniedTimer = 0f;
        returnTimer = returnDuration;
        ApplyColor(0f);
    }

    // Unscaled so the feedback plays the same no matter what the aim slow-mo left behind.
    private void Update()
    {
        float speed = spinSpeed;
        if (returnTimer > 0f)
        {
            returnTimer = Mathf.Max(0f, returnTimer - Time.unscaledDeltaTime);
            float r = returnTimer / returnDuration;
            if (r <= 0f)
            {
                Hide();
                return;
            }
            speed += returnSpinBoost * r;
            ApplyColor(0f, r);
        }
        spinAngle = Mathf.Repeat(spinAngle + speed * Time.unscaledDeltaTime, 360f);

        Vector3 shake = Vector3.zero;
        if (deniedTimer > 0f)
        {
            deniedTimer = Mathf.Max(0f, deniedTimer - Time.unscaledDeltaTime);
            float t = deniedDuration > 0f ? deniedTimer / deniedDuration : 0f;
            shake.x = Mathf.Sin(Time.unscaledTime * deniedShakeFrequency) * deniedShake * t;
            ApplyColor(t);
        }

        Draw(shake);

        if (badge != null)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * badgePulseSpeed) * badgePulseAmount;
            badge.transform.localScale = Vector3.one * (badgeScale * pulse);
        }
    }

    // denied: 0 = normal, 1 = full denial flash. alpha fades everything out during the return.
    private void ApplyColor(float denied, float alpha = 1f)
    {
        Color ringColor = Color.Lerp(color, deniedColor, denied);
        ringColor.a *= alpha;
        foreach (var dash in dashes) dash.startColor = dash.endColor = ringColor;
        if (arrowhead != null) arrowhead.startColor = arrowhead.endColor = ringColor;
        // The badge keeps its own artwork colors except while flashing.
        if (badge != null)
        {
            Color badgeColor = Color.Lerp(Color.white, deniedColor, denied);
            badgeColor.a *= alpha;
            badge.color = badgeColor;
        }
    }

    private void Draw(Vector3 offset)
    {
        // The arc runs anticlockwise from start to end; the arrowhead caps the end. Each dash sits
        // at the end of its slot, so the last dash runs right into the arrowhead.
        float start = (90f + spinAngle) * Mathf.Deg2Rad;
        float sweep = arcDegrees * Mathf.Deg2Rad;
        float slot = sweep / dashCount;
        int points = PointsPerDash;
        for (int d = 0; d < dashCount; d++)
        {
            float dashEnd = start + slot * (d + 1);
            float dashStart = dashEnd - slot * dashFill;
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.Lerp(dashStart, dashEnd, (float)i / (points - 1));
                dashes[d].SetPosition(i, offset + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }

        if (arrowhead != null)
        {
            float end = start + sweep;
            Vector3 radial = new Vector3(Mathf.Cos(end), Mathf.Sin(end), 0f);
            Vector3 tangent = new Vector3(-radial.y, radial.x, 0f); // anticlockwise direction
            Vector3 baseCenter = offset + radial * radius;
            Vector3 tip = baseCenter + tangent * (radius * headLength);
            arrowhead.SetPosition(0, baseCenter + radial * (radius * headWidth));
            arrowhead.SetPosition(1, tip);
            arrowhead.SetPosition(2, baseCenter - radial * (radius * headWidth));
        }

        if (badge != null)
        {
            badge.transform.localPosition = offset + (Vector3)(badgeOffset * radius);
        }
    }
}
