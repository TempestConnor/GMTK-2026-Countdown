using UnityEngine;

// Small radial-wipe circle shown at the cursor while a volley is out on plane B --
// the arc shrinks from a full circle to nothing as the return timer elapses.
[RequireComponent(typeof(LineRenderer))]
public class BanishCountdownIndicator : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private float radius = 0.4f;
    [SerializeField] private int outlineSegments = 32;
    [SerializeField] private Color color = Color.cyan;

    private void Awake()
    {
        if (outline == null)
        {
            outline = GetComponent<LineRenderer>();
        }

        outline.useWorldSpace = false;
        outline.loop = false;
        outline.startColor = outline.endColor = color;

        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // remainingFraction: 1 = just fired (full circle), 0 = about to return (nothing left)
    public void SetProgress(float remainingFraction)
    {
        remainingFraction = Mathf.Clamp01(remainingFraction);
        int pointCount = Mathf.Max(2, Mathf.RoundToInt(outlineSegments * remainingFraction) + 1);
        outline.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float angle = 2f * Mathf.PI * i / outlineSegments;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
    }
}
