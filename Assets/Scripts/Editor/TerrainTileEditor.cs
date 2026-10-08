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
        EditorGUILayout.HelpBox("Paint onto Ground or GroundB. Lethal tiles are filled solids; safe tiles form hollow regions with solid borders. TerrainCollision generates the safe outlines automatically.", MessageType.Info);
        if (targets.Length == 1 && ((TerrainTile)target).IsConnected)
            EditorGUILayout.HelpBox("Draws as a connected frame with neighboring tiles of the same lethality. Regenerate with Tools > Level > Generate Safe Tile Frames.", MessageType.None);
        if (serializedObject.ApplyModifiedProperties())
        {
            foreach (var terrain in UnityEngine.Object.FindObjectsByType<TerrainCollision>())
            {
                terrain.GetComponent<UnityEngine.Tilemaps.Tilemap>().RefreshAllTiles();
                terrain.Rebuild();
            }
        }
    }
}
