using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ValidateGroundTileCopy
{
    public static string Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var tile = ScriptableObject.CreateInstance<Tile>();
        var other = ScriptableObject.CreateInstance<Tile>();
        try
        {
            tile.flags = TileFlags.None;
            other.flags = TileFlags.None;
            var grid = new GameObject("Grid", typeof(Grid));
            SceneManager.MoveGameObjectToScene(grid, scene);
            var a = new GameObject("Ground", typeof(Tilemap)).GetComponent<Tilemap>();
            a.transform.SetParent(grid.transform, false);
            var b = new GameObject("GroundB", typeof(Tilemap)).GetComponent<Tilemap>();
            b.transform.SetParent(grid.transform, false);
            var cell = new Vector3Int(-3, 2, 0);
            var extra = new Vector3Int(5, 5, 0);
            var matrix = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
            a.SetTile(cell, tile);
            a.SetColor(cell, Color.red);
            a.SetTransformMatrix(cell, matrix);
            a.SetTileFlags(cell, TileFlags.LockColor | TileFlags.LockTransform);
            b.SetTile(cell, other);
            b.SetTile(extra, other);
            if (GroundTileCopy.CopyPaintedCells(a, b) != 1 || b.GetTile(cell) != tile ||
                b.GetColor(cell) != Color.red || b.GetTransformMatrix(cell) != matrix ||
                b.GetTileFlags(cell) != a.GetTileFlags(cell) || b.GetTile(extra) != other)
                throw new Exception("Copy or destination preservation failed.");
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            if (b.GetTile(cell) != other || b.GetTile(extra) != other || b.GetColor(cell) != Color.white)
                throw new Exception("Undo failed.");
            Undo.PerformRedo();
            if (b.GetTile(cell) != tile || b.GetColor(cell) != Color.red || b.GetTile(extra) != other)
                throw new Exception("Redo failed.");
            Undo.ClearUndo(b);
            return "Passed: tile/color/transform/flags copy, destination-only preservation, Undo and Redo. Working level untouched.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(other);
        }
    }
}
