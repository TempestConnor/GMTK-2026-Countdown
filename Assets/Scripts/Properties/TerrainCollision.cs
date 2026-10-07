using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

/// <summary>Bakes saved safe-terrain collision in the editor. No runtime generation.</summary>
[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap), typeof(CompositeCollider2D), typeof(TilemapCollider2D))]
public sealed class TerrainCollision : MonoBehaviour
{
#if UNITY_EDITOR
    private Tilemap source;
    [SerializeField, HideInInspector] private Tilemap safeMap;
    [SerializeField, HideInInspector] private Tile collisionTile;
    private TilemapCollider2D safeTiles;
    private CompositeCollider2D safeOutline;
    private CompositeCollider2D solid;
    private TilemapCollider2D sourceTiles;
    private bool dirty;

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        source = GetComponent<Tilemap>();
        solid = GetComponent<CompositeCollider2D>();
        sourceTiles = GetComponent<TilemapCollider2D>();
        Tilemap.tilemapTileChanged += TilesChanged;
        UnityEditor.Undo.undoRedoPerformed += MarkDirty;
        dirty = true;
    }

    private void OnDisable()
    {
        Tilemap.tilemapTileChanged -= TilesChanged;
        UnityEditor.Undo.undoRedoPerformed -= MarkDirty;
    }

    private void MarkDirty()
    {
        if (source != null) source.RefreshAllTiles();
        dirty = true;
    }
    private void OnValidate() => dirty = true;

    private void TilesChanged(Tilemap map, Tilemap.SyncTile[] changes)
    {
        if (map == source) dirty = true;
    }

    private void Update() => EnsureCollision();

    private void EnsureCollision()
    {
        if (Application.isPlaying) return;
        if (dirty || safeMap == null) Rebuild();
    }

    /// <summary>Editor-only bake. Save/build hooks call this even for inactive terrain.</summary>
    public void Rebuild()
    {
        if (Application.isPlaying) return;
        if (source == null) source = GetComponent<Tilemap>();
        if (solid == null) solid = GetComponent<CompositeCollider2D>();
        if (sourceTiles == null) sourceTiles = GetComponent<TilemapCollider2D>();
        bool changed = safeMap == null || collisionTile == null;
        if (safeMap == null)
        {
            var generated = new GameObject("Safe terrain collision (generated)");
            SceneManager.MoveGameObjectToScene(generated, gameObject.scene);
            generated.transform.SetParent(transform, false);
            generated.layer = gameObject.layer;
            generated.hideFlags = HideFlags.HideInHierarchy;
            safeMap = generated.AddComponent<Tilemap>();
            generated.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            safeOutline = generated.AddComponent<CompositeCollider2D>();
            safeOutline.geometryType = CompositeCollider2D.GeometryType.Outlines;
            safeOutline.generationType = CompositeCollider2D.GenerationType.Manual;
            safeTiles = generated.AddComponent<TilemapCollider2D>();
            safeTiles.compositeOperation = Collider2D.CompositeOperation.Merge;
        }
        safeTiles = safeMap.GetComponent<TilemapCollider2D>();
        safeOutline = safeMap.GetComponent<CompositeCollider2D>();
        const string tilePath = "Assets/Tiles/Tile_SafeCollision.asset";
        if (collisionTile == null) collisionTile = UnityEditor.AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (collisionTile == null)
        {
            collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.colliderType = Tile.ColliderType.Grid;
            UnityEditor.AssetDatabase.CreateAsset(collisionTile, tilePath);
        }
        changed |= safeMap.gameObject.layer != gameObject.layer ||
            safeOutline.enabled != (solid.enabled && sourceTiles.enabled) ||
            safeOutline.sharedMaterial != solid.sharedMaterial || safeOutline.isTrigger != solid.isTrigger;
        safeMap.gameObject.layer = gameObject.layer;
        safeOutline.enabled = solid.enabled && sourceTiles.enabled;
        safeOutline.sharedMaterial = solid.sharedMaterial;
        safeOutline.isTrigger = solid.isTrigger;
        safeOutline.attachedRigidbody.simulated = solid.attachedRigidbody == null || solid.attachedRigidbody.simulated;

        var cells = new List<Vector3Int>();
        foreach (var cell in source.cellBounds.allPositionsWithin)
            if (source.GetTile(cell) is TerrainTile tile && !tile.killsOnPenetration)
                cells.Add(cell);
        var tiles = new TileBase[cells.Count];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = collisionTile;
        int existingCells = 0;
        foreach (var cell in safeMap.cellBounds.allPositionsWithin)
            if (safeMap.HasTile(cell)) existingCells++;
        changed |= existingCells != cells.Count;
        foreach (var cell in cells) changed |= safeMap.GetTile(cell) != collisionTile;
        if (changed)
        {
            safeMap.ClearAllTiles();
            safeMap.SetTiles(cells.ToArray(), tiles);
        }
        safeTiles.ProcessTilemapChanges();
        safeOutline.GenerateGeometry();
        // Filled lethal cells retain their boundary even beside a safe region.
        sourceTiles.ProcessTilemapChanges();
        solid.GenerateGeometry();
        dirty = false;
        if (!changed) return;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(safeMap);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(safeMap);
        }
        if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene))
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
