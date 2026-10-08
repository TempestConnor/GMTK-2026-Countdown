using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Edit mode: SafeBox prefab variant, palette entry/spacing, and actual EntityBrush painting.</summary>
public static class ValidateSafeBox
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception("FAILED: " + name + " || " + string.Join(" | ", passed)); passed.Add(name); };
        var box = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Box.prefab");
        var safeBox = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/SafeBox.prefab");
        expect("SafeBox.prefab exists", safeBox != null);
        expect("SafeBox is a prefab variant of Box", PrefabUtility.GetPrefabAssetType(safeBox) == PrefabAssetType.Variant &&
            PrefabUtility.GetCorrespondingObjectFromSource(safeBox) == box);
        var collider = safeBox.GetComponent<BoxCollider2D>();
        var region = safeBox.GetComponent<SafeRegion>();
        expect("2x2 footprint with bottom-left root", safeBox.transform.localScale == Vector3.one &&
            collider.size == new Vector2(2, 2) && collider.offset == new Vector2(1, 1));
        expect("Keeps Pushable, Banishable and a dynamic body", safeBox.GetComponent<Pushable>() != null &&
            safeBox.GetComponent<Banishable>() != null && safeBox.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic);
        expect("Interior is nonlethal (KillsOnPenetration disabled)", !safeBox.GetComponent<KillsOnPenetration>().enabled);
        expect("SafeRegion bakes four movable boundaries with support excluding only player layers",
            region != null && region.Movable && region.Support == collider && region.Boundaries.Length == 4 &&
            collider.excludeLayers.value == SafeRegion.PlayerMask);
        int edges = 0;
        foreach (var edge in safeBox.GetComponentsInChildren<EdgeCollider2D>(true))
            if (region.Owns(edge) && edge.excludeLayers.value == ~SafeRegion.PlayerMask) edges++;
        expect("Four player-only edges on a kinematic child body", edges == 4);
        expect("Ordinary Box is unchanged: no SafeRegion, lethal penetration enabled",
            box.GetComponent<SafeRegion>() == null && box.GetComponent<KillsOnPenetration>().enabled &&
            box.GetComponent<BoxCollider2D>().excludeLayers.value == 0);
        var visual = safeBox.transform.Find("Visual").GetComponent<SpriteRenderer>();
        expect("Distinct frame sprite drawn below the player", visual.sprite != null && visual.sprite.name == "SafeBoxFrame" &&
            visual.sortingOrder < 5 && visual == (SpriteRenderer)new SerializedObject(safeBox.GetComponent<Banishable>()).FindProperty("targetRenderer").objectReferenceValue);

        var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
        var row = palette.transform.Find("Layer1");
        var entry = row.Find("SafeBox");
        expect("Connected palette entry at (18, 3, 0)", entry != null &&
            PrefabUtility.GetCorrespondingObjectFromSource(entry.gameObject) == safeBox && entry.localPosition == new Vector3(18, 3, 0));
        int entries = 0;
        foreach (Transform t in row) if (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == safeBox) entries++;
        expect("Exactly one SafeBox palette entry", entries == 1);
        float previousRight = float.NegativeInfinity;
        foreach (Transform t in row)
        {
            if (t == entry) continue;
            var c = t.GetComponent<BoxCollider2D>();
            float right = c != null ? t.localPosition.x + c.offset.x + c.size.x * .5f : t.localPosition.x;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) right = Mathf.Max(right, row.InverseTransformPoint(r.bounds.max).x);
            previousRight = Mathf.Max(previousRight, right);
        }
        expect("One empty column after the previous entry (prev right edge " + previousRight + ")",
            Mathf.Approximately(entry.localPosition.x, Mathf.Ceil(previousRight - .001f) + 1));

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var target = level.transform.Find("Entities").gameObject;
            var brush = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset"));
            try
            {
                brush.Init(Vector3Int.one);
                brush.SetGameObject(Vector3Int.zero, safeBox);
                int count = target.transform.childCount;
                brush.Paint(level.GetComponent<Grid>(), target, new Vector3Int(30, 2, 0));
                var painted = target.transform.GetChild(count).gameObject;
                expect("EntityBrush paints a connected SafeBox onto Level > Entities at the grid cell",
                    PrefabUtility.GetCorrespondingObjectFromSource(painted) == safeBox && painted.GetComponent<SafeRegion>() != null &&
                    painted.transform.parent == target.transform && (Vector2)painted.transform.position == new Vector2(30, 2));
                expect("Painted instance has no unexpected overrides", PrefabUtility.GetObjectOverrides(painted).Count == 0 &&
                    PrefabUtility.GetAddedGameObjects(painted).Count == 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(brush); }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return string.Join("\n", passed);
    }
}
