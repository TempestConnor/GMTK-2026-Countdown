using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the listed prompt zones when the player enters: an extra area for prompts that
/// have more than one way in. Each prompt still shows at most once per room load.
/// </summary>
public sealed class TutorialTriggerZone : TutorialZone
{
    [Tooltip("Prompt zones (in this room) to show.")]
    public List<TutorialPromptZone> prompts = new List<TutorialPromptZone>();

    protected override Color GizmoColor => new Color(0.4f, 0.85f, 1f);

    protected override void OnPlayerEntered()
    {
        foreach (var prompt in prompts)
            if (prompt != null) prompt.Show();
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = GizmoColor;
        foreach (var prompt in prompts)
            if (prompt != null) Gizmos.DrawLine(AreaCenter, prompt.AreaCenter);
        var style = new GUIStyle(UnityEditor.EditorStyles.miniLabel) { normal = { textColor = GizmoColor } };
        UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(0, areaSize.y + 0.6f)), PlaneTag + "shows " + prompts.Count + " prompt(s)", style);
    }
#endif
}
