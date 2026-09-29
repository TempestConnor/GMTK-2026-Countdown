using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MigrateTerrainTiles
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play mode first.");
        var prototype = ScriptableObject.CreateInstance<TerrainTile>();
        var script = MonoScript.FromScriptableObject(prototype);
        UnityEngine.Object.DestroyImmediate(prototype);
        foreach (string name in new[] { "Tile_Ground", "Tile_Platform", "Tile_SafeWall" })
        {
            string path = "Assets/Tiles/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (!(tile is TerrainTile))
            {
                var data = new SerializedObject(tile);
                data.FindProperty("m_Script").objectReferenceValue = script;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(tile);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainTile>(path);
            if (terrain == null) throw new Exception("Tile conversion failed: " + path);
            terrain.killsOnPenetration = name != "Tile_SafeWall";
            terrain.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(terrain);
            AssetDatabase.SaveAssetIfDirty(terrain);
        }

        var notes = new List<string>();
        // Migrate scene overrides before deleting their source prefab children.
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool wasDirty = scene.isDirty;
            bool changed = false;
            try
            {
                foreach (var root in scene.GetRootGameObjects()) changed |= MigrateRoot(root);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!wasDirty) EditorSceneManager.SaveScene(scene);
                    else notes.Add(path + " migrated in memory; kept existing unsaved edits unsaved");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        const string prefabPath = "Assets/Prefabs/Level/LevelTemplate.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            MigrateRoot(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        return "Converted all three tiles in place; migrated safe cells; removed obsolete maps and terrain-wide lethal markers. " + string.Join("; ", notes);
    }

    private static bool MigrateRoot(GameObject root)
    {
        bool changed = false;
        foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
        {
            if (map.name != "SafeGround" && map.name != "SafeGroundB") continue;
            string targetName = map.name == "SafeGround" ? "Ground" : "GroundB";
            var sibling = map.transform.parent.Find(targetName);
            if (sibling == null) throw new Exception("Missing " + targetName);
            var target = sibling.GetComponent<Tilemap>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                if (target.HasTile(cell) && target.GetTile(cell) != map.GetTile(cell))
                    throw new Exception("Conflicting safe/ground tiles at " + cell + " in " + map.gameObject.scene.path);
            }
            Undo.RecordObject(target, "Migrate safe terrain");
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                target.SetTile(cell, map.GetTile(cell));
                target.SetTileFlags(cell, TileFlags.None);
                target.SetColor(cell, map.GetColor(cell));
                target.SetTransformMatrix(cell, map.GetTransformMatrix(cell));
                target.SetTileFlags(cell, map.GetTileFlags(cell));
            }
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Undo.DestroyObjectImmediate(map.gameObject);
            changed = true;
        }
        foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
        {
            if (map.name != "Ground" && map.name != "GroundB") continue;
            var hazard = map.GetComponent<KillsOnPenetration>();
            if (hazard == null) continue;
            Undo.DestroyObjectImmediate(hazard);
            changed = true;
        }
        return changed;
    }
}
