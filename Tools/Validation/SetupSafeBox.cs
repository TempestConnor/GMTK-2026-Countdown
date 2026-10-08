using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Edit mode, idempotent: SafeBox frame and fill sprites, Box prefab variant with baked SafeRegion,
/// and a connected EntityPalette entry at the next free column.</summary>
public static class SetupSafeBox
{
    const string SpritePath = "Assets/Tiles/Sprites/SafeBoxFrame.png";
    const string FillPath = "Assets/Tiles/Sprites/SafeBoxFill.png";
    const int Size = 32, Border = 3, HatchSpacing = 4;
    const string BoxPath = "Assets/Prefabs/Entities/Box.prefab";
    const string SafeBoxPath = "Assets/Prefabs/Entities/SafeBox.prefab";
    const string PalettePath = "Assets/Palettes/EntityPalette.prefab";

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var sprite = WriteSprite(SpritePath, FramePixel);
        var fillSprite = WriteSprite(FillPath, FillPixel);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SafeBoxPath) == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath));
            try { PrefabUtility.SaveAsPrefabAsset(instance, SafeBoxPath); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        var root = PrefabUtility.LoadPrefabContents(SafeBoxPath);
        try
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(root) != AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath))
                throw new Exception("SafeBox must be a variant of Box.");
            root.name = "SafeBox";
            // Exact 2x2 footprint; Box's root carries a stray 0.9948 Y scale.
            root.transform.localScale = Vector3.one;
            // Nonlethal like safe terrain: the hollow interior must never kill on penetration.
            root.GetComponent<KillsOnPenetration>().enabled = false;
            var visual = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.color = Color.white;
            visual.sortingOrder = 4; // Below the player so an occupant draws over the translucent interior.
            visual.maskInteraction = SpriteMaskInteraction.None;
            // SafeSeams clears the frame along open boundaries (shader side); the fill layer beneath shows through.
            var fillTransform = visual.transform.Find("Fill");
            if (fillTransform == null)
            {
                fillTransform = new GameObject("Fill").transform;
                fillTransform.SetParent(visual.transform, false);
            }
            fillTransform.gameObject.layer = visual.gameObject.layer;
            var fill = fillTransform.GetComponent<SpriteRenderer>();
            if (fill == null) fill = fillTransform.gameObject.AddComponent<SpriteRenderer>();
            fill.sprite = fillSprite;
            fill.sharedMaterial = visual.sharedMaterial;
            fill.sortingOrder = 3;
            // An earlier mask-based seam prototype added a SortingGroup; it is no longer needed.
            var group = root.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group != null) UnityEngine.Object.DestroyImmediate(group, true);
            var region = root.GetComponent<SafeRegion>();
            if (region == null) region = root.AddComponent<SafeRegion>();
            var box = root.GetComponent<BoxCollider2D>();
            Vector2 min = box.offset - box.size * .5f, max = box.offset + box.size * .5f;
            region.Bake(box, new[] {
                new SafeRegion.Boundary(min, new Vector2(min.x, max.y), Vector2.left),
                new SafeRegion.Boundary(new Vector2(max.x, min.y), max, Vector2.right),
                new SafeRegion.Boundary(min, new Vector2(max.x, min.y), Vector2.down),
                new SafeRegion.Boundary(new Vector2(min.x, max.y), max, Vector2.up)}, true);
            PrefabUtility.SaveAsPrefabAsset(root, SafeBoxPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var palette = PrefabUtility.LoadPrefabContents(PalettePath);
        float x;
        try
        {
            var row = palette.transform.Find("Layer1");
            var existing = row.Find("SafeBox");
            if (existing == null)
            {
                float right = float.NegativeInfinity;
                foreach (Transform entity in row)
                {
                    var collider = entity.GetComponent<BoxCollider2D>();
                    if (collider != null) right = Mathf.Max(right, entity.localPosition.x + collider.offset.x + collider.size.x * .5f);
                    foreach (var r in entity.GetComponentsInChildren<Renderer>())
                        right = Mathf.Max(right, row.InverseTransformPoint(r.bounds.max).x);
                }
                x = Mathf.Ceil(right - .001f) + 1;
                var entry = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SafeBoxPath), row);
                entry.transform.localPosition = new Vector3(x, 3, 0);
                PrefabUtility.SaveAsPrefabAsset(palette, PalettePath);
            }
            else x = existing.localPosition.x;
        }
        finally { PrefabUtility.UnloadPrefabContents(palette); }
        AssetDatabase.SaveAssets();
        return "SafeBox variant saved; palette entry at (" + x + ", 3, 0).";
    }

    // Border and corner brackets only; masked along open seams.
    static Color FramePixel(int x, int y)
    {
        var frame = new Color(.45f, .95f, .8f, 1);
        bool edge = x < Border || y < Border || x >= Size - Border || y >= Size - Border;
        // Small inner corner brackets keep the hollow readable against busy backgrounds.
        bool bracket = !edge && (x < Border + 4 || x >= Size - Border - 4) && (y < Border + 4 || y >= Size - Border - 4) &&
                       (x == Border || y == Border || x == Size - Border - 1 || y == Size - Border - 1);
        return edge ? frame : bracket ? new Color(frame.r, frame.g, frame.b, .7f) : Color.clear;
    }

    // Full-rect interior, so it also fills where the frame is masked away. Diagonal stripes mark the box
    // as a movable object; safe terrain tiles stay plain.
    static Color FillPixel(int x, int y) => (x - y + Size) % HatchSpacing == 0
        ? new Color(.35f, .75f, .65f, .35f)
        : new Color(.35f, .75f, .65f, .22f);

    static Sprite WriteSprite(string path, Func<int, int, Color> pixel)
    {
        // Always redraw: overwriting the PNG in place keeps its GUID, so prefab references hold.
        bool created = AssetDatabase.LoadAssetAtPath<Sprite>(path) == null;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            texture.SetPixel(x, y, pixel(x, y));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (created)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size; // 1 world unit; the Visual child scales it to 2x2 like Box.
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
