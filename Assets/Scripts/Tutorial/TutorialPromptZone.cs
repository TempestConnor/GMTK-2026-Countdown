using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a tutorial prompt on screen when the player enters (or banishes, with
/// <see cref="showOnBanish"/>; after dying in the room, with <see cref="showAfterDeath"/>; or when
/// another prompt or a <see cref="TutorialTriggerZone"/> shows it). If another prompt is on screen, this one waits
/// for it. It leaves after <see cref="duration"/>, when the player performs
/// <see cref="completeAction"/>, or when it is dismissed, whichever comes first. Completing by
/// action or duration then shows the <see cref="thenShow"/> prompts.
/// </summary>
public sealed class TutorialPromptZone : TutorialZone
{
    [Tooltip("[KEY_NAME] = button prompt, [@Action] = that action's binding, ~text~ = spooky wiggle.")]
    [TextArea(2, 5)] public string text = "Hold [RIGHT_CLICK] to Aim, Press [LEFT_CLICK] to Banish";
    [Tooltip("Real-time seconds on screen after fading in. 0 = stays until completed or dismissed.")]
    [Min(0f)] public float duration = 4f;
    [Tooltip("Off = the area is ignored; only another prompt's Then Show or a Tutorial Trigger Zone shows this prompt.")]
    public bool showOnEnter = true;
    [Tooltip("Also shown when the player successfully banishes something (themselves included), anywhere in the room.")]
    public bool showOnBanish;
    [Tooltip("Shown when the room loads because the player died in it, before the room's other prompts.")]
    public bool showAfterDeath;
    [Tooltip("Real-time seconds between being triggered and queuing the prompt.")]
    [Min(0f)] public float delay;

    [Header("Complete by input")]
    [Tooltip("Input action (e.g. Aim) whose press completes this prompt. Empty = none.")]
    public string completeAction;
    [Tooltip("Real-time seconds the action must be held to complete. 0 = a press.")]
    [Min(0f)] public float completeHoldTime;

    [Header("Chaining")]
    [Tooltip("Prompts shown once this one completes, by its action or its duration running out (not when dismissed).")]
    public List<TutorialPromptZone> thenShow = new List<TutorialPromptZone>();
    [Tooltip("Prompts dismissed when this one is triggered, so it does not wait behind them.")]
    public List<TutorialPromptZone> replaces = new List<TutorialPromptZone>();

    private bool awaitingAction;
    private float pressedSince = -1f;
    private Coroutine delayed;

    protected override Color GizmoColor => new Color(1f, 0.85f, 0.2f);
    protected override bool UsesArea => showOnEnter;

    protected override void OnPlayerEntered() => Begin();

    // Start runs before the first physics step, so this queues ahead of prompts entered at spawn.
    private void Start()
    {
        if (showAfterDeath && RoomTravel.ArrivedByRespawn) Show();
    }

    private void OnEnable() => playerController2.BanishFired += OnBanishFired;
    private void OnDisable() => playerController2.BanishFired -= OnBanishFired;

    private void OnBanishFired(playerController2 player)
    {
        if (showOnBanish) Show();
    }

    /// <summary>Shows this prompt unless it has already been shown or dismissed this room load.</summary>
    public void Show()
    {
        if (Fired) return;
        Fired = true;
        Begin();
    }

    /// <summary>Removes this prompt from the screen or queue and stops it showing again this room load.</summary>
    public void Dismiss()
    {
        Fired = true;
        awaitingAction = false;
        if (delayed != null) StopCoroutine(delayed);
        delayed = null;
        TutorialPrompts.Dismiss(this);
    }

    private void Begin()
    {
        foreach (var other in replaces)
            if (other != null && other != this) other.Dismiss();
        if (delay > 0f) delayed = StartCoroutine(QueueAfterDelay());
        else Queue();
    }

    private IEnumerator QueueAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delay);
        delayed = null;
        Queue();
    }

    private void Queue()
    {
        TutorialPrompts.Show(this, text, duration, Complete);
        awaitingAction = !string.IsNullOrEmpty(completeAction);
        pressedSince = -1f;
    }

    private void Update()
    {
        if (!awaitingAction) return;
        var action = FindPlayerAction(completeAction);
        if (action == null || !action.IsPressed())
        {
            pressedSince = -1f;
            return;
        }
        // Unscaled, because aiming slows time down.
        if (pressedSince < 0f) pressedSince = Time.unscaledTime;
        if (Time.unscaledTime - pressedSince < completeHoldTime) return;
        TutorialPrompts.Dismiss(this);
        Complete();
    }

    private void Complete()
    {
        awaitingAction = false;
        foreach (var next in thenShow)
            if (next != null && next != this) next.Show();
    }

    // The player's own action instance, so it reflects what the player actually receives.
    private static InputAction FindPlayerAction(string actionName)
    {
        foreach (var input in PlayerInput.all)
        {
            var action = input.actions != null ? input.actions.FindAction(actionName) : null;
            if (action != null) return action;
        }
        return null;
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = GizmoColor;
        foreach (var next in thenShow)
            if (next != null) Gizmos.DrawLine(LabelPoint, next.LabelPoint);

        string timing = delay > 0f ? $"after {delay:0.#}s, " : string.Empty;
        timing += duration > 0 ? $"{duration:0.#}s" : "until dismissed";
        if (!string.IsNullOrEmpty(completeAction)) timing += $", or {completeAction}";
        var style = new GUIStyle(UnityEditor.EditorStyles.miniLabel) { normal = { textColor = GizmoColor }, wordWrap = true, fixedWidth = 220 };
        string trigger = showOnEnter ? PlaneTag : showOnBanish || showAfterDeath ? "" : "(chained) ";
        if (showOnBanish) trigger += "on banish, ";
        if (showAfterDeath) trigger += "after death, ";
        UnityEditor.Handles.Label(LabelPoint, trigger + timing + ": " + text, style);
    }

    private Vector3 LabelPoint => showOnEnter
        ? transform.TransformPoint(new Vector3(0, areaSize.y + 0.6f))
        : transform.position;
#endif
}
