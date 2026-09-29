using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ValidateTerrainTiles
{
    public static string Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var passed = new List<string>();
        try
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            if (level.transform.Find("SafeGround") != null || level.transform.Find("SafeGroundB") != null)
                throw new Exception("Obsolete safe maps still exist.");
            var lethal = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_Ground.asset");
            var safe = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_SafeWall.asset");
            if (!lethal.killsOnPenetration || safe.killsOnPenetration) throw new Exception("Wrong tile flags.");
            var player = new GameObject("Terrain Test Player");
            SceneManager.MoveGameObjectToScene(player, scene);
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            var body = player.AddComponent<BoxCollider2D>();
            body.size = Vector2.one * 0.5f;
            var check = player.AddComponent<PlayerPenetrationCheck>();
            var data = new SerializedObject(check);
            data.FindProperty("bodyCollider").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (string suffix in new[] { "", "B" })
            {
                player.layer = LayerMask.NameToLayer("Player" + suffix);
                var map = level.transform.Find("Ground" + suffix).GetComponent<Tilemap>();
                if (map.GetComponentInParent<KillsOnPenetration>() != null) throw new Exception("Terrain-wide lethal marker remains.");
                map.SetTile(new Vector3Int(0, 0, 0), safe);
                map.SetTile(new Vector3Int(1, 0, 0), lethal);
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                map.GetComponent<CompositeCollider2D>().GenerateGeometry();
                Action<string, Vector2, bool> expect = (name, position, expected) =>
                {
                    player.transform.position = position;
                    Physics2D.SyncTransforms();
                    if (check.TryGetLethalOverlap(out _) != expected)
                        throw new Exception(name + " plane " + suffix);
                    passed.Add(name + " plane " + (suffix == "" ? "A" : "B"));
                };
                expect("safe fully enclosed", new Vector2(0.5f, 0.5f), false);
                expect("lethal fully enclosed", new Vector2(1.5f, 0.5f), true);
                expect("mixed seam shallow", new Vector2(0.76f, 0.5f), false);
                expect("mixed seam deep", new Vector2(0.85f, 0.5f), true);
                expect("surface contact", new Vector2(1.5f, 1.25f), false);
                expect("shallow overlap", new Vector2(1.5f, 1.24f), false);
                expect("deep overlap", new Vector2(1.5f, 1.15f), true);
                player.layer = LayerMask.NameToLayer(suffix == "" ? "PlayerB" : "Player");
                expect("opposite plane ignored", new Vector2(1.5f, 0.5f), false);
                map.ClearAllTiles();
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                map.GetComponent<CompositeCollider2D>().GenerateGeometry();
            }
            body.enabled = false;
            var capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.5f, 0.8f);
            data.Update();
            data.FindProperty("bodyCollider").objectReferenceValue = capsule;
            data.ApplyModifiedPropertiesWithoutUndo();
            player.layer = LayerMask.NameToLayer("Player");
            var capsuleMap = level.transform.Find("Ground").GetComponent<Tilemap>();
            capsuleMap.SetTile(Vector3Int.zero, safe);
            capsuleMap.SetTile(Vector3Int.right, lethal);
            capsuleMap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            capsuleMap.GetComponent<CompositeCollider2D>().GenerateGeometry();
            foreach (var sample in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.76f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(1.5f, 0.5f) })
            {
                player.transform.position = sample;
                if (check.TryGetLethalOverlap(out _) != (sample.x > 0.8f))
                    throw new Exception("Capsule check failed at " + sample);
            }
            passed.Add("capsule safe, shallow, mixed, and enclosed checks");
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/TerrainPalette.prefab");
            bool found = false;
            foreach (var map in palette.GetComponentsInChildren<Tilemap>())
                foreach (var cell in map.cellBounds.allPositionsWithin)
                    if (map.GetTile(cell) == safe) found = true;
            if (!found) throw new Exception("Safe tile missing from palette.");
            passed.Add("palette reference preserved");
            return string.Join("\n", passed);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
