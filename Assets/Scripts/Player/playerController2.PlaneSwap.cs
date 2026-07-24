using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    [SerializeField] private float swapTargetRadius = 2f;

    [Header("Banish")]
    [SerializeField] private BanishStats banishStats;
    [SerializeField] private BanishReticule reticule;
    [SerializeField] private BanishCountdownIndicator countdownIndicator;

    private Banishable _planeMember;
    private Banishable planeMember => _planeMember != null ? _planeMember : (_planeMember = GetComponent<Banishable>());

    // Right-click held (arming or already armed) -- gates whether left-click fires a
    // banish instead of a normal attack. See Combat.cs's onAttack guard.
    private bool isTargeting;
    private float armElapsed;
    private bool isArmed => banishStats != null && armElapsed >= banishStats.armTime;

    // True while a volley is out on plane B awaiting its return timer or an early recall.
    private bool isBanished;
    private float banishElapsed;
    private readonly List<Banishable> banishedMembers = new List<Banishable>();
    private Coroutine returnCoroutine;

    private void OnEnable()
    {
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

    // Temporary debug binding -- real swap input/targeting (grab-based?) is not decided yet.
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            SwapSelfPlane();
        }

        if (Mouse.current != null)
        {
            HandleBanishInput();
        }

        if (isBanished)
        {
            UpdateCountdownIndicator();
        }
    }

    private void UpdateCountdownIndicator()
    {
        if (countdownIndicator == null || banishStats == null) return;

        banishElapsed = Mathf.Min(banishElapsed + Time.deltaTime, banishStats.returnDelay);
        countdownIndicator.transform.position = GetPointerWorldPosition();

        float remainingFraction = banishStats.returnDelay > 0f
            ? 1f - banishElapsed / banishStats.returnDelay
            : 0f;
        countdownIndicator.SetProgress(remainingFraction);
    }

    private void SwapSelfPlane()
    {
        if (planeMember == null) return;

        planeMember.TogglePlane();
        touchingDirection.SetActivePlane(planeMember.CurrentPlane);
        UpdateCameraVisibility(planeMember.CurrentPlane);
    }

    // Right-click: hold to arm/show the targeting circle, release to disarm/hide it.
    // Left-click: fires the armed circle, or -- if a volley is already out -- recalls it early.
    private void HandleBanishInput()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && !isBanished)
        {
            if (playerAudio != null) playerAudio.PlayBanishArm();
            isTargeting = true;
            armElapsed = 0f;
            if (reticule != null) reticule.Show();
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isTargeting = false;
            if (reticule != null) reticule.Hide();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isBanished)
            {
                ReturnBanished();
            }
            else if (isTargeting && isArmed)
            {
                FireBanish();
            }
        }

        if (isTargeting)
        {
            UpdateReticule();
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
        returnCoroutine = StartCoroutine(ReturnAfterDelay());
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(banishStats.returnDelay);
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
    }
}
