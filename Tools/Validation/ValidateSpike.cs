using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateSpike
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, value) => { if (!value) throw new Exception(name); passed.Add(name); };
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Spike.prefab");
            expect("Spike prefab exists", prefab != null);
            var spike = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            expect("Spike cannot be banished", spike.GetComponentInChildren<Banishable>() == null);
            var hazard = spike.GetComponentInChildren<KillsOnContact>();
            var hitbox = spike.GetComponentInChildren<PolygonCollider2D>();
            expect("Spike has triangular trigger and contact hazard", hazard != null && hitbox.isTrigger && hitbox.GetPath(0).Length == 3);
            var orientation = spike.GetComponent<CardinalOrientation>();
            foreach (CardinalOrientation.Direction direction in Enum.GetValues(typeof(CardinalOrientation.Direction)))
            {
                orientation.Facing = direction;
                Physics2D.SyncTransforms();
                var bounds = spike.GetComponentInChildren<SpriteRenderer>().bounds;
                expect(direction + " stays inside its grid cell", Vector3.Distance(bounds.center, new Vector3(0.5f, 0.5f, 0)) < 0.01f && Mathf.Abs(bounds.size.x - 1) < 0.01f && Mathf.Abs(bounds.size.y - 1) < 0.01f);
                var tip = hitbox.transform.TransformPoint(hitbox.GetPath(0)[0]);
                // Unity's triangle shape starts at its tip; cardinal rotation moves it about the cell center.
                var wanted = Quaternion.Euler(0, 0, -90 * (int)direction) * Vector3.up;
                expect(direction + " rotates hitbox with sprite", Vector3.Dot((tip - bounds.center).normalized, wanted) > 0.99f);
            }

            var bodyObject = new GameObject("Non-player damageable");
            SceneManager.MoveGameObjectToScene(bodyObject, scene);
            var rb = bodyObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            bodyObject.layer = LayerMask.NameToLayer("Player");
            var body = bodyObject.AddComponent<BoxCollider2D>();
            var damageable = bodyObject.AddComponent<Damageable>();
            int deaths = 0;
            damageable.OnKilled.AddListener(() => { deaths++; damageable.Kill(); });
            hazard.SendMessage("OnTriggerEnter2D", body);
            hazard.SendMessage("OnTriggerStay2D", body);
            expect("Non-player contact death fires once, including recursive calls", damageable.IsDead && deaths == 1);
            damageable.ResetLife();
            body.isTrigger = true;
            hazard.SendMessage("OnTriggerEnter2D", body);
            expect("Trigger sensors ignored", !damageable.IsDead);
            body.isTrigger = false;
            damageable.enabled = false;
            hazard.SendMessage("OnTriggerEnter2D", body);
            expect("Disabled Damageable ignored", !damageable.IsDead);
            damageable.enabled = true;
            hazard.enabled = false;
            hazard.SendMessage("OnTriggerEnter2D", body);
            expect("Disabled contact hazard ignored", !damageable.IsDead);
            hazard.enabled = true;
            var ordinary = new GameObject("Not damageable");
            SceneManager.MoveGameObjectToScene(ordinary, scene);
            hazard.SendMessage("OnTriggerEnter2D", ordinary.AddComponent<BoxCollider2D>());
            expect("Ordinary object ignored", ordinary != null && !damageable.IsDead);

            var check = bodyObject.AddComponent<DamageablePenetrationCheck>();
            var data = new SerializedObject(check);
            data.FindProperty("bodyCollider").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            var wall = new GameObject("Lethal wall");
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.layer = LayerMask.NameToLayer("Ground");
            wall.AddComponent<BoxCollider2D>();
            wall.AddComponent<KillsOnPenetration>();
            wall.transform.position = new Vector3(0.99f, 0, 0);
            check.CheckNow();
            expect("Shallow penetration safe for non-player", !damageable.IsDead);
            wall.transform.position = new Vector3(0.8f, 0, 0);
            wall.layer = LayerMask.NameToLayer("GroundB");
            check.CheckNow();
            expect("Opposite-plane penetration ignored", !damageable.IsDead);
            wall.layer = LayerMask.NameToLayer("Ground");
            check.CheckNow();
            expect("Deep penetration kills non-player via Damageable", damageable.IsDead && deaths == 2);

            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab");
            var playerDamageable = player.GetComponent<Damageable>();
            expect("Player has Damageable and generic penetration check", playerDamageable != null && player.GetComponent<DamageablePenetrationCheck>() != null);
            bool wired = false;
            for (int i = 0; i < playerDamageable.OnKilled.GetPersistentEventCount(); i++)
                wired |= playerDamageable.OnKilled.GetPersistentTarget(i) == player.GetComponent<PlayerLife>() && playerDamageable.OnKilled.GetPersistentMethodName(i) == "Kill";
            expect("Player death event wired to existing PlayerLife.Kill", wired);
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
            var entry = palette.transform.Find("Layer1/Spike");
            expect("Palette entry is prefab-connected at (14,3,0)", entry != null && entry.localPosition == new Vector3(14, 3, 0) && PrefabUtility.GetCorrespondingObjectFromSource(entry.gameObject) == prefab);
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var target = level.transform.Find("Entities").gameObject;
            var brush = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset"));
            try
            {
                brush.Init(Vector3Int.one);
                brush.SetGameObject(Vector3Int.zero, prefab);
                int count = target.transform.childCount;
                brush.Paint(level.GetComponent<Grid>(), target, new Vector3Int(50, 0, 0));
                expect("EntityBrush paints spike onto Entities", target.transform.childCount == count + 1);
                var painted = target.transform.GetChild(count).gameObject;
                expect("Painted spike retains prefab and hazard", PrefabUtility.GetCorrespondingObjectFromSource(painted) == prefab && painted.GetComponentInChildren<KillsOnContact>() != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(brush); }
            return string.Join("\n", passed);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
