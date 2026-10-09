using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the dim "far facility wall" backdrop texture and wires a <see cref="FacilityBackdrop"/>
/// child into the MainCamera prefab, so every room shows it behind both planes. Low contrast and
/// darker than terrain on purpose: it must never read as something solid.
/// </summary>
public static class FacilityBackdropGenerator
{
    private const string MenuPath = "Tools/Level/Generate Facility Backdrop";
    private const string TexturePath = "Assets/Tiles/Sprites/FacilityBackdrop.png";
    private const string CameraPrefabPath = "Assets/Prefabs/MainCamera.prefab";
    private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const string ChildName = "Backdrop";

    // One panel spans 2x2 cells at the terrain's 16 px per unit.
    private const int PixelsPerUnit = 16, Panel = 32;
    private static readonly Color32 Joint = new(17, 20, 27, 255); // Also the camera clear color.
    private static readonly Color32 JointHighlight = new(27, 31, 40, 255);
    private static readonly Color32 Bolt = new(30, 34, 44, 255);
    private static readonly Color32 Face = new(22, 25, 33, 255);

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        WriteTexture();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
        WireCamera(sprite);
        Debug.Log($"Facility backdrop: {TexturePath} wired into {CameraPrefabPath}.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerate() => ConnectedTileAtlas.CanGenerate();

    [InitializeOnLoadMethod]
    private static void GenerateIfMissing()
    {
        if (File.Exists(TexturePath)) return;
        EditorApplication.delayCall += () =>
        {
            if (ConnectedTileAtlas.CanGenerate()) Generate();
        };
    }

    private static Color32 Pixel(int x, int y)
    {
        if (x == 0 || y == 0) return Joint;
        if (x == 1 || y == 1) return JointHighlight;
        if ((x == 4 || x == Panel - 5) && (y == 4 || y == Panel - 5)) return Bolt;
        return Face;
    }

    private static void WriteTexture()
    {
        var texture = new Texture2D(Panel, Panel, TextureFormat.RGBA32, false);
        for (int y = 0; y < Panel; y++)
        for (int x = 0; x < Panel; x++)
            texture.SetPixel(x, y, Pixel(x, y));
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // Required for Draw Mode > Tiled.
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void WireCamera(Sprite sprite)
    {
        var root = PrefabUtility.LoadPrefabContents(CameraPrefabPath);
        try
        {
            var child = root.transform.Find(ChildName);
            if (child == null)
            {
                child = new GameObject(ChildName).transform;
                child.SetParent(root.transform, false);
            }
            if (!child.TryGetComponent(out SpriteRenderer renderer))
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            // First sorting layer (GroundB sits below Default), far below anything painted.
            renderer.sortingLayerID = SortingLayer.layers[0].id;
            renderer.sortingOrder = -1000;
            if (child.GetComponent<FacilityBackdrop>() == null) child.gameObject.AddComponent<FacilityBackdrop>();

            // Anything the backdrop misses (e.g. the frame before it resizes) blends in.
            root.GetComponent<Camera>().backgroundColor = Joint;
            PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
