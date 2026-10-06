using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class CopyLevel02Ground
{
    public static string Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Levels/Level_02.unity")
            throw new Exception("Expected Level_02 open in Edit mode.");
        Tilemap source = null, destination = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name == "Ground") source = map;
                if (map.name == "GroundB") destination = map;
            }
        if (source == null || destination == null) throw new Exception("Missing Ground or GroundB.");
        Undo.RegisterCompleteObjectUndo(destination, "Copy Ground A tiles to Ground B");
        int count = 0;
        foreach (var cell in source.cellBounds.allPositionsWithin)
        {
            var tile = source.GetTile(cell);
            if (tile == null) continue;
            destination.SetTile(cell, tile);
            destination.SetTileFlags(cell, TileFlags.None);
            destination.SetColor(cell, source.GetColor(cell));
            destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            destination.SetTileFlags(cell, source.GetTileFlags(cell));
            count++;
        }
        destination.RefreshAllTiles();
        if (destination.TryGetComponent<TilemapCollider2D>(out var collider))
            collider.ProcessTilemapChanges();
        if (destination.TryGetComponent<CompositeCollider2D>(out var composite))
            composite.GenerateGeometry();
        foreach (var cell in source.cellBounds.allPositionsWithin)
            if (source.HasTile(cell) && (source.GetTile(cell) != destination.GetTile(cell) ||
                source.GetColor(cell) != destination.GetColor(cell) ||
                source.GetTransformMatrix(cell) != destination.GetTransformMatrix(cell)))
                throw new Exception("Tile verification failed at " + cell);
        PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
        EditorUtility.SetDirty(destination);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        return "Copied and verified " + count + " Ground cells onto GroundB; saved " + scene.path;
    }
}
