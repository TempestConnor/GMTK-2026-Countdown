using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dismisses tutorial prompts when the player enters: the listed prompt zones fade out if
/// showing, are dropped if still queued, and will not show later this room load. With an
/// empty list it fades out whichever prompt is currently on screen.
/// </summary>
public sealed class TutorialDismissZone : TutorialZone
{
    [Tooltip("Prompt zones (in this room) to dismiss. Empty = the prompt currently on screen.")]
    public List<TutorialPromptZone> prompts = new List<TutorialPromptZone>();

    protected override Color GizmoColor => new Color(1f, 0.35f, 0.3f);

    protected override void OnPlayerEntered()
    {
        if (prompts.Count == 0)
        {
            TutorialPrompts.DismissCurrent();
            return;
        }
        foreach (var prompt in prompts)
            if (prompt != null) prompt.Dismiss();
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = GizmoColor;
        foreach (var prompt in prompts)
            if (prompt != null) Gizmos.DrawLine(AreaCenter, prompt.AreaCenter);
    }
}
