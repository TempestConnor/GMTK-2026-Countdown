using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BakeTerrainScenes
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play mode first.");
        var notes = new List<string>();
        const string prefabPath = "Assets/Prefabs/Level/LevelTemplate.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var terrain in prefab.GetComponentsInChildren<TerrainCollision>(true)) terrain.Rebuild();
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool wasDirty = scene.isDirty;
            try
            {
                bool hasTerrain = false;
                foreach (var root in scene.GetRootGameObjects())
                    hasTerrain |= root.GetComponentsInChildren<TerrainCollision>(true).Length > 0;
                if (!hasTerrain) continue;
                TerrainCollisionBaking.Bake(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (wasDirty) notes.Add(path + ": baked in memory; existing unsaved edits preserved");
                else { EditorSceneManager.SaveScene(scene); notes.Add(path + ": baked and saved"); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", notes);
    }
}
