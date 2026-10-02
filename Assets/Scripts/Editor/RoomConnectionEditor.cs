using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomConnection))]
public sealed class RoomConnectionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawEndpoint("a", "Endpoint A");
        DrawEndpoint("b", "Endpoint B");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Assign this asset to both zones. Their Zone IDs must match these endpoints. Place each Arrival marker inside its room, clear of terrain.", MessageType.Info);
        if (GUILayout.Button("Include both rooms in build"))
        {
            var connection = (RoomConnection)target;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var endpoint in new[] { connection.a, connection.b })
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(endpoint.scenePath) == null) continue;
                var scene = scenes.Find(s => s.path == endpoint.scenePath);
                if (scene == null) scenes.Add(new EditorBuildSettingsScene(endpoint.scenePath, true));
                else scene.enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        if (GUILayout.Button("Validate paired zones"))
        {
            try { RoomConnectionValidation.Validate((RoomConnection)target); Debug.Log("Room connection is valid.", target); }
            catch (System.Exception error) { Debug.LogError(error.Message, target); }
        }
    }

    private void DrawEndpoint(string propertyName, string label)
    {
        var property = serializedObject.FindProperty(propertyName);
        var path = property.FindPropertyRelative("scenePath");
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue);
        EditorGUI.BeginChangeCheck();
        scene = (SceneAsset)EditorGUILayout.ObjectField("Scene", scene, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck()) path.stringValue = AssetDatabase.GetAssetPath(scene);
        EditorGUILayout.PropertyField(property.FindPropertyRelative("zoneId"));
    }
}

public sealed class RoomConnectionValidation : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;
    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RoomConnection"))
            Validate(AssetDatabase.LoadAssetAtPath<RoomConnection>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    public static void Validate(RoomConnection connection)
    {
        if (connection.a == null || !connection.TryGetDestination(connection.a.scenePath, connection.a.zoneId, out _))
            throw new BuildFailedException(connection.name + ": connection needs two distinct room scenes and nonempty zone IDs.");
        foreach (var endpoint in new[] { connection.a, connection.b })
        {
            bool included = false;
            foreach (var buildScene in EditorBuildSettings.scenes)
                if (buildScene.enabled && buildScene.path == endpoint.scenePath) included = true;
            if (!included || AssetDatabase.LoadAssetAtPath<SceneAsset>(endpoint.scenePath) == null)
                throw new BuildFailedException(connection.name + ": room missing from build: " + endpoint.scenePath);
            // Preview copies validate saved content without changing the designer's open scenes.
            var scene = EditorSceneManager.OpenPreviewScene(endpoint.scenePath);
            try
            {
                int matches = 0;
                int players = 0;
                var ids = new HashSet<string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var player in root.GetComponentsInChildren<PlayerLife>())
                        if (player.isActiveAndEnabled) players++;
                    foreach (var zone in root.GetComponentsInChildren<RoomTransition>())
                    {
                        if (!zone.isActiveAndEnabled) continue;
                        if (string.IsNullOrWhiteSpace(zone.zoneId) || !ids.Add(zone.zoneId))
                            throw new BuildFailedException(endpoint.scenePath + ": empty or duplicate transition Zone ID.");
                        if (zone.zoneId != endpoint.zoneId) continue;
                        if (zone.connection != connection || zone.arrival == null || !zone.GetComponent<BoxCollider2D>().enabled)
                            throw new BuildFailedException(endpoint.scenePath + ": endpoint needs the matching connection, active trigger and arrival marker.");
                        matches++;
                    }
                }
                if (matches != 1 || players != 1)
                    throw new BuildFailedException(endpoint.scenePath + ": expected one matching transition and one active player.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
