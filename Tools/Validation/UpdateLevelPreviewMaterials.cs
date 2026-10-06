using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UpdateLevelPreviewMaterials
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Update the level prefab in Edit mode.");

        const string path = "Assets/Prefabs/Level/LevelTemplate.prefab";
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLitPlaneAHide.mat");
        if (material == null) throw new Exception("Preview material is missing.");

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var name in new[] { "Ground", "Background" })
            {
                var child = root.transform.Find(name);
                if (child == null || !child.TryGetComponent<TilemapRenderer>(out var renderer))
                    throw new Exception("Missing TilemapRenderer on " + name);
                renderer.sharedMaterial = material;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new Exception("Prefab save failed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var name in new[] { "Ground", "Background" })
            if (saved.transform.Find(name).GetComponent<TilemapRenderer>().sharedMaterial != material)
                throw new Exception("Saved material verification failed for " + name);
        return "Saved and verified SpriteLitPlaneAHide on LevelTemplate/Ground and LevelTemplate/Background.";
    }
}
