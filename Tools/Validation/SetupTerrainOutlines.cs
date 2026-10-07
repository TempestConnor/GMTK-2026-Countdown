using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class SetupTerrainOutlines
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play mode first.");
        const string path = "Assets/Prefabs/Level/LevelTemplate.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name != "Ground" && map.name != "GroundB") continue;
                if (!map.TryGetComponent<CompositeCollider2D>(out var collider)) continue;
                collider.geometryType = CompositeCollider2D.GeometryType.Polygons;
                if (!map.TryGetComponent<TerrainCollision>(out _)) map.gameObject.AddComponent<TerrainCollision>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // Refresh cached tile data in open scenes after changing TerrainTile's
        // collision policy. Do not save any of the user's open scenes.
        foreach (var terrain in UnityEngine.Object.FindObjectsByType<TerrainCollision>())
        {
            terrain.GetComponent<Tilemap>().RefreshAllTiles();
            terrain.Rebuild();
        }
        return "LevelTemplate uses filled lethal terrain with separately generated safe outlines.";
    }
}
