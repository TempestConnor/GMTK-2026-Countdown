using UnityEngine;

/// <summary>
/// A painted-on tutorial arrow: a stroke from the root cell's center to <see cref="End"/>,
/// optionally bowed upward into a jump arc, finished with an open chevron head.
/// Purely decorative -- no collider, unaffected by planes and lighting.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class TutorialArrow : MonoBehaviour
{
    // The tail sits on the root cell's center so painting the arrow with EntityBrush
    // (root = bottom-left of the clicked cell) starts it in the middle of that cell.
    public static readonly Vector2 Tail = new Vector2(0.5f, 0.5f);
    const int ArcSegments = 24;

    [Tooltip("Arrow tip, in cells, relative to the tail (the root cell's center).")]
    [SerializeField] private Vector2 end = new Vector2(2f, 0f);
    [Tooltip("How far the arc's midpoint bows upward (world up), in cells. 0 draws a straight arrow.")]
    [SerializeField] private float arcHeight;
    [SerializeField] private Color color = new Color(0.91f, 0.86f, 0.77f, 1f);
    [SerializeField, Min(0.02f)] private float strokeWidth = 0.16f;
    [SerializeField, Min(0f)] private float headLength = 0.45f;
    [SerializeField, Range(10f, 80f)] private float headAngle = 38f;
    [SerializeField] private LineRenderer shaft;
    [SerializeField] private LineRenderer head;

    public Vector2 End { get => end; set { end = value; Refresh(); } }
    public float ArcHeight { get => arcHeight; set { arcHeight = value; Refresh(); } }

    /// <summary>Local-space point along the stroke, t in [0, 1].</summary>
    public Vector2 Evaluate(float t) => Tail + end * t + Vector2.up * (4f * arcHeight * t * (1f - t));

    private void OnEnable() => Refresh();

#if UNITY_EDITOR
    // LineRenderer setters are safe here, but deferring keeps OnValidate side-effect free
    // (no renderer writes while Unity is still deserializing the prefab instance).
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Refresh(); };
#endif

    public void Refresh()
    {
        if (shaft == null || head == null) return;

        int count = Mathf.Approximately(arcHeight, 0f) ? 2 : ArcSegments + 1;
        var points = new Vector3[count];
        for (int i = 0; i < count; i++) points[i] = Evaluate(i / (float)(count - 1));
        Apply(shaft, points);

        // Head direction follows the stroke's final tangent so arcs land "into" their target.
        Vector2 tip = Evaluate(1f);
        Vector2 tangent = end + Vector2.up * (-4f * arcHeight);
        Vector2 back = (tangent.sqrMagnitude > 1e-6f ? -tangent.normalized : Vector2.left) * headLength;
        Apply(head, new Vector3[]
        {
            tip + (Vector2)(Quaternion.Euler(0, 0, headAngle) * back),
            tip,
            tip + (Vector2)(Quaternion.Euler(0, 0, -headAngle) * back),
        });
    }

    private void Apply(LineRenderer line, Vector3[] points)
    {
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.widthMultiplier = strokeWidth;
        line.startColor = line.endColor = color;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
    }
}
