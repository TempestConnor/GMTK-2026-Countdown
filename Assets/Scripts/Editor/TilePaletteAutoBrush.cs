using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;

[InitializeOnLoad]
static class TilePaletteAutoBrush
{
    const string EntityPaletteName = "EntityPalette";
    const string EntityBrushPath = "Assets/Palettes/EntityBrush.asset";

    static GridBrushBase defaultBrush;
    static GridBrushBase entityBrush;

    static TilePaletteAutoBrush()
    {
        GridPaintingState.paletteChanged += OnPaletteChanged;
    }

    static void OnPaletteChanged(GameObject palette)
    {
        if (palette == null)
            return;

        GridBrushBase target = palette.name == EntityPaletteName ? GetEntityBrush() : GetDefaultBrush();
        if (target != null && GridPaintingState.gridBrush != target)
            GridPaintingState.gridBrush = target;
    }

    static GridBrushBase GetDefaultBrush()
    {
        if (defaultBrush == null)
            defaultBrush = ScriptableObject.CreateInstance<GridBrush>();
        return defaultBrush;
    }

    static GridBrushBase GetEntityBrush()
    {
        if (entityBrush == null)
            entityBrush = AssetDatabase.LoadAssetAtPath<GridBrushBase>(EntityBrushPath);
        return entityBrush;
    }
}
