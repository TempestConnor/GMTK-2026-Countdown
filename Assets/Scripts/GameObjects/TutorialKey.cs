using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A painted-on keycap prompt for tutorials. The label comes from the player's input
/// action binding (keyboard/mouse), so it follows playerActions.inputactions; set
/// <see cref="labelOverride"/> to show custom text instead. The cap is one cell tall and
/// widens in whole cells to fit its label, growing right from the bottom-left root.
/// With the keycap hidden it doubles as a plain painted word.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class TutorialKey : MonoBehaviour
{
    const float Inset = 0.08f;
    const float CornerRadius = 0.2f;
    const float LabelPadding = 0.1f; // per side; keeps any single letter (even W/M) within one cell
    const int CornerSegments = 5;

    [SerializeField] private InputActionReference action;
    [Tooltip("Composite part to show, e.g. left/right/up/down for Move. Empty uses the action's first keyboard/mouse binding.")]
    [SerializeField] private string compositePart;
    [Tooltip("Shown instead of the binding when not empty.")]
    [SerializeField] private string labelOverride;
    [Tooltip("Off draws the label alone, e.g. a word like \"Grab\" beside a key.")]
    [SerializeField] private bool showKeycap = true;
    [Tooltip("Shrinks the label so long names fit a narrower cap, e.g. 0.75 fits SPACE in 2 cells.")]
    [SerializeField, Range(0.4f, 1f)] private float labelScale = 1f;
    [SerializeField] private Color color = new Color(0.91f, 0.86f, 0.77f, 1f);
    [SerializeField, Min(0.02f)] private float strokeWidth = 0.1f;
    [SerializeField, Min(1)] private int minWidthCells = 1;
    [SerializeField] private LineRenderer outline;
    [SerializeField] private TextMeshPro label;

    /// <summary>Footprint width in whole cells (after fitting the label).</summary>
    public int WidthCells { get; private set; } = 1;
    public string Label => label != null ? label.text : string.Empty;

    private void OnEnable() => Refresh();

#if UNITY_EDITOR
    // TMP and renderer updates are not allowed during OnValidate; run them right after.
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Refresh(); };
#endif

    public void Refresh()
    {
        if (outline == null || label == null) return;

        string text = ResolveLabel();
        if (label.text != text) label.text = text;
        if (label.color != color) label.color = color;

        float textWidth = label.GetPreferredValues(text).x * labelScale;
        WidthCells = Mathf.Max(minWidthCells, Mathf.CeilToInt(textWidth + 2f * (LabelPadding + Inset)));
        var size = new Vector2(WidthCells, 1f);

        label.rectTransform.sizeDelta = size / labelScale;
        label.transform.localPosition = size * 0.5f;
        label.transform.localScale = new Vector3(labelScale, labelScale, 1f);

        outline.enabled = showKeycap;
        var points = RoundedRect(new Rect(Vector2.one * Inset, size - 2f * Inset * Vector2.one), CornerRadius);
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = points.Length;
        outline.SetPositions(points);
        outline.widthMultiplier = strokeWidth;
        outline.startColor = outline.endColor = color;
        outline.numCornerVertices = 2;
    }

    private string ResolveLabel()
    {
        if (!string.IsNullOrEmpty(labelOverride)) return labelOverride;
        return BindingLabel(action != null ? action.action : null, compositePart);
    }

    /// <summary>Short keyboard/mouse label for an action's binding, e.g. "SPACE" or "RMB"; "?" if none.</summary>
    public static string BindingLabel(InputAction inputAction, string compositePart)
    {
        if (inputAction == null) return "?";

        foreach (var binding in inputAction.bindings)
        {
            if (binding.isComposite) continue;
            if (!string.IsNullOrEmpty(compositePart) &&
                !(binding.isPartOfComposite && string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase)))
                continue;
            string path = binding.effectivePath;
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))
                return Shorten(InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice));
        }
        return "?";
    }

    private static string Shorten(string name)
    {
        switch (name)
        {
            case "Left Button": return "LMB";
            case "Right Button": return "RMB";
            case "Middle Button": return "MMB";
            default: return name.ToUpperInvariant();
        }
    }

    private static Vector3[] RoundedRect(Rect rect, float radius)
    {
        radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
        var corners = new[]
        {
            new Vector2(rect.xMax - radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius),
            new Vector2(rect.xMax - radius, rect.yMin + radius),
        };
        var points = new Vector3[4 * (CornerSegments + 1)];
        for (int c = 0; c < 4; c++)
            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (c * 90f + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                points[c * (CornerSegments + 1) + i] = corners[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        return points;
    }
}
