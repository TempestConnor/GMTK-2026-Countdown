using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
public class Banishable : MonoBehaviour
{
    public enum Plane { A, B }

    [System.Serializable]
    public class PlaneChangedEvent : UnityEvent<Plane> { }

    [SerializeField] private Plane currentPlane = Plane.A;
    [SerializeField] private int layerOnA;
    [SerializeField] private int layerOnB;
    [SerializeField] private SpriteRenderer targetRenderer;

    public PlaneChangedEvent onPlaneChanged;

    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private MaterialPropertyBlock mpb;

    public Plane CurrentPlane => currentPlane;

    private void Reset()
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        Apply();
    }

    private void OnValidate()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        Apply();
    }

    public void SetPlane(Plane p)
    {
        currentPlane = p;
        Apply();
        onPlaneChanged?.Invoke(currentPlane);
    }

    public void TogglePlane()
    {
        SetPlane(currentPlane == Plane.A ? Plane.B : Plane.A);
    }

    private void Apply()
    {
        int layer = currentPlane == Plane.A ? layerOnA : layerOnB;
        gameObject.layer = layer;

        if (targetRenderer == null) return;

        targetRenderer.gameObject.layer = layer;

        mpb ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(SaturationId, currentPlane == Plane.A ? 1f : 0f);
        targetRenderer.SetPropertyBlock(mpb);
    }
}
