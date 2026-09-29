using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidatePenetration
{
    public static string Main()
    {
        var passed = new List<string>();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var player = new GameObject("Test Player");
            SceneManager.MoveGameObjectToScene(player, scene);
            player.layer = LayerMask.NameToLayer("Player");
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            var body = player.AddComponent<BoxCollider2D>();
            var check = player.AddComponent<PlayerPenetrationCheck>();
            var data = new SerializedObject(check);
            data.FindProperty("bodyCollider").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            var wall = new GameObject("Test Wall");
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.layer = LayerMask.NameToLayer("Ground");
            var solid = wall.AddComponent<BoxCollider2D>();
            var hazard = wall.AddComponent<KillsOnPenetration>();
            Action<string, bool> expect = (name, expected) =>
            {
                bool actual = check.TryGetLethalOverlap(out _);
                if (actual != expected) throw new Exception(name + ": expected " + expected + ", got " + actual);
                passed.Add(name);
            };
            wall.transform.position = new Vector3(2, 0, 0);
            expect("separated", false);
            wall.transform.position = new Vector3(1, 0, 0);
            expect("surface contact", false);
            wall.transform.position = new Vector3(0.99f, 0, 0);
            expect("shallow contact", false);
            wall.transform.position = new Vector3(0.8f, 0, 0);
            expect("deep overlap", true);
            wall.layer = LayerMask.NameToLayer("GroundB");
            expect("opposite plane", false);
            player.layer = LayerMask.NameToLayer("PlayerB");
            expect("both on plane B", true);
            solid.isTrigger = true;
            expect("trigger ignored", false);
            solid.isTrigger = false;
            solid.enabled = false;
            expect("disabled collider", false);
            solid.enabled = true;
            hazard.enabled = false;
            expect("disabled property", false);
            hazard.enabled = true;
            Physics2D.IgnoreCollision(body, solid, true);
            expect("ignored collision", false);
            Physics2D.IgnoreCollision(body, solid, false);
            wall.transform.position = Vector3.zero;
            solid.size = Vector2.one * 8;
            expect("fully enclosed box", true);
            var wallBody = wall.AddComponent<Rigidbody2D>();
            wallBody.bodyType = RigidbodyType2D.Static;
            var composite = wall.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            solid.compositeOperation = Collider2D.CompositeOperation.Merge;
            composite.GenerateGeometry();
            expect("fully enclosed polygon composite", true);
            rb.simulated = false;
            expect("unsimulated player", false);

            var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
            foreach (string name in new[] { "Box", "Door" })
            {
                var entity = palette.transform.Find("Layer1/" + name);
                if (entity == null || entity.GetComponent<KillsOnPenetration>() == null ||
                    PrefabUtility.GetCorrespondingObjectFromSource(entity.gameObject) == null)
                    throw new Exception("Palette inheritance failed: " + name);
                passed.Add(name + " palette inherits property and prefab connection");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab");
            var configured = new SerializedObject(prefab.GetComponent<PlayerPenetrationCheck>());
            if (configured.FindProperty("bodyCollider").objectReferenceValue == null)
                throw new Exception("Player collider not assigned");
            passed.Add("player collider serialized");
            var levelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab");
            var level = (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab, scene);
            var target = level.transform.Find("Entities").gameObject;
            var brushAsset = AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset");
            var brush = UnityEngine.Object.Instantiate(brushAsset);
            try
            {
                int x = 50;
                foreach (string name in new[] { "Box", "Door" })
                {
                    brush.Init(Vector3Int.one);
                    var entityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/" + name + ".prefab");
                    brush.SetGameObject(Vector3Int.zero, entityPrefab);
                    int count = target.transform.childCount;
                    brush.Paint(level.GetComponent<Grid>(), target, new Vector3Int(x, 0, 0));
                    if (target.transform.childCount != count + 1) throw new Exception("Painting failed: " + name);
                    var painted = target.transform.GetChild(count).gameObject;
                    if (painted.GetComponent<KillsOnPenetration>() == null ||
                        PrefabUtility.GetCorrespondingObjectFromSource(painted) != entityPrefab)
                        throw new Exception("Paint lost property or prefab connection: " + name);
                    passed.Add(name + " painted onto Entities with prefab connection");
                    x += 10;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(brush); }
            return string.Join("\n", passed);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
