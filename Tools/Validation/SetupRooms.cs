using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SetupRooms
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        const string playerPath = "Assets/Prefabs/Entities/Player.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            if (player.GetComponent<PlayerInputLock>() == null) player.AddComponent<PlayerInputLock>();
            if (player.GetComponent<PlayerActionCancellation>() == null) player.AddComponent<PlayerActionCancellation>();
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        const string path = "Assets/Prefabs/Entities/RoomTransition.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            var root = new GameObject("RoomTransition");
            try
            {
                var zone = root.AddComponent<RoomTransition>();
                // Default layer is shared across both gameplay planes and is not plane-painted.
                var box = root.GetComponent<BoxCollider2D>();
                box.includeLayers = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("PlayerB"));
                box.layerOverridePriority = 10;
                var marker = new GameObject("Arrival");
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = new Vector3(-2, 1, 0);
                zone.arrival = marker.transform;
                var visual = new GameObject("Palette Visual");
                visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiles/Sprites/PlaceholderSquare.png");
                renderer.color = new Color(0.2f, 0.8f, 1f, 0.5f);
                var data = new SerializedObject(zone);
                data.FindProperty("paletteVisual").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                zone.ConfigureArea();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var palette = PrefabUtility.LoadPrefabContents("Assets/Palettes/EntityPalette.prefab");
        float nextX;
        try
        {
            var row = palette.transform.Find("Layer1");
            var existing = row.Find("RoomTransition");
            if (existing == null)
            {
                float right = float.NegativeInfinity;
                foreach (Transform entity in row)
                {
                    var box = entity.GetComponent<BoxCollider2D>();
                    if (box != null) right = Mathf.Max(right, entity.localPosition.x + box.size.x);
                    else foreach (var renderer in entity.GetComponentsInChildren<SpriteRenderer>())
                        right = Mathf.Max(right, row.InverseTransformPoint(renderer.bounds.max).x);
                }
                nextX = float.IsNegativeInfinity(right) ? 0 : Mathf.Ceil(right) + 1;
                var entry = (GameObject)PrefabUtility.InstantiatePrefab(prefab, row);
                entry.transform.localPosition = new Vector3(nextX, 3, 0);
                PrefabUtility.SaveAsPrefabAsset(palette, "Assets/Palettes/EntityPalette.prefab");
            }
            else nextX = existing.localPosition.x;
        }
        finally { PrefabUtility.UnloadPrefabContents(palette); }

        // An isolated pair demonstrates authoring and enables real scene-load validation.
        // Existing level scenes and progression entries are not rewritten.
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Rooms")) AssetDatabase.CreateFolder("Assets/Scenes", "Rooms");
        if (!AssetDatabase.IsValidFolder("Assets/Data/RoomConnections")) AssetDatabase.CreateFolder("Assets/Data", "RoomConnections");
        const string a = "Assets/Scenes/Rooms/ExampleRoom_A.unity";
        const string b = "Assets/Scenes/Rooms/ExampleRoom_B.unity";
        const string connectionPath = "Assets/Data/RoomConnections/ExampleRoomConnection.asset";
        var connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(connectionPath);
        if (connection == null)
        {
            connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.a.scenePath = a; connection.a.zoneId = "east";
            connection.b.scenePath = b; connection.b.zoneId = "west";
            AssetDatabase.CreateAsset(connection, connectionPath);
        }
        CreateRoom(a, true, connection, prefab);
        CreateRoom(b, false, connection, prefab);
        var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string scenePath in new[] { a, b })
        {
            var existing = builds.Find(s => s.path == scenePath);
            if (existing == null) builds.Add(new EditorBuildSettingsScene(scenePath, true));
            else existing.enabled = true;
        }
        EditorBuildSettings.scenes = builds.ToArray();
        AssetDatabase.SaveAssets();
        return "Player lock/cancellation attached; connected RoomTransition palette entry at (" + nextX + ",3,0); paired example rooms created and included in build.";
    }

    private static void CreateRoom(string path, bool first, RoomConnection connection, GameObject prefab)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            var existingScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                RefreshTerrain(existingScene);
                EditorSceneManager.SaveScene(existingScene);
            }
            finally { EditorSceneManager.CloseScene(existingScene, true); }
            return;
        }
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var ground = level.transform.Find("Ground").GetComponent<Tilemap>();
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Tile_SafeWall.asset");
            for (int x = -16; x <= 16; x++) ground.SetTile(new Vector3Int(x, -3, 0), tile);
            var groundB = level.transform.Find("GroundB");
            if (groundB != null)
                for (int x = -16; x <= 16; x++) groundB.GetComponent<Tilemap>().SetTile(new Vector3Int(x, -3, 0), tile);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab"), scene);
            player.transform.position = new Vector3(0, -1, 0);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -20);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 6;
            camera.backgroundColor = first ? new Color(0.08f, 0.14f, 0.2f) : new Color(0.2f, 0.12f, 0.08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var lightObject = new GameObject("Global Light 2D");
            var light = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            var zoneObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.transform.Find("Entities"));
            zoneObject.transform.localPosition = new Vector3(first ? 10 : -11, -2, 0);
            var zone = zoneObject.GetComponent<RoomTransition>();
            zone.connection = connection;
            zone.zoneId = first ? "east" : "west";
            zone.arrival.position = new Vector3(first ? 7 : -7, -1, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone.arrival);
            new GameObject("Level Flow", typeof(LevelCompletion));
            RefreshTerrain(scene);
            EditorSceneManager.SaveScene(scene, path);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RefreshTerrain(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var map in root.GetComponentsInChildren<Tilemap>())
            {
                map.RefreshAllTiles();
                if (map.TryGetComponent<TilemapCollider2D>(out var collider))
                {
                    // Rebuild the empty template's cached collider before merging the painted floor.
                    collider.compositeOperation = Collider2D.CompositeOperation.None;
                    collider.enabled = false;
                    collider.enabled = true;
                    collider.ProcessTilemapChanges();
                    collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                    if (map.TryGetComponent<CompositeCollider2D>(out var composite))
                    {
                        composite.GenerateGeometry();
                        EditorUtility.SetDirty(composite);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(composite);
                    }
                    EditorUtility.SetDirty(collider);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
                EditorUtility.SetDirty(map);
                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            }
    }
}
