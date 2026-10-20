using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    private sealed class AimAction : IPlayerAction, IPlayerInputState
    {
        private readonly playerController2 player;
        public AimAction(playerController2 player) { this.player = player; }
        public void ClearInput() => Cancel();
        public void Cancel()
        {
            player.isTargeting = false;
            player.armElapsed = 0;
            if (player.reticule != null) player.reticule.Hide();
        }
    }

    private sealed class BanishAction : IPlayerAction
    {
        private readonly playerController2 player;
        public BanishAction(playerController2 player) { this.player = player; }
        public void Cancel()
        {
            player.ReturnBanished();
            player.snapPoseTimer = 0;
        }
    }

    public struct BanishState
    {
        public bool active;
        public bool playerBanished;
    }

    public BanishState CaptureBanishState() => new BanishState
    {
        active = isBanished,
        playerBanished = isBanished && banishedMembers.Contains(planeMember)
    };

    public void RestoreBanishState(BanishState state)
    {
        if (!state.active) return;
        isBanished = true;
        if (state.playerBanished)
        {
            banishedMembers.Add(planeMember);
            planeMember.SetPlane(Banishable.Plane.B);
            touchingDirection.SetActivePlane(Banishable.Plane.B);
            UpdateCameraVisibility(Banishable.Plane.B);
        }
        ShowCountdownIndicator();
    }
    [SerializeField] private float swapTargetRadius = 2f;

    [Header("Banish")]
    [SerializeField] private BanishStats banishStats;
    [SerializeField] private BanishReticule reticule;
    [SerializeField] private BanishCountdownIndicator countdownIndicator;

    [Header("Banish Flash")]
    [SerializeField] private BanishFlash.Settings banishFlash = BanishFlash.Settings.Default;
    [Tooltip("Where the flash sits on the drawn body, as fractions of its half-extents (x forward, y up).")]
    [SerializeField] private Vector2 banishFlashEyeOffset = new Vector2(0.3f, 0.55f);

    private Banishable _planeMember;
    private Banishable planeMember => _planeMember != null ? _planeMember : (_planeMember = GetComponent<Banishable>());

    // Right-click held (arming or already armed) -- gates whether left-click fires a banish.
    private bool isTargeting;
    private float armElapsed;
    private bool isArmed => banishStats != null && armElapsed >= banishStats.armTime;

    // True while a volley is out on plane B, until the player recalls it with left-click.
    private bool isBanished;

    /// <summary>Raised after a banish catches at least one member (the player included).</summary>
    public static event System.Action<playerController2> BanishFired;
    /// <summary>Raised after an active banish is returned to plane A (recalled or cancelled).</summary>
    public static event System.Action<playerController2> BanishReturned;
    private readonly List<Banishable> banishedMembers = new List<Banishable>();

    private void OnEnable()
    {
        SafePlayerCollision.Register(this);
        SafeBodyCollision.Register(GetComponent<Rigidbody2D>());
        UpdateCameraVisibility(planeMember.CurrentPlane);
    }

    // Only the ground/entity layers are toggled -- Player/PlayerB stay visible on both,
    // since it's the same GameObject just changing layer and must never hide itself.
    private static void UpdateCameraVisibility(Banishable.Plane activePlane)
    {
        var cam = Camera.main;
        if (cam == null) return;

        int planeAMask = (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("Entities"));
        int planeBMask = (1 << LayerMask.NameToLayer("GroundB")) | (1 << LayerMask.NameToLayer("EntityB"));

        cam.cullingMask = activePlane == Banishable.Plane.A
            ? (cam.cullingMask | planeAMask) & ~planeBMask
            : (cam.cullingMask | planeBMask) & ~planeAMask;

        // Set alongside the culling swap so plane B never renders a frame clipped to the preview circle.
        Shader.SetGlobalFloat(PreviewRevealBId, activePlane == Banishable.Plane.A ? 1f : 0f);
    }

    private void Update()
    {
        if (RoomTravel.IsLoading) return;
        if (isTargeting)
        {
            UpdateReticule();
        }

        // Also runs during the indicator's post-return spin-out, so it keeps following the cursor.
        if (countdownIndicator != null && countdownIndicator.gameObject.activeSelf)
        {
            UpdateCountdownIndicator();
        }

        UpdatePreview();
        UpdateAnimation();
    }

    // No return timer: the locked reticule at the cursor marks that a volley is out until recalled.
    private void ShowCountdownIndicator()
    {
        if (countdownIndicator == null) return;
        countdownIndicator.transform.position = GetPointerWorldPosition();
        countdownIndicator.Show();
    }

    private void UpdateCountdownIndicator()
    {
        if (countdownIndicator == null) return;
        countdownIndicator.transform.position = GetPointerWorldPosition();
    }


    // Aim (right-click): hold to arm/show the targeting circle, release to disarm/hide it.
    // While a volley is out, pressing it only plays the denied feedback.
    public void onAim(InputAction.CallbackContext context)
    {
        if(!CanPlay) return;

        if (context.started && !isBanished && !ClickedHud(context))
        {
            if (playerAudio != null) playerAudio.PlayBanishArm();
            // Cut any post-return spin-out short so it doesn't overlap the reticule.
            if (countdownIndicator != null) countdownIndicator.Hide();
            isTargeting = true;
            armElapsed = 0f;
            if (reticule != null) reticule.Show();
        }
        else if (context.started && isBanished && !ClickedHud(context))
        {
            // Aiming is locked until the volley is recalled -- say so instead of ignoring the click.
            if (countdownIndicator != null) countdownIndicator.Deny();
            if (playerAudio != null) playerAudio.PlayBanishDenied();
        }
        else if (context.canceled)
        {
            isTargeting = false;
            if (reticule != null) reticule.Hide();
        }
    }

    // Fire (left-click): fires the armed circle, or -- if a volley is already out -- recalls it.
    public void onBanishFire(InputAction.CallbackContext context)
    {
        if(!CanPlay) return;

        if (!context.started || ClickedHud(context)) return;

        if (isBanished)
        {
            ReturnBanished();
        }
        else if (isTargeting && isArmed)
        {
            FireBanish();
        }
    }

    // Mouse clicks on HUD such as the tutorial log must not aim, fire or recall.
    private static bool ClickedHud(InputAction.CallbackContext context) =>
        context.control?.device is Mouse mouse && TutorialLog.IsPointerOver(mouse.position.ReadValue());

    private void UpdateReticule()
    {
        if (banishStats == null || reticule == null) return;

        armElapsed = Mathf.Min(armElapsed + Time.deltaTime, banishStats.armTime);

        reticule.transform.position = GetPointerWorldPosition();

        float radius = banishStats.armTime > 0f
            ? Mathf.Lerp(0f, banishStats.targetRadius, armElapsed / banishStats.armTime)
            : banishStats.targetRadius;
        reticule.SetRadius(radius);
        reticule.SetArmed(isArmed);
    }

    private Vector3 GetPointerWorldPosition()
    {
        var cam = Camera.main;
        if (cam == null) return transform.position;

        if (Mouse.current == null) return transform.position;
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = transform.position.z;
        return world;
    }

    private void FireBanish()
    {
        if (reticule == null) return;

        banishedMembers.Clear();
        
        foreach (var member in reticule.TouchingMembers)
        {
            banishedMembers.Add(member);
            member.SetPlane(Banishable.Plane.B);

            if (member == planeMember)
            {
                touchingDirection.SetActivePlane(Banishable.Plane.B);
                UpdateCameraVisibility(Banishable.Plane.B);
            }
        }

        if (banishedMembers.Count == 0) return;
        reticule.Hide();
        if (playerAudio != null) playerAudio.PlayBanishFire();
        PlayBanishFlash();
        isBanished = true;
        snapPoseTimer = snapPoseDuration;
        ShowCountdownIndicator();
        CheckDamageablePenetration();
        BanishFired?.Invoke(this);
    }

    private void PlayBanishFlash()
    {
        Vector3 position = transform.position;
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            float forward = spriteRenderer.flipX ? -1f : 1f;
            position = bounds.center + new Vector3(
                bounds.extents.x * banishFlashEyeOffset.x * forward,
                bounds.extents.y * banishFlashEyeOffset.y * GravityUpSign,
                0f);
        }
        BanishFlash.Play(position, banishFlash);
    }

    // Forces every banished member (including the player, if they were caught in the volley)
    // back to plane A -- used for the left-click recall and when the banish action is cancelled.
    private void ReturnBanished()
    {
        if (!isBanished) return;

        foreach (var member in banishedMembers)
        {
            if (member == null) continue;

            member.SetPlane(Banishable.Plane.A);

            if (member == planeMember)
            {
                touchingDirection.SetActivePlane(Banishable.Plane.A);
                UpdateCameraVisibility(Banishable.Plane.A);
            }
        }

        banishedMembers.Clear();
        isBanished = false;
        if (countdownIndicator != null) countdownIndicator.PlayReturn();
        if (playerAudio != null) playerAudio.PlayBanishReturn();
        CheckDamageablePenetration();
        BanishReturned?.Invoke(this);
    }

    private void CheckDamageablePenetration()
    {
        SafeBoundarySystem.RefreshNow();
        // Wait until the entire volley is on its final plane before testing overlap.
        foreach (var check in FindObjectsByType<DamageablePenetrationCheck>(FindObjectsSortMode.None))
            if (check != null) check.CheckNow();
    }
}

