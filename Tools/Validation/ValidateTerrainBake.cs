using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ValidateTerrainBake
{
    public static string Main()
    {
        string path = "Assets/TerrainBakeValidation_" + Guid.NewGuid().ToString("N") + ".prefab";
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject loaded = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var map = root.transform.Find("Ground").GetComponent<Tilemap>();
            var safe = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_SafeWall.asset");
            var lethal = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_Ground.asset");
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++) map.SetTile(new Vector3Int(x, y, 0), x == 3 ? lethal : safe);
            map.GetComponent<TerrainCollision>().Rebuild();
            // Disable generation before saving and reloading. All checks below
            // must succeed using serialized collision alone.
            foreach (var terrain in root.GetComponentsInChildren<TerrainCollision>()) terrain.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            loaded = PrefabUtility.LoadPrefabContents(path);
            var saved = loaded.transform.Find("Ground").GetComponent<Tilemap>();
            var child = saved.transform.GetChild(0).GetComponent<Tilemap>();
            if (child == null || child.GetUsedTilesCount() != 1 || !child.HasTile(Vector3Int.zero) || child.HasTile(new Vector3Int(3, 0, 0)))
                throw new Exception("Saved safe cell data was not preserved.");
            if (!AssetDatabase.Contains(child.GetTile(Vector3Int.zero))) throw new Exception("Collision tile is not a saved asset.");
            Physics2D.SyncTransforms();
            if (child.GetComponent<CompositeCollider2D>().pathCount == 0) throw new Exception("Saved outline geometry missing.");
            if (!saved.GetComponent<CompositeCollider2D>().OverlapPoint(new Vector2(3.5f, 3))) throw new Exception("Saved lethal interior missing.");
            if (child.GetComponent<CompositeCollider2D>().OverlapPoint(new Vector2(1.5f, 3))) throw new Exception("Safe interior is filled.");
            return "Saved/reloaded collision retains safe cells, outline paths, and filled lethal interior with every TerrainCollision component disabled; no regeneration required.";
        }
        finally
        {
            if (loaded != null) PrefabUtility.UnloadPrefabContents(loaded);
            EditorSceneManager.ClosePreviewScene(scene);
            AssetDatabase.DeleteAsset(path);
        }
    }
}
