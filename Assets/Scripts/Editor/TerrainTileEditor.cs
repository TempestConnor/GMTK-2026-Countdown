using UnityEditor;

[CustomEditor(typeof(TerrainTile))]
[CanEditMultipleObjects]
public sealed class TerrainTileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Sprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Color"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("killsOnPenetration"));
        EditorGUILayout.HelpBox("Full-cell solid terrain. Paint onto Ground or GroundB. Collision remains solid when Kills On Penetration is off.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }
}
