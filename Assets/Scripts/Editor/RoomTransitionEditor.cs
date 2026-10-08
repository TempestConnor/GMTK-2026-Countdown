using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomTransition)), CanEditMultipleObjects]
public sealed class RoomTransitionEditor : Editor
{
    // Zone IDs of closed scenes, read once from a preview copy; cleared whenever any scene is saved.
    private static readonly Dictionary<string, string[]> closedSceneZones = new Dictionary<string, string[]>();

    static RoomTransitionEditor() => EditorSceneManager.sceneSaved += _ => closedSceneZones.Clear();

    private bool pickAnyFolder;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var property = serializedObject.GetIterator();
        for (bool enter = true; property.NextVisible(enter); enter = false)
        {
            if (property.name == "destinationScene") DrawDestination();
            else if (property.name == "destinationZone") continue;
            else using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
        if (targets.Length == 1) DrawLinkStatus((RoomTransition)target);
    }

    private void DrawDestination()
    {
        var scenePath = serializedObject.FindProperty("destinationScene");
        var zone = serializedObject.FindProperty("destinationZone");
        EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);

        EditorGUI.showMixedValue = scenePath.hasMultipleDifferentValues;
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.stringValue);
        if (DrawScenePicker(ref scene))
        {
            scenePath.stringValue = scene != null ? AssetDatabase.GetAssetPath(scene) : "";
            var ids = scene != null ? ZoneIds(scenePath.stringValue) : new string[0];
            zone.stringValue = ids.Length == 1 ? ids[0] : "";
        }
        EditorGUI.showMixedValue = false;

        if (scene == null || scenePath.hasMultipleDifferentValues) return;
        var options = ZoneIds(scenePath.stringValue).ToList();
        if (options.Count == 0)
        {
            EditorGUILayout.PropertyField(zone, new GUIContent("Zone"));
            EditorGUILayout.HelpBox("That scene has no RoomTransition with a Zone ID yet.", MessageType.Warning);
            return;
        }
        if (!options.Contains(zone.stringValue)) options.Insert(0, zone.stringValue);
        var labels = options.Select(id => new GUIContent(string.IsNullOrEmpty(id) ? "(choose)" : id)).ToArray();
        EditorGUI.showMixedValue = zone.hasMultipleDifferentValues;
        int index = EditorGUILayout.Popup(new GUIContent("Zone"), options.IndexOf(zone.stringValue), labels);
        EditorGUI.showMixedValue = false;
        if (index >= 0) zone.stringValue = options[index];
    }

    /// <summary>
    /// Lists the rooms in this scene's folder (one folder per level); "Other folder..." falls back to
    /// the project-wide scene picker for cross-level links. Returns true when the selection changed.
    /// </summary>
    private bool DrawScenePicker(ref SceneAsset scene)
    {
        string ownPath = ((RoomTransition)target).gameObject.scene.path;
        if (string.IsNullOrEmpty(ownPath) || pickAnyFolder)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            scene = (SceneAsset)EditorGUILayout.ObjectField("Scene", scene, typeof(SceneAsset), false);
            bool changed = EditorGUI.EndChangeCheck();
            if (!string.IsNullOrEmpty(ownPath) && GUILayout.Button("Same folder", EditorStyles.miniButton, GUILayout.Width(80)))
                pickAnyFolder = false;
            EditorGUILayout.EndHorizontal();
            if (changed) pickAnyFolder = false;
            return changed;
        }

        string folder = Path.GetDirectoryName(ownPath).Replace('\\', '/');
        var paths = AssetDatabase.FindAssets("t:Scene", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder && p != ownPath)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToList();
        var labels = paths.Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
        string current = scene != null ? AssetDatabase.GetAssetPath(scene) : "";
        if (current != "" && !paths.Contains(current))
        {
            paths.Insert(0, current);
            labels.Insert(0, Path.GetFileNameWithoutExtension(current) + " (" + Path.GetFileName(Path.GetDirectoryName(current)) + ")");
        }
        paths.Insert(0, "");
        labels.Insert(0, "(none)");
        labels.Add("Other folder...");

        int index = EditorGUILayout.Popup("Scene", paths.IndexOf(current), labels.ToArray());
        if (index == labels.Count - 1)
        {
            pickAnyFolder = true;
            return false;
        }
        if (index < 0 || paths[index] == current) return false;
        scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[index]);
        return true;
    }

    private static string[] ZoneIds(string scenePath)
    {
        var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool loaded = open.IsValid() && open.isLoaded;
        if (!loaded && closedSceneZones.TryGetValue(scenePath, out var cached)) return cached;
        var info = RoomLinksBuilder.Collect(scenePath);
        var ids = info == null ? new string[0] : info.zones.Select(z => z.id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (!loaded) closedSceneZones[scenePath] = ids;
        return ids;
    }

    private static void DrawLinkStatus(RoomTransition zone)
    {
        string scenePath = zone.gameObject.scene.path;
        if (string.IsNullOrEmpty(scenePath)) return; // Prefab asset or unsaved scene.
        if (!string.IsNullOrWhiteSpace(zone.destinationScene))
        {
            EditorGUILayout.HelpBox("Two-way: the destination doorway links back here automatically. Save the scene to update the room link registry.", MessageType.None);
            return;
        }
        var links = AssetDatabase.LoadAssetAtPath<RoomLinks>(RoomLinksBuilder.AssetPath);
        if (links != null && links.TryGetDestination(scenePath, zone.zoneId, out var otherScene, out var otherZone))
            EditorGUILayout.HelpBox("Linked from " + System.IO.Path.GetFileNameWithoutExtension(otherScene) + " / " + otherZone + ".", MessageType.Info);
        else
            EditorGUILayout.HelpBox("Not linked. Set a destination here, or on the doorway in the other room.", MessageType.Warning);
    }
}
