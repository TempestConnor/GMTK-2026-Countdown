using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Generates the hollow frame atlas for safe terrain and wires it into every nonlethal
/// <see cref="TerrainTile"/>. Adjacent safe tiles then draw as one framed region. Distinct from
/// SafeBox on purpose: same outline, but a plain interior with no stripes or corner
/// brackets, so fixed terrain never reads as a movable object.
/// </summary>
public static class SafeTileFrameGenerator
{
    private const string MenuPath = "Tools/Level/Generate Safe Tile Frames";
    private const string AtlasPath = "Assets/Tiles/Sprites/SafeTileFrame.png";

    // 16 px per cell: the same pixel density as SafeBox's 2x-scaled 32 px sprite.
    private const int Cell = 16, Border = 3, Columns = 8;
    private const int Slot = Cell + 2; // 1 px extruded padding on each side against seams.
    private static readonly Color32 EdgeColor = new(115, 242, 204, 255); // Same as SafeBox outline.
    private static readonly Color32 InteriorColor = SafeSeams.TileInterior; // Seam patches redraw this color.

    // Bits match TerrainTile.NeighborOffsets: N, NE, E, SE, S, SW, W, NW.
    private const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        var masks = Enumerable.Range(0, 256).Select(Canonical).Distinct().OrderBy(m => m).ToArray();
        WriteAtlas(masks);
        var byMask = ImportAtlas(masks);

        int tiles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainTile"))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null || tile.killsOnPenetration) continue;
            var so = new SerializedObject(tile);
            var sprites = so.FindProperty("connectedSprites");
            sprites.arraySize = 256;
            for (int m = 0; m < 256; m++)
                sprites.GetArrayElementAtIndex(m).objectReferenceValue = byMask[Canonical(m)];
            so.FindProperty("m_Sprite").objectReferenceValue = byMask[0]; // Palette preview: a lone tile.
            so.FindProperty("m_Color").colorValue = Color.white; // The atlas carries its own colors.
            so.ApplyModifiedPropertiesWithoutUndo();
            tiles++;
        }
        AssetDatabase.SaveAssets();
        foreach (var terrain in UnityEngine.Object.FindObjectsByType<TerrainCollision>(FindObjectsInactive.Include))
            terrain.GetComponent<UnityEngine.Tilemaps.Tilemap>().RefreshAllTiles();
        Debug.Log($"Safe tile frames: {masks.Length} sprites in {AtlasPath}, applied to {tiles} safe terrain tile(s).");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    /// <summary>A diagonal only matters when both orthogonals beside it are present (47 shapes).</summary>
    public static int Canonical(int mask)
    {
        if ((mask & (N | E)) != (N | E)) mask &= ~NE;
        if ((mask & (S | E)) != (S | E)) mask &= ~SE;
        if ((mask & (S | W)) != (S | W)) mask &= ~SW;
        if ((mask & (N | W)) != (N | W)) mask &= ~NW;
        return mask;
    }

    private static Color32 Pixel(int mask, int x, int y)
    {
        bool has(int bit) => (mask & bit) != 0;
        bool top = y >= Cell - Border, bottom = y < Border, left = x < Border, right = x >= Cell - Border;
        bool edge = (!has(N) && top) || (!has(S) && bottom) || (!has(W) && left) || (!has(E) && right) ||
                    // Inner corners where the region turns: fill the notch the two edges leave.
                    (has(N) && has(E) && !has(NE) && top && right) || (has(S) && has(E) && !has(SE) && bottom && right) ||
                    (has(S) && has(W) && !has(SW) && bottom && left) || (has(N) && has(W) && !has(NW) && top && left);
        return edge ? EdgeColor : InteriorColor;
    }

    private static void WriteAtlas(int[] masks)
    {
        int rows = (masks.Length + Columns - 1) / Columns;
        var texture = new Texture2D(Columns * Slot, rows * Slot, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Color32[texture.width * texture.height]);
        for (int i = 0; i < masks.Length; i++)
        {
            var origin = SlotOrigin(i, rows);
            for (int y = -1; y <= Cell; y++)
            for (int x = -1; x <= Cell; x++)
                texture.SetPixel(origin.x + x, origin.y + y,
                    Pixel(masks[i], Mathf.Clamp(x, 0, Cell - 1), Mathf.Clamp(y, 0, Cell - 1)));
        }
        File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Vector2Int SlotOrigin(int index, int rows) =>
        new(index % Columns * Slot + 1, (rows - 1 - index / Columns) * Slot + 1);

    private static Dictionary<int, Sprite> ImportAtlas(int[] masks)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = Cell;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        // Reuse existing sprite IDs so regenerating keeps tile references stable.
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        int rows = (masks.Length + Columns - 1) / Columns;
        var rects = new SpriteRect[masks.Length];
        for (int i = 0; i < masks.Length; i++)
        {
            string name = SpriteName(masks[i]);
            var origin = SlotOrigin(i, rows);
            rects[i] = new SpriteRect
            {
                name = name,
                rect = new Rect(origin.x, origin.y, Cell, Cell),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f),
                spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate(),
            };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        var byName = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
        return masks.ToDictionary(m => m, m => byName[SpriteName(m)]);
    }

    private static string SpriteName(int mask) => $"SafeTileFrame_{mask:D3}";
}
