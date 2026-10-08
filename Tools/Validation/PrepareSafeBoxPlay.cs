using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Edit mode: bakes a temporary safe-terrain fixture (Assets/SafeBoxPhysicsPrototype.prefab) for
/// ValidateSafeBoxPlay. Terrain collision can only be baked in the editor. Delete the asset afterwards with
/// PrepareSafeBoxPlay.Cleanup.</summary>
public static class PrepareSafeBoxPlay
{
    const string Path = "Assets/SafeBoxPhysicsPrototype.prefab";
    public static string Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var map = level.transform.Find("Ground").GetComponent<Tilemap>(); map.ClearAllTiles();
            var safe = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_SafeWall.asset");
            var lethal = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_Ground.asset");
            // Safe block x[0,10) y[-3,0); lethal block beside it for the ordinary-box regression.
            for (int x = 0; x < 10; x++) for (int y = -3; y < 0; y++) map.SetTile(new Vector3Int(x, y, 0), safe);
            for (int x = 12; x < 16; x++) for (int y = -3; y < 0; y++) map.SetTile(new Vector3Int(x, y, 0), lethal);
            map.GetComponent<TerrainCollision>().Rebuild();
            PrefabUtility.SaveAsPrefabAsset(level, Path);
            return "Saved temporary baked fixture " + Path;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static string Cleanup() => AssetDatabase.DeleteAsset(Path) ? "Deleted " + Path : "Nothing to delete";
}
