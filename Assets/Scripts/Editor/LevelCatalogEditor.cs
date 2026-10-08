using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(LevelCatalog))]
public class LevelCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Order this list to set progression. Keep IDs stable. Scene paths must be included in the build.", MessageType.Info);
        if (GUILayout.Button("Add level scene"))
        {
            string path = EditorUtility.OpenFilePanel("Choose level scene", Application.dataPath + "/Scenes", "unity");
            if (string.IsNullOrEmpty(path)) return;
            path = FileUtil.GetProjectRelativePath(path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
            var catalog = (LevelCatalog)target;
            Undo.RecordObject(catalog, "Add level");
            catalog.levels.Add(new LevelCatalog.Level { id = AssetDatabase.AssetPathToGUID(path), title = System.IO.Path.GetFileNameWithoutExtension(path), scenePath = path });
            EditorUtility.SetDirty(catalog);
        }
        if (GUILayout.Button("Include catalog scenes in build"))
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var level in ((LevelCatalog)target).levels)
            {
                var paths = new List<string> { level.scenePath };
                if (level.roomScenePaths != null) paths.AddRange(level.roomScenePaths);
                foreach (string path in paths)
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                    var existing = scenes.Find(s => s.path == path);
                    if (existing != null) existing.enabled = true;
                    else scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

public class LevelCatalogBuildValidation : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        if (catalog == null || catalog.levels.Count == 0) throw new BuildFailedException("Configure the LevelCatalog before building.");
        var scenes = new HashSet<string>();
        foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
        if (!scenes.Contains(GameProgress.MenuScene)) throw new BuildFailedException("MainMenu scene must be enabled in the build.");
        var ids = new HashSet<string>();
        var paths = new HashSet<string>();
        foreach (var level in catalog.levels)
        {
            if (level == null || string.IsNullOrWhiteSpace(level.id) || !ids.Add(level.id) ||
                string.IsNullOrWhiteSpace(level.title) || string.IsNullOrEmpty(level.scenePath) ||
                !paths.Add(level.scenePath) || !scenes.Contains(level.scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(level.scenePath) == null)
                throw new BuildFailedException("LevelCatalog needs unique nonempty IDs, titles, unique valid scenes, and enabled build entries.");
            if (level.roomScenePaths == null) continue;
            foreach (string path in level.roomScenePaths)
                if (string.IsNullOrWhiteSpace(path) || !paths.Add(path) || !scenes.Contains(path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new BuildFailedException("Each room must belong to one level and have a unique valid scene enabled in the build: " + path);
        }
    }
}
