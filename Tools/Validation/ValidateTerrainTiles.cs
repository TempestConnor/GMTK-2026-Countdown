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
            // Match the Player prefab's movement body, including swept collision.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
                if (map.GetComponent<TerrainCollision>() == null ||
                    map.GetComponent<CompositeCollider2D>().geometryType != CompositeCollider2D.GeometryType.Polygons)
                    throw new Exception("Terrain must generate safe outlines and use filled lethal collision.");
                if (map.GetComponentInParent<KillsOnPenetration>() != null) throw new Exception("Terrain-wide lethal marker remains.");
                map.SetTile(new Vector3Int(0, 0, 0), safe);
                map.SetTile(new Vector3Int(1, 0, 0), lethal);
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                map.GetComponent<CompositeCollider2D>().GenerateGeometry();
                map.GetComponent<TerrainCollision>().Rebuild();
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
                map.GetComponent<TerrainCollision>().Rebuild();
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
            capsuleMap.GetComponent<TerrainCollision>().Rebuild();
            foreach (var sample in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.76f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(1.5f, 0.5f) })
            {
                player.transform.position = sample;
                if (check.TryGetLethalOverlap(out _) != (sample.x > 0.8f))
                    throw new Exception("Capsule check failed at " + sample);
            }
            passed.Add("capsule safe, shallow, mixed, and enclosed checks");
            capsuleMap.ClearAllTiles();
            capsuleMap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            capsuleMap.GetComponent<CompositeCollider2D>().GenerateGeometry();
            capsuleMap.GetComponent<TerrainCollision>().Rebuild();
            rb.freezeRotation = true;
            foreach (string suffix in new[] { "", "B" })
            {
                player.layer = LayerMask.NameToLayer("Player" + suffix);
                var map = level.transform.Find("Ground" + suffix).GetComponent<Tilemap>();
                for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++) map.SetTile(new Vector3Int(x, y, 0), safe);
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                map.GetComponent<CompositeCollider2D>().GenerateGeometry();
                map.GetComponent<TerrainCollision>().Rebuild();
                Action<Vector2, Vector2, int> simulate = (position, velocity, steps) =>
                {
                    rb.position = position;
                    rb.linearVelocity = velocity;
                    rb.WakeUp();
                    Physics2D.SyncTransforms();
                    for (int i = 0; i < steps; i++) scene.GetPhysicsScene2D().Simulate(0.02f);
                };
                simulate(new Vector2(3, 3), Vector2.zero, 20);
                if (Vector2.Distance(rb.position, new Vector2(3, 3)) > 0.01f)
                    throw new Exception("Safe interior pushed the body out.");
                simulate(new Vector2(2, 3), new Vector2(2, 0), 40);
                if (rb.position.x < 3.5f || rb.position.x > 3.7f)
                    throw new Exception("Internal tile seams blocked movement: " + rb.position);
                simulate(new Vector2(3, 3), new Vector2(3, 0), 100);
                if (rb.position.x < 5.6f || rb.position.x > 5.8f)
                    throw new Exception("Inside right border did not contain body: " + rb.position);
                simulate(new Vector2(3, 3), new Vector2(0, -3), 100);
                if (rb.position.y < 0.3f || rb.position.y > 0.5f)
                    throw new Exception("Inside floor did not support body: " + rb.position);
                simulate(new Vector2(3, 3), new Vector2(0, 3), 100);
                if (rb.position.y < 5.5f || rb.position.y > 5.7f)
                    throw new Exception("Inside ceiling did not contain body: " + rb.position);
                simulate(new Vector2(-1, 3), new Vector2(3, 0), 100);
                if (rb.position.x < -0.4f || rb.position.x > -0.2f)
                    throw new Exception("Outside body entered safe terrain: " + rb.position);
                passed.Add("safe interior stays put, crosses cells, collides with inner borders, blocks outside entry: plane " + suffix);
                // A gray pillar interrupts the green region, as in the reported layout.
                for (int y = 0; y < 6; y++) map.SetTile(new Vector3Int(3, y, 0), lethal);
                map.GetComponent<TerrainCollision>().Rebuild();
                rb.position = new Vector2(1.5f, 3);
                Physics2D.SyncTransforms();
                for (int i = 0; i < 100; i++)
                {
                    rb.linearVelocity = new Vector2(5, 0);
                    scene.GetPhysicsScene2D().Simulate(0.02f);
                    if (check.TryGetLethalOverlap(out _))
                        throw new Exception("Walking from safe interior into gray wall caused lethal penetration at step " + i + " position " + rb.position + ".");
                }
                if (rb.position.x < 2.6f || rb.position.x > 2.8f)
                    throw new Exception("Gray wall did not stop movement from safe interior: " + rb.position);
                rb.position = new Vector2(3.5f, 3);
                Physics2D.SyncTransforms();
                if (!check.TryGetLethalOverlap(out _)) throw new Exception("Swap directly inside gray wall was not lethal.");
                passed.Add("gray pillar blocks movement from safe interior without killing; direct penetration stays lethal: plane " + suffix);
                for (int y = 0; y < 6; y++) map.SetTile(new Vector3Int(3, y, 0), safe);
                // Exercise the paint callback and scheduled rebuild, not the explicit API.
                map.GetComponent<TerrainCollision>().SendMessage("Update");
                simulate(new Vector2(2, 3), new Vector2(2, 0), 40);
                if (rb.position.x < 3.5f) throw new Exception("Repainting pillar safe left stale collision.");
                var terrain = map.GetComponent<TerrainCollision>();
                terrain.enabled = false;
                terrain.enabled = true;
                terrain.SendMessage("Update");
                if (map.GetComponentsInChildren<CompositeCollider2D>().Length != 2)
                    throw new Exception("Re-enabling terrain leaked generated colliders.");
                passed.Add("painting refreshes collision; disable/re-enable recreates one safe outline: plane " + suffix);
                map.ClearAllTiles();
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                map.GetComponent<CompositeCollider2D>().GenerateGeometry();
                map.GetComponent<TerrainCollision>().Rebuild();
            }
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
