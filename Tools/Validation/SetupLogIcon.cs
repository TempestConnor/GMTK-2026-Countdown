using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Draws the tutorial log toggle's icon (a sheet of paper with a folded corner and text lines)
// to Assets/Tiles/Sprites/LogIcon.png and puts it on Resources/TutorialPrompts.prefab's
// LogToggle/Icon, replacing the old bullet-list bars. Also places the log panel above the prompt
// band so the two never overlap. Re-running redraws and rewires in place. Edit mode only.
public static class SetupLogIcon
{
    const string TexturePath = "Assets/Tiles/Sprites/LogIcon.png";
    const string PrefabPath = "Assets/Resources/TutorialPrompts.prefab";

    const int Size = 64;
    const int Samples = 4; // per axis, for anti-aliasing
    // Matches the old icon bars.
    static readonly Color Ink = new Color(0.96f, 0.93f, 0.86f, 1f);
    const float PaperAlpha = 0.18f;

    // Sheet, in pixels (y up), with the top-right corner folded over.
    const float Left = 12f, Right = 52f, Bottom = 4f, Top = 60f, Fold = 15f;
    const float Stroke = 4f;
    static readonly float[] LineY = { 37f, 27f, 17f };
    const float LineLeft = 20f, LineRight = 44f;

    // The panel's bottom edge, in canvas pixels: just above the prompt band (Prompt rect top = 340).
    const float PanelBottom = 360f;
    const float PanelMaxHeight = 640f;

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var sprite = DrawIcon();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var icon = (RectTransform)root.transform.Find("LogToggle/Icon");
            for (int i = icon.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(icon.GetChild(i).gameObject);
            var image = icon.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            icon.sizeDelta = new Vector2(36f, 36f);

            var panel = (RectTransform)root.transform.Find("Log");
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, PanelBottom);
            var log = new SerializedObject(root.GetComponent<TutorialLog>());
            log.FindProperty("maxHeight").floatValue = PanelMaxHeight;
            log.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Log icon drawn and wired into " + PrefabPath;
    }

    static Sprite DrawIcon()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                texture.SetPixel(x, y, Shade(x, y));
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
    }

    // Averages sub-pixel samples: a faint paper fill under solid ink strokes.
    static Color Shade(int x, int y)
    {
        float paper = 0f, ink = 0f;
        for (int sy = 0; sy < Samples; sy++)
            for (int sx = 0; sx < Samples; sx++)
            {
                var p = new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples);
                float sheet = Sheet(p);
                if (sheet < 0f) paper++;
                if (IsInk(p, sheet)) ink++;
            }
        paper /= Samples * Samples;
        ink /= Samples * Samples;
        float alpha = ink + paper * PaperAlpha * (1f - ink);
        return alpha <= 0f ? Color.clear : new Color(Ink.r, Ink.g, Ink.b, alpha);
    }

    static bool IsInk(Vector2 p, float sheet)
    {
        if (sheet <= 0f && sheet >= -Stroke) return true; // outline, drawn inside the edge
        if (sheet > 0f) return false;
        float half = Stroke * 0.5f;
        // Fold crease: the turned-down flap's edges.
        float foldX = Right - Fold, foldY = Top - Fold;
        if (p.y >= foldY - half && Mathf.Abs(p.x - foldX) <= half) return true;
        if (p.x >= foldX - half && Mathf.Abs(p.y - foldY) <= half) return true;
        foreach (float lineY in LineY)
            if (Capsule(p, new Vector2(LineLeft, lineY), new Vector2(LineRight, lineY)) <= half) return true;
        return false;
    }

    // Signed distance to the sheet: a rectangle with its top-right corner cut off diagonally.
    static float Sheet(Vector2 p)
    {
        var c = new Vector2((Left + Right) * 0.5f, (Bottom + Top) * 0.5f);
        var h = new Vector2((Right - Left) * 0.5f, (Top - Bottom) * 0.5f);
        var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h;
        float box = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        float cut = (p.x - Right + p.y - Top + Fold) / Mathf.Sqrt(2f);
        return Mathf.Max(box, cut);
    }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
