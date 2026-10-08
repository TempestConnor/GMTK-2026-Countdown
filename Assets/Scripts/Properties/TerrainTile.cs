using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Full-cell rectangular terrain with per-tile penetration behavior.</summary>
[CreateAssetMenu(fileName = "TerrainTile", menuName = "2D/Tiles/Terrain Tile")]
public sealed class TerrainTile : Tile
{
    /// <summary>Neighbor bits for <see cref="connectedSprites"/>, clockwise from north.</summary>
    public static readonly Vector3Int[] NeighborOffsets =
    {
        new(0, 1, 0), new(1, 1, 0), new(1, 0, 0), new(1, -1, 0),
        new(0, -1, 0), new(-1, -1, 0), new(-1, 0, 0), new(-1, 1, 0),
    };

    [Tooltip("Kill when the player penetrates this tile beyond the allowed tolerance.")]
    public bool killsOnPenetration = true;

    // Optional 256 sprites indexed by neighbor mask, so adjacent tiles of the same
    // lethality draw as one framed region. Filled by Tools > Level > Generate Safe Tile Frames.
    [SerializeField, HideInInspector] private Sprite[] connectedSprites;

    public bool IsConnected => connectedSprites != null && connectedSprites.Length == 256;

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        // Neighbors pick their frame from this cell, so they redraw on paint and erase.
        tilemap.RefreshTile(position);
        foreach (var offset in NeighborOffsets) tilemap.RefreshTile(position + offset);
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        if (IsConnected)
        {
            int mask = 0;
            for (int i = 0; i < NeighborOffsets.Length; i++)
                if (tilemap.GetTile(position + NeighborOffsets[i]) is TerrainTile other &&
                    other.killsOnPenetration == killsOnPenetration)
                    mask |= 1 << i;
            tileData.sprite = connectedSprites[mask];
        }
        // Collision and lethal queries share the same full-cell footprint.
        // Safe cells get their own merged outline from TerrainCollision.
        tileData.colliderType = killsOnPenetration ? ColliderType.Grid : ColliderType.None;
        tileData.transform = Matrix4x4.identity;
    }
}
