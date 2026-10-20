using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// Draws the mouse button icons used by tutorial prompts ([@Aim], [@Fire], [LEFT_CLICK], ...) and
// builds the TMP sprite asset that <sprite="MouseIcons" name="..."> tags resolve from:
//   Assets/Tiles/Sprites/MouseIcons.png                   (sheet, one cell per button)
//   Assets/Resources/Sprite Assets/MouseIcons.asset       (TMP sprite asset + material)
// Re-running redraws the sheet and rebuilds the asset in place (same GUID). Edit mode only.
public static class SetupMouseIcons
{
    const string TexturePath = "Assets/Tiles/Sprites/MouseIcons.png";
    const string AssetFolder = "Assets/Resources/Sprite Assets";
    const string AssetPath = AssetFolder + "/MouseIcons.asset";

    const int Cell = 64;
    const int Samples = 4; // per axis, for anti-aliasing
    static readonly string[] Names = { "MouseLeft", "MouseRight", "MouseMiddle" };

    // Matches TutorialKey's painted colour and the prompts' key highlight (#FFE08A).
    static readonly Color Outline = new Color(0.91f, 0.86f, 0.77f, 1f);
    static readonly Color Highlight = new Color(1f, 0.878f, 0.541f, 1f);

    // Shape, in cell pixels (y up).
    static readonly Vector2 BodyCenter = new Vector2(32f, 30f);
    static readonly Vector2 BodyHalf = new Vector2(17f, 27f);
    const float BodyRadius = 15f;
    const float ButtonLine = 36f;
    static readonly Vector2 WheelCenter = new Vector2(32f, 45f);
    static readonly Vector2 WheelHalf = new Vector2(3.5f, 6.5f);
    const float Stroke = 4f;
    const float ThinStroke = 3f;

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");

        DrawSheet();
        var texture = ImportSheet();
        var asset = BuildSpriteAsset(texture);
        AssetDatabase.SaveAssets();
        return $"Mouse icons: {asset.spriteCharacterTable.Count} sprites in {AssetPath}";
    }

    static void DrawSheet()
    {
        var texture = new Texture2D(Cell * Names.Length, Cell, TextureFormat.RGBA32, false);
        for (int icon = 0; icon < Names.Length; icon++)
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    texture.SetPixel(icon * Cell + x, y, Shade(icon, x, y));
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
    }

    // Averages sub-pixel samples: highlighted button underneath, outline strokes on top.
    static Color Shade(int icon, int x, int y)
    {
        float fill = 0f, line = 0f;
        for (int sy = 0; sy < Samples; sy++)
            for (int sx = 0; sx < Samples; sx++)
            {
                var p = new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples);
                if (IsHighlighted(icon, p)) fill++;
                if (IsStroke(p)) line++;
            }
        fill /= Samples * Samples;
        line /= Samples * Samples;

        float alpha = line + fill * (1f - line);
        if (alpha <= 0f) return Color.clear;
        var rgb = (Outline * line + Highlight * fill * (1f - line)) / alpha;
        rgb.a = alpha;
        return rgb;
    }

    static bool IsHighlighted(int icon, Vector2 p)
    {
        float wheel = RoundRect(p, WheelCenter, WheelHalf, WheelHalf.x);
        if (icon == 2) return wheel < 0f;
        if (RoundRect(p, BodyCenter, BodyHalf, BodyRadius) >= 0f || p.y < ButtonLine || wheel < ThinStroke * 0.5f) return false;
        return icon == 0 ? p.x < BodyCenter.x : p.x > BodyCenter.x;
    }

    static bool IsStroke(Vector2 p)
    {
        float body = RoundRect(p, BodyCenter, BodyHalf, BodyRadius);
        if (Mathf.Abs(body) <= Stroke * 0.5f) return true;
        if (body >= 0f) return false;
        float wheel = RoundRect(p, WheelCenter, WheelHalf, WheelHalf.x);
        if (Mathf.Abs(wheel) <= ThinStroke * 0.5f) return true;
        if (Mathf.Abs(p.y - ButtonLine) <= ThinStroke * 0.5f) return true;
        return p.y > ButtonLine && wheel > 0f && Mathf.Abs(p.x - BodyCenter.x) <= ThinStroke * 0.5f;
    }

    // Signed distance to a rounded rectangle (negative inside).
    static float RoundRect(Vector2 p, Vector2 center, Vector2 half, float radius)
    {
        var q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * radius;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    static Texture2D ImportSheet()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
#pragma warning disable 618 // spritesheet is still the simplest way to name fixed cells
        var sheet = new SpriteMetaData[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            sheet[i] = new SpriteMetaData { name = Names[i], rect = new Rect(i * Cell, 0, Cell, Cell), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
        importer.spritesheet = sheet;
#pragma warning restore 618
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    static TMP_SpriteAsset BuildSpriteAsset(Texture2D texture)
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Sprite Assets");

        var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        var material = asset.material;
        if (material == null)
        {
            material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "MouseIcons Material" };
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;
        }
        material.SetTexture(ShaderUtilities.ID_MainTex, texture);

        asset.spriteSheet = texture;
        // A new asset has no version, so TMP first "upgrades" it from the legacy list; give it an
        // empty one and let that one-time upgrade run before filling the tables.
        if (asset.spriteInfoList == null) asset.spriteInfoList = new System.Collections.Generic.List<TMP_Sprite>();
        asset.UpdateLookupTables();
        asset.spriteGlyphTable.Clear();
        asset.spriteCharacterTable.Clear();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath);
        for (int i = 0; i < Names.Length; i++)
        {
            var sprite = Array.Find(sprites, s => s is Sprite && s.name == Names[i]) as Sprite;
            // Taller than capitals, dropped below the baseline so it does not reach into the line above.
            var metrics = new GlyphMetrics(Cell, Cell, 0f, Cell * 0.66f, Cell);
            var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(i * Cell, 0, Cell, Cell), 1.6f, 0, sprite);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = Names[i] });
        }
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(asset);
        return asset;
    }
}
