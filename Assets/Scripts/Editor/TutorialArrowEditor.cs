using UnityEditor;
using UnityEngine;

// Scene-view handles for placed tutorial arrows: drag the square to move the tip
// (snaps to half cells) and the dot to bow the arc (snaps to quarter cells).
[CustomEditor(typeof(TutorialArrow))]
public sealed class TutorialArrowEditor : Editor
{
    private void OnSceneGUI()
    {
        var arrow = (TutorialArrow)target;
        var t = arrow.transform;
        Handles.color = Color.yellow;

        Vector3 tipWorld = t.TransformPoint(arrow.Evaluate(1f));
        float size = HandleUtility.GetHandleSize(tipWorld) * 0.08f;
        EditorGUI.BeginChangeCheck();
        Vector3 newTip = Handles.FreeMoveHandle(tipWorld, size, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector2 local = (Vector2)t.InverseTransformPoint(newTip) - TutorialArrow.Tail;
            Undo.RecordObject(arrow, "Move Tutorial Arrow Tip");
            arrow.End = new Vector2(Mathf.Round(local.x * 2f) / 2f, Mathf.Round(local.y * 2f) / 2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
        }

        Vector3 mid = t.TransformPoint(arrow.Evaluate(0.5f));
        EditorGUI.BeginChangeCheck();
        Vector3 newMid = Handles.Slider(mid, t.up, size, Handles.DotHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            float straightY = TutorialArrow.Tail.y + arrow.End.y * 0.5f;
            float height = t.InverseTransformPoint(newMid).y - straightY;
            Undo.RecordObject(arrow, "Bow Tutorial Arrow");
            arrow.ArcHeight = Mathf.Round(height * 4f) / 4f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
        }
    }
}
