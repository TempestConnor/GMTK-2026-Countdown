using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    [Header("Plane Preview")]
    [SerializeField] private PreviewStats previewStats;

    private static readonly int PreviewCenterId = Shader.PropertyToID("_PreviewCenter");
    private static readonly int PreviewRadiusId = Shader.PropertyToID("_PreviewRadius");
    private static readonly int PreviewEdgeSoftnessId = Shader.PropertyToID("_PreviewEdgeSoftness");
    private static readonly int PreviewHideEligibleId = Shader.PropertyToID("_PreviewHideEligible");

    private bool isPreviewHeld;
    private float previewRadius;
    private bool hasExemptedSelfFromPreviewHide;

    // Preview (hold E): peeks at plane B in a circle around the player without actually
    // swapping planes -- movement/collision stay on plane A the whole time. Plane A's own
    // ground/background materials (Sprite-Lit-PlaneAHide) go transparent inside the circle;
    // plane B renders normally underneath (it already sorts behind plane A in the Sorting
    // Layers list), so punching the hole is enough to reveal it -- no extra draw pass needed.
    public void onPreview(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isPreviewHeld = true;
        }
        else if (context.canceled)
        {
            isPreviewHeld = false;
        }
    }

    private void UpdatePreview()
    {
        if (previewStats == null) return;

        if (!hasExemptedSelfFromPreviewHide)
        {
            ExemptSelfFromPreviewHide();
            hasExemptedSelfFromPreviewHide = true;
        }

        float target = isPreviewHeld ? previewStats.maxRadius : 0f;
        previewRadius = Mathf.MoveTowards(previewRadius, target, previewStats.expansionSpeed * Time.deltaTime);

        // Only manage plane B's culling bits ourselves while actually on plane A -- once fully
        // swapped, PlaneSwap.cs's UpdateCameraVisibility already owns them, don't fight it.
        if (planeMember.CurrentPlane == Banishable.Plane.A)
        {
            SetPlaneBVisibleForPreview(previewRadius > 0f);
        }

        Shader.SetGlobalVector(PreviewCenterId, transform.position);
        Shader.SetGlobalFloat(PreviewRadiusId, previewRadius);
        Shader.SetGlobalFloat(PreviewEdgeSoftnessId, previewStats.edgeSoftness);
    }

    // Plane A entities (boxes, doors, switches, ...) share Sprite-Lit-Saturation with the player
    // and hide inside the preview circle by default -- the player needs a per-instance override
    // so it doesn't hide itself. MaterialPropertyBlock read-modify-write here means this survives
    // Banishable.Apply() re-setting _Saturation later (that call preserves whatever else is set).
    private void ExemptSelfFromPreviewHide()
    {
        if (spriteRenderer == null) return;

        var mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(PreviewHideEligibleId, 0f);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    private static void SetPlaneBVisibleForPreview(bool visible)
    {
        var cam = Camera.main;
        if (cam == null) return;

        int planeBMask = (1 << LayerMask.NameToLayer("GroundB")) | (1 << LayerMask.NameToLayer("EntityB"));
        cam.cullingMask = visible ? (cam.cullingMask | planeBMask) : (cam.cullingMask & ~planeBMask);
    }
}
