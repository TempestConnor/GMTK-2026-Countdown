using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Shared atlas builder for connected <see cref="TerrainTile"/> sprites: draws one 16 px sprite per
/// distinct neighbor shape (47 of them), imports it as a sliced sprite sheet, and wires the result
/// into a tile's 256-entry neighbor lookup.
/// </summary>
public static class ConnectedTileAtlas
{
    // 16 px per cell: the same pixel density as SafeBox's 2x-scaled 32 px sprite.
    public const int Cell = 16;
    private const int Columns = 8;
    private const int Slot = Cell + 2; // 1 px extruded padding on each side against seams.

    // Bits match TerrainTile.NeighborOffsets: N, NE, E, SE, S, SW, W, NW.
    public const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

    /// <summary>A diagonal only matters when both orthogonals beside it are present (47 shapes).</summary>
    public static int Canonical(int mask)
    {
        if ((mask & (N | E)) != (N | E)) mask &= ~NE;
        if ((mask & (S | E)) != (S | E)) mask &= ~SE;
        if ((mask & (S | W)) != (S | W)) mask &= ~SW;
        if ((mask & (N | W)) != (N | W)) mask &= ~NW;
        return mask;
    }

    /// <summary>Writes and imports the atlas; <paramref name="pixel"/> receives (mask, x, y) with y up.</summary>
    public static Dictionary<int, Sprite> Build(string path, string spritePrefix, Func<int, int, int, Color32> pixel)
    {
        var masks = Enumerable.Range(0, 256).Select(Canonical).Distinct().OrderBy(m => m).ToArray();
        WriteAtlas(path, masks, pixel);
        return ImportAtlas(path, spritePrefix, masks);
    }

    /// <summary>Points every neighbor mask of <paramref name="tile"/> at its canonical sprite.</summary>
    public static void Assign(TerrainTile tile, Dictionary<int, Sprite> byMask)
    {
        var so = new SerializedObject(tile);
        var sprites = so.FindProperty("connectedSprites");
        sprites.arraySize = 256;
        for (int m = 0; m < 256; m++)
            sprites.GetArrayElementAtIndex(m).objectReferenceValue = byMask[Canonical(m)];
        so.FindProperty("m_Sprite").objectReferenceValue = byMask[0]; // Palette preview: a lone tile.
        so.FindProperty("m_Color").colorValue = Color.white; // The atlas carries its own colors.
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Saves tile assets and redraws terrain in the open scenes.</summary>
    public static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        foreach (var terrain in UnityEngine.Object.FindObjectsByType<TerrainCollision>(FindObjectsInactive.Include))
            terrain.GetComponent<UnityEngine.Tilemaps.Tilemap>().RefreshAllTiles();
    }

    public static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    private static void WriteAtlas(string path, int[] masks, Func<int, int, int, Color32> pixel)
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
                    pixel(masks[i], Mathf.Clamp(x, 0, Cell - 1), Mathf.Clamp(y, 0, Cell - 1)));
        }
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Vector2Int SlotOrigin(int index, int rows) =>
        new(index % Columns * Slot + 1, (rows - 1 - index / Columns) * Slot + 1);

    private static Dictionary<int, Sprite> ImportAtlas(string path, string spritePrefix, int[] masks)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
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
            string name = SpriteName(spritePrefix, masks[i]);
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

        var byName = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        return masks.ToDictionary(m => m, m => byName[SpriteName(spritePrefix, m)]);
    }

    private static string SpriteName(string prefix, int mask) => $"{prefix}_{mask:D3}";
}
