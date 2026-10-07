using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Full-cell rectangular terrain with per-tile penetration behavior.</summary>
[CreateAssetMenu(fileName = "TerrainTile", menuName = "2D/Tiles/Terrain Tile")]
public sealed class TerrainTile : Tile
{
    [Tooltip("Kill when the player penetrates this tile beyond the allowed tolerance.")]
    public bool killsOnPenetration = true;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        // Collision and lethal queries share the same full-cell footprint.
        // Safe cells get their own merged outline from TerrainCollision.
        tileData.colliderType = killsOnPenetration ? ColliderType.Grid : ColliderType.None;
        tileData.transform = Matrix4x4.identity;
    }
}
