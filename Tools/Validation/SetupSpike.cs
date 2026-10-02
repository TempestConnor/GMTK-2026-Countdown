using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

public static class SetupSpike
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        const string playerPath = "Assets/Prefabs/Entities/Player.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var damageable = player.GetComponent<Damageable>() ?? player.AddComponent<Damageable>();
            var life = player.GetComponent<PlayerLife>();
            bool wired = false;
            for (int i = 0; i < damageable.OnKilled.GetPersistentEventCount(); i++)
                wired |= damageable.OnKilled.GetPersistentTarget(i) == life && damageable.OnKilled.GetPersistentMethodName(i) == "Kill";
            if (!wired) UnityEventTools.AddPersistentListener(damageable.OnKilled, life.Kill);
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        const string spritePath = "Assets/Tiles/Sprites/Spike.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath) == null)
        {
            if (!AssetDatabase.CopyAsset("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Triangle.png", spritePath))
                throw new Exception("Unable to copy Unity triangle sprite.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Sprite sprite = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spritePath))
            if (asset is Sprite found) { sprite = found; break; }
        if (sprite == null) throw new Exception("Triangle sprite not imported.");
        const string spikePath = "Assets/Prefabs/Entities/Spike.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(spikePath) == null)
        {
            var root = new GameObject("Spike");
            try
            {
                root.layer = LayerMask.NameToLayer("Entities");
                var pivot = new GameObject("Spike Visual and Hitbox");
                pivot.layer = root.layer;
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
                var visual = new GameObject("Sprite");
                visual.layer = root.layer;
                visual.transform.SetParent(pivot.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(0.95f, 0.25f, 0.25f, 1);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLitPlaneAHide.mat");
                visual.transform.localScale = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1);
                visual.transform.localPosition = -Vector3.Scale(sprite.bounds.center, visual.transform.localScale);
                var hitbox = visual.AddComponent<PolygonCollider2D>();
                // Match the triangle's visible outline using the sprite's generated physics shape.
                var points = new System.Collections.Generic.List<Vector2>();
                int shapes = sprite.GetPhysicsShapeCount();
                if (shapes == 0) throw new Exception("Triangle sprite has no physics outline.");
                hitbox.pathCount = shapes;
                for (int i = 0; i < shapes; i++) { sprite.GetPhysicsShape(i, points); hitbox.SetPath(i, points); }
                hitbox.isTrigger = true;
                visual.AddComponent<KillsOnContact>();
                var orientation = root.AddComponent<CardinalOrientation>();
                var data = new SerializedObject(orientation);
                data.FindProperty("pivot").objectReferenceValue = pivot.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
                orientation.Facing = CardinalOrientation.Direction.Up;
                PrefabUtility.SaveAsPrefabAsset(root, spikePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        const string palettePath = "Assets/Palettes/EntityPalette.prefab";
        var palette = PrefabUtility.LoadPrefabContents(palettePath);
        float nextX = 0;
        try
        {
            var row = palette.transform.Find("Layer1");
            if (row.Find("Spike") == null)
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
                var spike = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spikePath), row);
                spike.transform.localPosition = new Vector3(nextX, 3, 0);
                PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
            }
            else nextX = row.Find("Spike").localPosition.x;
        }
        finally { PrefabUtility.UnloadPrefabContents(palette); }
        AssetDatabase.SaveAssets();
        return "Player Damageable wired to PlayerLife.Kill; Spike prefab and connected palette entry at (" + nextX + ", 3, 0).";
    }
}
