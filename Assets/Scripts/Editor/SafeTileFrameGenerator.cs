using UnityEditor;
using UnityEngine;
using static ConnectedTileAtlas;

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

    private const int Border = 3;
    private static readonly Color32 EdgeColor = new(115, 242, 204, 255); // Same as SafeBox outline.
    private static readonly Color32 InteriorColor = SafeSeams.TileInterior; // Seam patches redraw this color.

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        var byMask = Build(AtlasPath, "SafeTileFrame", Pixel);
        int tiles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainTile"))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null || tile.killsOnPenetration) continue;
            Assign(tile, byMask);
            tiles++;
        }
        SaveAndRefresh();
        Debug.Log($"Safe tile frames: {byMask.Count} sprites in {AtlasPath}, applied to {tiles} safe terrain tile(s).");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerate() => CanGenerate();

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
}
