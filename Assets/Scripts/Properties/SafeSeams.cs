using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Runtime only. Hides drawn borders where a SafeBox shares an open boundary with safe terrain or another
/// SafeBox, mirroring the collision openings SafeBoundarySystem computes, so they read as one space.
/// Seams are world rects uploaded as shader globals (Assets/Shaders/SafeSeams.hlsl): the box frame turns
/// transparent over its striped fill, and safe tiles are redrawn as plain interior. Each rect is tagged with
/// its plane and role, so the other plane and unrelated sprites are never touched.
/// </summary>
public static class SafeSeams
{
    /// <summary>Safe tile interior; SafeTileFrameGenerator draws the tiles with this color.</summary>
    public static readonly Color32 TileInterior = new Color32(89, 191, 166, 36);
    /// <summary>Frame thickness in world units: 3 px at 16 px per unit, for tiles and SafeBox alike.</summary>
    public const float Border = 3f / 16f;
    private const int MaxRects = 32; // Matches SAFE_SEAM_MAX in SafeSeams.hlsl.
    private const float RoleBox = 1, RoleTerrain = 2;
    private const float Tolerance = SafeBoundarySystem.ContactTolerance;
    // Box cuts also clear the corner brackets, which sit one pixel inside the frame and run 4 px.
    private const float BoxDepth = Border + 4f / 16f;
    // Terrain cuts also span the small contact gap a resting box keeps from the grid.
    private const float Reach = Border + Tolerance + .01f;

    private enum Corner { Closed, Open }

    private struct Span
    {
        public float start, end;
        public SafeRegion terrain; // Null when every partner in the span is another SafeBox.
    }

    private static readonly int RectsId = Shader.PropertyToID("_SafeSeamRects");
    private static readonly int InfoId = Shader.PropertyToID("_SafeSeamInfo");
    private static readonly int CountId = Shader.PropertyToID("_SafeSeamCount");
    private static readonly int TileInteriorId = Shader.PropertyToID("_SafeSeamTileInterior");
    private static readonly int RoleId = Shader.PropertyToID("_SafeSeamRole");
    private static readonly int PlaneId = Shader.PropertyToID("_SafeSeamPlane");

    private static readonly Dictionary<SafeRegion, List<(Vector4 rect, Vector4 info)>> seams =
        new Dictionary<SafeRegion, List<(Vector4, Vector4)>>();
    private static readonly List<List<Span>> allSpans = new List<List<Span>>(), terrainSpans = new List<List<Span>>();
    private static readonly List<SafeBoundarySystem.Opening> sorted = new List<SafeBoundarySystem.Opening>();
    private static readonly Vector4[] rects = new Vector4[MaxRects], infos = new Vector4[MaxRects];
    private static MaterialPropertyBlock block;
    private static readonly Dictionary<Transform, Vector3> visualRest = new Dictionary<Transform, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        seams.Clear();
        visualRest.Clear();
        Upload();
    }

    /// <summary>Rebuilds a box's seams from its edges' current openings.</summary>
    public static void Refresh(SafeRegion box)
    {
        if (box == null) return;
        var frame = Frame(box);
        if (frame == null) return; // Only boxes with a layered frame and fill (SafeBox) take part.
        float plane = PlaneOf(box.gameObject.layer);
        SetRole(frame, RoleBox, plane);
        if (!seams.TryGetValue(box, out var list)) seams.Add(box, list = new List<(Vector4, Vector4)>());
        list.Clear();

        var edges = box.Edges;
        // Box cuts follow the frame exactly, including the contact-gap nudge.
        Vector2 shift = CloseContactGap(frame.transform, edges);
        while (allSpans.Count < edges.Count) { allSpans.Add(new List<Span>()); terrainSpans.Add(new List<Span>()); }
        for (int i = 0; i < edges.Count; i++)
        {
            Merge(edges[i], false, allSpans[i]);
            Merge(edges[i], true, terrainSpans[i]);
        }
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            float length = Vector2.Distance(edge.a, edge.b);
            Vector2 dir = (edge.b - edge.a) / length;
            foreach (var span in allSpans[i])
            {
                bool open0 = span.start <= Tolerance && OpensCorner(edges, i, edge.a) == Corner.Open;
                bool open1 = span.end >= length - Tolerance && OpensCorner(edges, i, edge.b) == Corner.Open;
                // Stop one border short of each end so the outline stays continuous where the region turns.
                float from = open0 ? 0 : span.start + Border, to = open1 ? length : span.end - Border;
                if (to - from > .001f)
                    list.Add((Rect(edge.a + dir * from + shift, edge.a + dir * to + shift, -edge.normal * BoxDepth), new Vector4(plane, RoleBox)));
            }
            foreach (var span in terrainSpans[i])
            {
                bool open0 = span.start <= Tolerance && OpensCorner(edges, i, edge.a) == Corner.Open;
                bool open1 = span.end >= length - Tolerance && OpensCorner(edges, i, edge.b) == Corner.Open;
                // An open corner also clears the terrain's inner-corner notch beyond the box.
                float from = open0 ? -Reach : span.start + Border, to = open1 ? length + Reach : span.end - Border;
                if (to - from <= .001f) continue;
                var tiles = span.terrain.transform.parent != null ? span.terrain.transform.parent.GetComponent<TilemapRenderer>() : null;
                if (tiles == null) continue;
                SetRole(tiles, RoleTerrain, PlaneOf(tiles.gameObject.layer));
                list.Add((Rect(edge.a + dir * from, edge.a + dir * to, edge.normal * Reach), new Vector4(plane, RoleTerrain)));
            }
        }
        Upload();
    }

    public static void Release(SafeRegion box)
    {
        var frame = box != null ? Frame(box) : null;
        if (frame != null && visualRest.TryGetValue(frame.transform, out var rest))
        {
            frame.transform.localPosition = rest;
            visualRest.Remove(frame.transform);
        }
        if (box != null && seams.Remove(box)) Upload();
    }

    /// <summary>A resting box keeps a sub-pixel contact gap from what it touches. With both borders gone
    /// that gap would show as a hairline, so the box's visuals (not its collider) close it.</summary>
    private static Vector2 CloseContactGap(Transform visual, List<SafeBoundarySystem.Edge> edges)
    {
        if (!visualRest.TryGetValue(visual, out var rest)) visualRest.Add(visual, rest = visual.localPosition);
        Vector2 shift = Vector2.zero;
        foreach (var edge in edges)
            foreach (var opening in edge.openings)
            {
                // One gap per axis is enough: a box can only rest against one side at a time per axis.
                Vector2 offset = edge.normal * Mathf.Clamp(opening.gap, 0, Tolerance);
                if (Mathf.Abs(edge.normal.x) > .5f && shift.x == 0) shift.x = offset.x;
                if (Mathf.Abs(edge.normal.y) > .5f && shift.y == 0) shift.y = offset.y;
            }
        Vector3 parentScale = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
        var target = rest + new Vector3(shift.x / parentScale.x, shift.y / parentScale.y, 0);
        if (visual.localPosition != target) visual.localPosition = target;
        return shift;
    }

    private static void Upload()
    {
        int count = 0;
        foreach (var list in seams.Values)
            foreach (var (rect, info) in list)
            {
                if (count == MaxRects) break;
                rects[count] = rect;
                infos[count++] = info;
            }
        Shader.SetGlobalVectorArray(RectsId, rects);
        Shader.SetGlobalVectorArray(InfoId, infos);
        Shader.SetGlobalFloat(CountId, count);
        // Globals are passed through as-is; the shader compares against linear texture samples.
        Color interior = TileInterior;
        Shader.SetGlobalVector(TileInteriorId, QualitySettings.activeColorSpace == ColorSpace.Linear ? interior.linear : interior);
    }

    private static SpriteRenderer Frame(SafeRegion box)
    {
        var visual = box.transform.Find("Visual");
        return visual != null && visual.Find("Fill") != null ? visual.GetComponent<SpriteRenderer>() : null;
    }

    private static float PlaneOf(int layer) =>
        layer == LayerMask.NameToLayer("GroundB") || layer == LayerMask.NameToLayer("EntityB") ? 1 : 0;

    private static void SetRole(Renderer renderer, float role, float plane)
    {
        // Get/Set preserves other overrides on the block, such as Banishable's _Saturation.
        block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        if (block.GetFloat(RoleId) == role && block.GetFloat(PlaneId) == plane) return;
        block.SetFloat(RoleId, role);
        block.SetFloat(PlaneId, plane);
        renderer.SetPropertyBlock(block);
    }

    private static void Merge(SafeBoundarySystem.Edge edge, bool terrainOnly, List<Span> output)
    {
        output.Clear();
        sorted.Clear();
        foreach (var opening in edge.openings)
            if (!terrainOnly || IsTerrain(opening.partner)) sorted.Add(opening);
        sorted.Sort((x, y) => x.start.CompareTo(y.start));
        foreach (var opening in sorted)
        {
            var terrain = IsTerrain(opening.partner) ? opening.partner : null;
            int last = output.Count - 1;
            // Per-cell terrain edges arrive as separate openings; join them into one continuous cut.
            if (last >= 0 && opening.start <= output[last].end + Tolerance &&
                (!terrainOnly || output[last].terrain == terrain))
            {
                var span = output[last];
                span.end = Mathf.Max(span.end, opening.end);
                if (span.terrain == null) span.terrain = terrain;
                output[last] = span;
            }
            else output.Add(new Span { start = opening.start, end = opening.end, terrain = terrain });
        }
    }

    private static bool IsTerrain(SafeRegion region) => region != null && !region.Movable;

    /// <summary>A corner opens when the perpendicular edge is also open there and the diagonal is safe
    /// terrain, i.e. the box sits in an inner corner of a region or beside another box on it. Otherwise
    /// the outline turns there.</summary>
    private static Corner OpensCorner(List<SafeBoundarySystem.Edge> edges, int index, Vector2 corner)
    {
        var edge = edges[index];
        for (int j = 0; j < edges.Count; j++)
        {
            var other = edges[j];
            if (j == index || Mathf.Abs(Vector2.Dot(edge.normal, other.normal)) > .01f) continue;
            float otherLength = Vector2.Distance(other.a, other.b);
            float at;
            if ((other.a - corner).sqrMagnitude < 1e-4f) at = 0;
            else if ((other.b - corner).sqrMagnitude < 1e-4f) at = otherLength;
            else continue;
            Vector2 diagonal = corner + (edge.normal + other.normal) * (Border * .5f);
            foreach (var span in allSpans[j])
            {
                if (span.start > at + Tolerance || span.end < at - Tolerance) continue;
                if (InSafeTerrain(span.terrain, diagonal) || InSafeTerrain(TerrainAt(index, corner, edge), diagonal))
                    return Corner.Open;
            }
        }
        return Corner.Closed;
    }

    private static SafeRegion TerrainAt(int index, Vector2 corner, SafeBoundarySystem.Edge edge)
    {
        float at = Vector2.Distance(edge.a, corner);
        foreach (var span in allSpans[index])
            if (span.terrain != null && span.start <= at + Tolerance && span.end >= at - Tolerance) return span.terrain;
        return null;
    }

    private static bool InSafeTerrain(SafeRegion terrain, Vector2 point)
    {
        // Terrain regions live on the baked safe-collision tilemap, which holds exactly the safe cells.
        if (terrain == null || !terrain.TryGetComponent<Tilemap>(out var cells)) return false;
        return cells.HasTile(cells.WorldToCell(point));
    }

    private static Vector4 Rect(Vector2 start, Vector2 end, Vector2 depth)
    {
        Vector2 min = Vector2.Min(Vector2.Min(start, end), Vector2.Min(start + depth, end + depth));
        Vector2 max = Vector2.Max(Vector2.Max(start, end), Vector2.Max(start + depth, end + depth));
        return new Vector4(min.x, min.y, max.x, max.y);
    }
}
