using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Draws a dotted "wire" from every switch in this door's Door.switches list to the door, both in
// edit mode and in play mode, rebuilding itself whenever the list changes, either end moves, or
// terrain is painted. A wire is activeColor (white) while its switch is in the state the door's
// condition asks for -- pressed for AllPressed, released for AllReleased -- and inactiveColor
// (red) otherwise, so the door is open exactly when all its wires are white.
//
// Routing: wires snake along the 1x1 tile grid with right angles only. Each wire is the cheapest
// 4-way path between the switch's cell and the door's cell (Dijkstra over (cell, heading)
// states), where a step through solid terrain costs terrainStepCost, a step through open air
// costs airStepCost, and every change of heading adds turnCost. Making air pricier than terrain
// tucks wires into walls/floors where it can; the turn cost keeps bends to a few clean corners.
// "Solid terrain" = any tilemap with a TilemapCollider2D on the switch's plane's ground layer
// (Ground or GroundB) in this door's scene, so decorative background tilemaps don't count.
//
// Dots are plain SpriteRenderers (no colliders) that copy their switch visual's layer, sorting
// layer and material. The camera's per-plane culling (playerController2.UpdateCameraVisibility)
// and the plane-preview material therefore treat a wire exactly like its switch: it's hidden
// whenever the player is on the other plane. They live under a HideAndDontSave child, so they
// never show in the Hierarchy or get serialized into scenes, prefabs or prefab overrides.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Door))]
public class DoorWires : MonoBehaviour
{
    private const string ContainerName = "__DoorWires";

    [Header("Look")]
    [Tooltip("Sprite stamped along each wire. Expected to be 1 world unit across at its PPU; dotSize scales it.")]
    [SerializeField] private Sprite dotSprite;

    [Tooltip("Width/height of each dot in world units.")]
    [Min(0.01f)]
    [SerializeField] private float dotSize = 0.15f;

    [Tooltip("Approximate world-unit distance between dot centres. Every corner always gets a dot.")]
    [Min(0.05f)]
    [SerializeField] private float dotSpacing = 0.4f;

    [Tooltip("Wire color while its switch is in the state that helps open the door.")]
    [SerializeField] private Color activeColor = Color.white;

    [Tooltip("Wire color while its switch is in the state that keeps the door closed.")]
    [SerializeField] private Color inactiveColor = Color.red;

    [Tooltip("Sorting order within the switch's sorting layer. Keep above terrain (0) and below the door/switch visuals (4) so wires show over walls but tuck under both ends.")]
    [SerializeField] private int sortingOrder = 3;

    [Header("Routing")]
    [Tooltip("Cost of one tile step through solid terrain.")]
    [Min(0.01f)]
    [SerializeField] private float terrainStepCost = 1f;

    [Tooltip("Cost of one tile step through open air. Higher than terrainStepCost makes wires prefer running inside walls and floors.")]
    [Min(0.01f)]
    [SerializeField] private float airStepCost = 4f;

    [Tooltip("Extra cost of each right-angle bend. Higher = fewer, longer straight runs.")]
    [Min(0f)]
    [SerializeField] private float turnCost = 3f;

    [Tooltip("How many tiles beyond the switch/door bounding box a wire may detour through.")]
    [Min(0)]
    [SerializeField] private int searchMargin = 8;

    private sealed class Wire
    {
        public Switch source;
        public SpriteRenderer sourceRenderer;
        public readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        public Vector3 from;
        public Vector3 to;
        public int terrainLayer = -1;
    }

    // Right, up, left, down: (d + 2) & 3 is the opposite heading.
    private static readonly int[] StepX = { 1, 0, -1, 0 };
    private static readonly int[] StepY = { 0, 1, 0, -1 };

    private Door door;
    private SpriteRenderer doorRenderer;
    private Transform container;
    private readonly List<Wire> wires = new List<Wire>();
    private bool dirty = true;
    private bool routesDirty;

    // Building happens lazily in LateUpdate: parenting new objects under the door is an error
    // while the door itself is mid-activation, which is exactly when OnEnable runs.
    private void OnEnable()
    {
        dirty = true;
        Tilemap.tilemapTileChanged += OnTilemapTileChanged;
#if UNITY_EDITOR
        // Edit-mode LateUpdate only runs after a scene change; make sure wires appear on load.
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    // Only hides the dots: destroying or deactivating children is an error while the door is
    // itself being deactivated/destroyed, and the container is destroyed with the door anyway.
    private void OnDisable()
    {
        Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
        foreach (var wire in wires)
            foreach (var dot in wire.dots)
                if (dot != null) dot.enabled = false;
    }

    // Covers removing just this component, which would otherwise orphan the hidden dots.
    // Deferred, because when the whole door is being destroyed the container already is too.
    private void OnDestroy()
    {
        var target = container != null ? container : transform.Find(ContainerName);
        if (target == null) return;

        if (Application.isPlaying)
        {
            Destroy(target.gameObject);
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (target != null) DestroyImmediate(target.gameObject);
        };
#endif
    }

    private void OnValidate()
    {
        dirty = true;
    }

    private void OnTilemapTileChanged(Tilemap tilemap, Tilemap.SyncTile[] tiles)
    {
        if (tilemap.gameObject.scene == gameObject.scene) routesDirty = true;
    }

    private void LateUpdate()
    {
        if (door == null) door = GetComponent<Door>();
        if (dirty || WiringChanged()) Rebuild();

        foreach (var wire in wires)
            UpdateWire(wire);
        routesDirty = false;
    }

    private bool WiringChanged()
    {
        var switches = door.Switches;
        int matched = 0;
        for (int i = 0; i < switches.Count; i++)
        {
            if (switches[i] == null) continue;
            if (matched >= wires.Count || wires[matched].source != switches[i]) return true;
            matched++;
        }
        return matched != wires.Count;
    }

    private void Rebuild()
    {
        dirty = false;
        wires.Clear();

        if (container == null) container = transform.Find(ContainerName);
        if (container == null)
        {
            var go = new GameObject(ContainerName) { hideFlags = HideFlags.HideAndDontSave };
            container = go.transform;
            container.SetParent(transform, false);
        }

        // Also clears dots left over from before a domain reload, when `wires` was lost.
        for (int i = container.childCount - 1; i >= 0; i--)
            DestroySafe(container.GetChild(i).gameObject);

        doorRenderer = null;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.transform.IsChildOf(container)) continue;
            doorRenderer = sr;
            break;
        }

        foreach (var s in door.Switches)
        {
            if (s == null) continue;
            wires.Add(new Wire
            {
                source = s,
                sourceRenderer = s.GetComponentInChildren<SpriteRenderer>(true)
            });
        }
    }

    private void UpdateWire(Wire wire)
    {
        if (wire.source == null) return; // WiringChanged rebuilds next frame.

        var style = wire.sourceRenderer != null ? wire.sourceRenderer : doorRenderer;
        Vector3 from = Centre(wire.sourceRenderer, wire.source.transform);
        Vector3 to = Centre(doorRenderer, transform);
        int terrainLayer = TerrainLayerFor(style != null ? style.gameObject : wire.source.gameObject);

        if (routesDirty || wire.dots.Count == 0 || from != wire.from || to != wire.to || terrainLayer != wire.terrainLayer)
            Layout(wire, from, to, terrainLayer);

        bool wantsPressed = door.Condition == DoorSwitchCondition.AllPressed;
        Color color = wire.source.isPressed == wantsPressed ? activeColor : inactiveColor;

        foreach (var dot in wire.dots)
        {
            if (dot == null) continue;
            dot.color = color;
            dot.enabled = true;
            if (style == null) continue;
            if (dot.gameObject.layer != style.gameObject.layer) dot.gameObject.layer = style.gameObject.layer;
            if (dot.sortingLayerID != style.sortingLayerID) dot.sortingLayerID = style.sortingLayerID;
            if (dot.sharedMaterial != style.sharedMaterial) dot.sharedMaterial = style.sharedMaterial;
        }
    }

    // Plane B entities sit on GroundB/EntityB, everything else on plane A (Ground/Entities).
    private static int TerrainLayerFor(GameObject go)
    {
        int groundB = LayerMask.NameToLayer("GroundB");
        bool planeB = go.layer == groundB || go.layer == LayerMask.NameToLayer("EntityB");
        return planeB ? groundB : LayerMask.NameToLayer("Ground");
    }

    private void Layout(Wire wire, Vector3 from, Vector3 to, int terrainLayer)
    {
        wire.from = from;
        wire.to = to;
        wire.terrainLayer = terrainLayer;

        var start = new Vector2Int(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
        var goal = new Vector2Int(Mathf.FloorToInt(to.x), Mathf.FloorToInt(to.y));
        var points = BuildPolyline(from, to, Route(start, goal, terrainLayer));

        var positions = new List<Vector3>();
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Vector3 a = points[i], b = points[i + 1];
            float length = Vector3.Distance(a, b);
            if (length < 0.0001f) continue;
            int steps = Mathf.Max(1, Mathf.RoundToInt(length / dotSpacing));
            for (int k = 0; k < steps; k++)
                positions.Add(Vector3.Lerp(a, b, k / (float)steps));
        }
        positions.Add(points[points.Count - 1]);

        while (wire.dots.Count < positions.Count)
            wire.dots.Add(CreateDot());
        while (wire.dots.Count > positions.Count)
        {
            int last = wire.dots.Count - 1;
            if (wire.dots[last] != null) DestroySafe(wire.dots[last].gameObject);
            wire.dots.RemoveAt(last);
        }

        for (int i = 0; i < positions.Count; i++)
            wire.dots[i].transform.position = positions[i];
    }

    // Cheapest right-angle path from start to goal cell, as its corner cells (start and goal
    // included). Dijkstra over (cell, heading) states so a bend can be priced: arriving at the
    // same cell facing a different way is a different state with a different future cost.
    private List<Vector2Int> Route(Vector2Int start, Vector2Int goal, int terrainLayer)
    {
        var corners = new List<Vector2Int> { start };
        if (start == goal) return corners;

        int minX = Mathf.Min(start.x, goal.x) - searchMargin;
        int minY = Mathf.Min(start.y, goal.y) - searchMargin;
        int width = Mathf.Abs(start.x - goal.x) + 1 + 2 * searchMargin;
        int height = Mathf.Abs(start.y - goal.y) + 1 + 2 * searchMargin;

        var tilemaps = TerrainTilemaps(terrainLayer);
        var stepCost = new float[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                stepCost[y * width + x] = IsTerrain(tilemaps, new Vector3(minX + x + 0.5f, minY + y + 0.5f))
                    ? terrainStepCost
                    : airStepCost;

        int startCell = (start.y - minY) * width + (start.x - minX);
        int goalCell = (goal.y - minY) * width + (goal.x - minX);

        var cost = new float[width * height * 4];
        var parent = new int[cost.Length];
        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = float.PositiveInfinity;
            parent[i] = -1;
        }

        var open = new MinHeap();
        for (int d = 0; d < 4; d++)
        {
            cost[startCell * 4 + d] = 0;
            open.Push(0, startCell * 4 + d);
        }

        int found = -1;
        while (open.Count > 0)
        {
            open.Pop(out float c, out int state);
            if (c > cost[state]) continue;

            int cell = state >> 2, heading = state & 3;
            if (cell == goalCell)
            {
                found = state;
                break;
            }

            int cx = cell % width, cy = cell / width;
            for (int d = 0; d < 4; d++)
            {
                if (d == ((heading + 2) & 3)) continue;
                int nx = cx + StepX[d], ny = cy + StepY[d];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                int next = (ny * width + nx) * 4 + d;
                float nc = c + stepCost[ny * width + nx] + (d != heading && cell != startCell ? turnCost : 0);
                if (nc >= cost[next]) continue;
                cost[next] = nc;
                parent[next] = state;
                open.Push(nc, next);
            }
        }

        if (found < 0)
        {
            corners.Add(goal);
            return corners;
        }

        var cells = new List<Vector2Int>();
        for (int s = found; s >= 0; s = parent[s])
        {
            int cell = s >> 2;
            var p = new Vector2Int(minX + cell % width, minY + cell / width);
            if (cells.Count == 0 || cells[cells.Count - 1] != p) cells.Add(p);
        }
        cells.Reverse();

        corners.Clear();
        corners.Add(cells[0]);
        for (int i = 1; i + 1 < cells.Count; i++)
            if (cells[i] - cells[i - 1] != cells[i + 1] - cells[i])
                corners.Add(cells[i]);
        corners.Add(cells[cells.Count - 1]);
        return corners;
    }

    // Turns the corner cells into world points through cell centres, then slides the first and
    // last corner along their segment so the wire meets the switch/door centres at a right angle
    // instead of with a short diagonal stub.
    private static List<Vector3> BuildPolyline(Vector3 from, Vector3 to, List<Vector2Int> corners)
    {
        float z = from.z;
        var points = new List<Vector3> { from };

        if (corners.Count < 2)
        {
            points.Add(new Vector3(to.x, from.y, z));
            points.Add(to);
            return points;
        }

        var centres = new List<Vector3>();
        foreach (var c in corners)
            centres.Add(new Vector3(c.x + 0.5f, c.y + 0.5f, z));

        bool firstHorizontal = corners[0].y == corners[1].y;
        centres[0] = firstHorizontal
            ? new Vector3(from.x, centres[0].y, z)
            : new Vector3(centres[0].x, from.y, z);

        int last = centres.Count - 1;
        bool lastHorizontal = corners[last].y == corners[last - 1].y;
        centres[last] = lastHorizontal
            ? new Vector3(to.x, centres[last].y, z)
            : new Vector3(centres[last].x, to.y, z);

        points.AddRange(centres);
        points.Add(to);
        return points;
    }

    private List<Tilemap> TerrainTilemaps(int layer)
    {
        var result = new List<Tilemap>();
        if (!gameObject.scene.IsValid()) return result;

        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var col in root.GetComponentsInChildren<TilemapCollider2D>())
                if (col.gameObject.layer == layer && col.TryGetComponent(out Tilemap tilemap))
                    result.Add(tilemap);
        return result;
    }

    private static bool IsTerrain(List<Tilemap> tilemaps, Vector3 worldPoint)
    {
        foreach (var tilemap in tilemaps)
            if (tilemap.HasTile(tilemap.WorldToCell(worldPoint)))
                return true;
        return false;
    }

    private SpriteRenderer CreateDot()
    {
        var go = new GameObject("WireDot") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(container, false);
        go.transform.localScale = new Vector3(dotSize, dotSize, 1);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private static Vector3 Centre(SpriteRenderer renderer, Transform fallback)
    {
        return renderer != null ? renderer.bounds.center : fallback.position;
    }

    private static void DestroySafe(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private sealed class MinHeap
    {
        private readonly List<float> keys = new List<float>();
        private readonly List<int> values = new List<int>();

        public int Count => keys.Count;

        public void Push(float key, int value)
        {
            keys.Add(key);
            values.Add(value);
            int i = keys.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0];
            value = values[0];

            int last = keys.Count - 1;
            keys[0] = keys[last];
            values[0] = values[last];
            keys.RemoveAt(last);
            values.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < keys.Count && keys[l] < keys[m]) m = l;
                if (r < keys.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
        }

        private void Swap(int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }
    }
}
