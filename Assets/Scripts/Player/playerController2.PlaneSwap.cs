using System.Collections;
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
            player.banishElapsed = 0;
        }
    }

    public struct BanishState
    {
        public bool active;
        public bool playerBanished;
        public float remaining;
    }

    public BanishState CaptureBanishState() => new BanishState
    {
        active = isBanished,
        playerBanished = isBanished && banishedMembers.Contains(planeMember),
        remaining = banishStats == null ? 0 : Mathf.Max(0, banishStats.returnDelay - banishElapsed)
    };

    public void RestoreBanishState(BanishState state)
    {
        if (!state.active || banishStats == null) return;
        isBanished = true;
        banishElapsed = Mathf.Max(0, banishStats.returnDelay - state.remaining);
        if (state.playerBanished)
        {
            banishedMembers.Add(planeMember);
            planeMember.SetPlane(Banishable.Plane.B);
            touchingDirection.SetActivePlane(Banishable.Plane.B);
            UpdateCameraVisibility(Banishable.Plane.B);
        }
        if (countdownIndicator != null) countdownIndicator.Show();
        returnCoroutine = StartCoroutine(ReturnAfterDelay(state.remaining));
    }
    [SerializeField] private float swapTargetRadius = 2f;

    [Header("Banish")]
    [SerializeField] private BanishStats banishStats;
    [SerializeField] private BanishReticule reticule;
    [SerializeField] private BanishCountdownIndicator countdownIndicator;

    private Banishable _planeMember;
    private Banishable planeMember => _planeMember != null ? _planeMember : (_planeMember = GetComponent<Banishable>());

    // Right-click held (arming or already armed) -- gates whether left-click fires a banish.
    private bool isTargeting;
    private float armElapsed;
    private bool isArmed => banishStats != null && armElapsed >= banishStats.armTime;

    // True while a volley is out on plane B awaiting its return timer or an early recall.
    private bool isBanished;

    /// <summary>Raised after a banish catches at least one member (the player included).</summary>
    public static event System.Action<playerController2> BanishFired;
    private float banishElapsed;
    private readonly List<Banishable> banishedMembers = new List<Banishable>();
    private Coroutine returnCoroutine;

    private void OnEnable()
    {
        SafePlayerCollision.Register(this);
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
    }

    private void Update()
    {
        if (RoomTravel.IsLoading) return;
        if (isTargeting)
        {
            UpdateReticule();
        }

        if (isBanished)
        {
            UpdateCountdownIndicator();
        }

        UpdatePreview();
    }

    private void UpdateCountdownIndicator()
    {
        if (banishStats == null) return;

        banishElapsed = Mathf.Min(banishElapsed + Time.deltaTime, banishStats.returnDelay);
        if (countdownIndicator == null) return;
        countdownIndicator.transform.position = GetPointerWorldPosition();

        float remainingFraction = banishStats.returnDelay > 0f
            ? 1f - banishElapsed / banishStats.returnDelay
            : 0f;
        countdownIndicator.SetProgress(remainingFraction);
    }


    // Aim (right-click): hold to arm/show the targeting circle, release to disarm/hide it.
    public void onAim(InputAction.CallbackContext context)
    {
        if(!CanPlay) return;

        if (context.started && !isBanished)
        {
            if (playerAudio != null) playerAudio.PlayBanishArm();
            isTargeting = true;
            armElapsed = 0f;
            if (reticule != null) reticule.Show();
        }
        else if (context.canceled)
        {
            isTargeting = false;
            if (reticule != null) reticule.Hide();
        }
    }

    // Fire (left-click): fires the armed circle, or -- if a volley is already out -- recalls it early.
    public void onBanishFire(InputAction.CallbackContext context)
    {
        if(!CanPlay) return;

        if (!context.started) return;

        if (isBanished)
        {
            ReturnBanished();
        }
        else if (isTargeting && isArmed)
        {
            FireBanish();
        }
    }

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
        isBanished = true;
        banishElapsed = 0f;
        if (countdownIndicator != null) countdownIndicator.Show();
        returnCoroutine = StartCoroutine(ReturnAfterDelay(banishStats.returnDelay));
        CheckDamageablePenetration();
        BanishFired?.Invoke(this);
    }

    private IEnumerator ReturnAfterDelay(float remaining)
    {
        while (remaining > 0)
        {
            yield return null;
            if (!RoomTravel.IsLoading) remaining -= Time.deltaTime;
        }
        returnCoroutine = null;
        ReturnBanished();
    }

    // Forces every banished member (including the player, if they were caught in the volley)
    // back to plane A -- used for both the automatic timer and an early left-click recall.
    private void ReturnBanished()
    {
        if (!isBanished) return;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

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
        if (countdownIndicator != null) countdownIndicator.Hide();
        if (playerAudio != null) playerAudio.PlayBanishReturn();
        CheckDamageablePenetration();
    }

    private void CheckDamageablePenetration()
    {
        SafeBoundarySystem.RefreshNow();
        // Wait until the entire volley is on its final plane before testing overlap.
        foreach (var check in FindObjectsByType<DamageablePenetrationCheck>(FindObjectsSortMode.None))
            if (check != null) check.CheckNow();
    }
}

