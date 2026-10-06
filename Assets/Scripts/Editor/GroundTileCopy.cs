using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Editor-only tile copying. Coordinates are local grid cells, not world positions.</summary>
public static class GroundTileCopy
{
    private const string MenuPath = "Tools/Level/Copy Ground A to Ground B";

    [MenuItem(MenuPath)]
    public static void CopyActiveSceneGround()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            Tilemap source = null, destination = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                {
                    if (map.name == "Ground")
                    {
                        if (source != null) throw new InvalidOperationException("Multiple Ground tilemaps found in the active scene.");
                        source = map;
                    }
                    if (map.name == "GroundB")
                    {
                        if (destination != null) throw new InvalidOperationException("Multiple GroundB tilemaps found in the active scene.");
                        destination = map;
                    }
                }
            if (source == null || destination == null)
                throw new InvalidOperationException("The active scene needs Ground and GroundB tilemaps.");
            int count = CopyPaintedCells(source, destination);
            Debug.Log($"Copied {count} Ground cells onto GroundB in {scene.name}. Review and save the scene; Ctrl+Z undoes the copy.", destination);
        }
        catch (Exception error) { Debug.LogError("Ground tile copy: " + error.Message); }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanCopy() => !EditorApplication.isPlayingOrWillChangePlaymode &&
        !EditorApplication.isCompiling && PrefabStageUtility.GetCurrentPrefabStage() == null;

    /// <summary>Copies occupied cells, preserving destination-only tiles. Returns the copied cell count.</summary>
    public static int CopyPaintedCells(Tilemap source, Tilemap destination)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Copy tiles in Edit mode.");
        if (source == null || destination == null || source == destination)
            throw new ArgumentException("Provide two different tilemaps.");
        if (EditorUtility.IsPersistent(destination) || !destination.gameObject.scene.IsValid())
            throw new ArgumentException("Destination must be a scene tilemap instance.");

        int count = 0;
        foreach (var cell in source.cellBounds.allPositionsWithin)
            if (source.HasTile(cell)) count++;
        if (count == 0) return 0;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Copy Ground A tiles to Ground B");
        Undo.RegisterCompleteObjectUndo(destination, "Copy Ground A tiles to Ground B");
        foreach (var cell in source.cellBounds.allPositionsWithin)
        {
            var tile = source.GetTile(cell);
            if (tile == null) continue;
            destination.SetTile(cell, tile);
            destination.SetTileFlags(cell, TileFlags.None);
            destination.SetColor(cell, source.GetColor(cell));
            destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            destination.SetTileFlags(cell, source.GetTileFlags(cell));
        }
        // Process collision updates without RefreshAllTiles, which can reset cell overrides.
        if (destination.TryGetComponent<TilemapCollider2D>(out var collider))
            collider.ProcessTilemapChanges();
        if (destination.TryGetComponent<CompositeCollider2D>(out var composite))
            composite.GenerateGeometry();
        if (PrefabUtility.IsPartOfPrefabInstance(destination))
            PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
        EditorUtility.SetDirty(destination);
        EditorSceneManager.MarkSceneDirty(destination.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        return count;
    }
}
