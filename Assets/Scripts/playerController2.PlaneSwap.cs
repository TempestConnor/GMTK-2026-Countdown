using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    [SerializeField] private float swapTargetRadius = 2f;

    private PlaneMember _planeMember;
    private PlaneMember planeMember => _planeMember != null ? _planeMember : (_planeMember = GetComponent<PlaneMember>());

    private void OnEnable()
    {
        UpdateCameraVisibility(planeMember.CurrentPlane);
    }

    // Only the ground/entity layers are toggled -- Player/PlayerB stay visible on both,
    // since it's the same GameObject just changing layer and must never hide itself.
    private static void UpdateCameraVisibility(PlaneMember.Plane activePlane)
    {
        var cam = Camera.main;
        if (cam == null) return;

        int planeAMask = (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("Entities"));
        int planeBMask = (1 << LayerMask.NameToLayer("GroundB")) | (1 << LayerMask.NameToLayer("EntityB"));

        cam.cullingMask = activePlane == PlaneMember.Plane.A
            ? (cam.cullingMask | planeAMask) & ~planeBMask
            : (cam.cullingMask | planeBMask) & ~planeAMask;
    }

    // Temporary debug bindings -- real swap input/targeting (grab-based?) is not decided yet.
    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            SwapSelfPlane();
        }

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            SwapNearestObjectPlane();
        }
    }

    private void SwapSelfPlane()
    {
        if (planeMember == null) return;

        planeMember.TogglePlane();
        touchingDirection.SetActivePlane(planeMember.CurrentPlane);
        UpdateCameraVisibility(planeMember.CurrentPlane);
    }

    private void SwapNearestObjectPlane()
    {
        PlaneMember nearest = null;
        float nearestDist = swapTargetRadius;

        foreach (var candidate in FindObjectsByType<PlaneMember>(FindObjectsSortMode.None))
        {
            if (candidate == planeMember) continue;

            float dist = Vector2.Distance(transform.position, candidate.transform.position);
            if (dist < nearestDist)
            {
                nearest = candidate;
                nearestDist = dist;
            }
        }

        nearest?.TogglePlane();
    }
}
