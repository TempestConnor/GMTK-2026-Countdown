using UnityEditor;
using UnityEngine;
using static ConnectedTileAtlas;

/// <summary>
/// Generates the auto-tiled art for lethal (solid) terrain and wires it into every lethal
/// <see cref="TerrainTile"/>. Exposed top and bottom faces draw as light floor plates (both are
/// walkable once gravity flips); exposed side faces draw as smooth blue wall panels, so floors
/// and walls read apart without painting them separately. Hazard tiles share the same faces over
/// a yellow/black striped interior, and join seamlessly with plain terrain.
/// </summary>
public static class TerrainFrameGenerator
{
    private const string MenuPath = "Tools/Level/Generate Terrain Frames";
    private const string PlainAtlasPath = "Assets/Tiles/Sprites/TerrainFrame.png";
    private const string HazardAtlasPath = "Assets/Tiles/Sprites/HazardFrame.png";
    private const string HazardTilePath = "Assets/Tiles/Tile_Hazard.asset";
    // Bump after changing the colors or Pixel() so every checkout regenerates on its next script reload.
    private const string StyleVersion = "2";

    // Interior.
    private static readonly Color32 Fill = new(46, 52, 66, 255);
    private static readonly Color32 PlateSeam = new(38, 43, 55, 255);
    private static readonly Color32 Rivet = new(64, 72, 88, 255);
    // Floor/ceiling plate, outermost row first.
    private static readonly Color32 FloorLip = new(222, 226, 232, 255);
    private static readonly Color32 FloorUpper = new(172, 176, 186, 255);
    private static readonly Color32 Floor = new(150, 154, 164, 255);
    private static readonly Color32 FloorGrip = new(110, 114, 124, 255);
    private static readonly Color32 FloorShadow = new(24, 26, 32, 255);
    // Wall panel, outermost column first.
    private static readonly Color32 WallLip = new(104, 128, 176, 255);
    private static readonly Color32 Wall = new(62, 76, 108, 255);
    private static readonly Color32 WallBolt = new(150, 172, 214, 255);
    private static readonly Color32 WallShadow = new(20, 24, 36, 255);
    // Hazard stripes: 4 px bands on the diagonal, so they continue across neighboring cells.
    private static readonly Color32 HazardYellow = new(236, 190, 36, 255);
    private static readonly Color32 HazardBlack = new(30, 30, 32, 255);
    private const int FaceDepth = 5, StripeWidth = 4;

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        var plain = Build(PlainAtlasPath, "TerrainFrame", (m, x, y) => Pixel(m, x, y, false));
        var hazard = Build(HazardAtlasPath, "HazardFrame", (m, x, y) => Pixel(m, x, y, true));
        int tiles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainTile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(path);
            if (tile == null || !tile.killsOnPenetration) continue;
            Assign(tile, path == HazardTilePath ? hazard : plain);
            tiles++;
        }
        foreach (string atlas in new[] { PlainAtlasPath, HazardAtlasPath })
        {
            var importer = AssetImporter.GetAtPath(atlas);
            importer.userData = StyleVersion;
            importer.SaveAndReimport();
        }
        SaveAndRefresh();
        Debug.Log($"Terrain frames: {plain.Count} sprites each in {PlainAtlasPath} and {HazardAtlasPath}, applied to {tiles} lethal terrain tile(s).");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerate() => CanGenerate();

    // Missing or outdated atlases regenerate on the next script reload.
    [InitializeOnLoadMethod]
    private static void GenerateIfOutdated()
    {
        if (IsCurrent(PlainAtlasPath) && IsCurrent(HazardAtlasPath)) return;
        EditorApplication.delayCall += () =>
        {
            if (CanGenerate()) Generate();
        };
    }

    private static bool IsCurrent(string atlas) => AssetImporter.GetAtPath(atlas)?.userData == StyleVersion;

    private static Color32 Pixel(int mask, int x, int y, bool hazard)
    {
        bool has(int bit) => (mask & bit) != 0;
        int top = Cell - 1 - y, bottom = y, left = x, right = Cell - 1 - x; // Distance to each edge.

        // Floors and ceilings win the corners: they are the surfaces the player stands on.
        foreach (var (exposed, d) in new[] { (!has(N), top), (!has(S), bottom) })
        {
            if (!exposed || d >= FaceDepth) continue;
            if (d == 0) return FloorLip;
            if (d == FaceDepth - 1) return FloorShadow;
            if (d == 2 && x % 4 == 1) return FloorGrip;
            return d == 1 ? FloorUpper : Floor;
        }
        foreach (var (exposed, d) in new[] { (!has(W), left), (!has(E), right) })
        {
            if (!exposed || d >= FaceDepth) continue;
            if (d == 0) return WallLip;
            if (d == FaceDepth - 1) return WallShadow;
            if (d == 2 && y is 7 or 8) return WallBolt; // One bolt pair per cell.
            return Wall;
        }
        // Inner corners where the region turns: round off the notch the two faces leave.
        if ((has(N) && has(E) && !has(NE) && top < 2 && right < 2) ||
            (has(N) && has(W) && !has(NW) && top < 2 && left < 2) ||
            (has(S) && has(E) && !has(SE) && bottom < 2 && right < 2) ||
            (has(S) && has(W) && !has(SW) && bottom < 2 && left < 2))
            return Floor;

        if (hazard) return (x + y) / StripeWidth % 2 == 0 ? HazardYellow : HazardBlack;
        if (x == 0 || y == 0) return PlateSeam;
        if ((x == 2 || x == Cell - 3) && (y == 2 || y == Cell - 3)) return Rivet;
        return Fill;
    }
}
