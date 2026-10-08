using System.Collections.Generic;
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
    [Tooltip("Outline this object while the banish reticule is up.")]
    [SerializeField] private bool glowWhenAiming = true;

    public PlaneChangedEvent onPlaneChanged;

    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int OutlineAmountId = Shader.PropertyToID("_OutlineAmount");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private MaterialPropertyBlock mpb;

    // Every enabled Banishable, so the reticule can light them all up at once. The glow state is
    // static too, so a Banishable enabled mid-aim (room load, respawn) picks it up in OnEnable.
    private static readonly HashSet<Banishable> active = new HashSet<Banishable>();
    private static bool aimGlowOn;
    private static Color aimGlowColor = new Color(1f, 0.84f, 0f, 1f);

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
        active.Add(this);
        Apply();
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (aimGlowOn) ApplyGlow(false);
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

    // Called by BanishReticule as it shows/hides.
    public static void SetAimGlow(bool on, Color color)
    {
        aimGlowOn = on;
        aimGlowColor = color;
        foreach (var member in active)
        {
            if (member != null) member.ApplyGlow(on);
        }
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
        // Layered visuals (SafeBox's fill under its frame) share the target's plane layer and tint;
        // a child left on the Plane A layer would stay visible after banishing.
        foreach (var sr in targetRenderer.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.gameObject.layer = layer;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(SaturationId, currentPlane == Plane.A ? 1f : 0f);
            sr.SetPropertyBlock(mpb);
        }

        ApplyGlow(aimGlowOn);
    }

    // Only the target renderer gets the outline -- layered children (SafeBox's fill) sit inside it.
    private void ApplyGlow(bool on)
    {
        if (targetRenderer == null) return;

        mpb ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(OutlineAmountId, on && glowWhenAiming ? 1f : 0f);
        mpb.SetColor(OutlineColorId, aimGlowColor);
        targetRenderer.SetPropertyBlock(mpb);
    }
}
