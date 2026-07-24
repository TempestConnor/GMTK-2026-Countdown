using System.Collections.Generic;
using UnityEngine;

// Trigger volume for playerController2's banish ability -- tracks whichever Banishables
// are currently overlapping it, so firing just snapshots whatever's touching at that instant.
[RequireComponent(typeof(CircleCollider2D))]
public class BanishReticule : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Color armingColor = Color.white;
    [SerializeField] private Color armedColor = Color.green;
    [SerializeField] private int outlineSegments = 48;

    private CircleCollider2D circleCollider;
    private readonly HashSet<Banishable> touching = new HashSet<Banishable>();

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

    public void SetArmed(bool armed)
    {
        if (outline == null) return;

        outline.startColor = outline.endColor = armed ? armedColor : armingColor;
    }

    private void DrawOutline(float radius)
    {
        if (outline == null) return;

        for (int i = 0; i < outlineSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / outlineSegments;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
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
