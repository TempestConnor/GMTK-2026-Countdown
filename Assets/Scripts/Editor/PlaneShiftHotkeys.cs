using System;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.Tilemaps;
using UnityEngine;

// Level-editing convenience: 1 shows Plane A (hides Plane B) in the Scene view and
// retargets tile/entity palette painting onto the Plane A layer; 2 does the opposite.
// Plane B GameObjects/Tilemaps follow the project convention of being named after
// their Plane A counterpart with a "B" suffix (Ground/GroundB, Background/BackgroundB).
[InitializeOnLoad]
static class PlaneShiftHotkeys
{
    static readonly string[] PlaneALayers = { "Ground", "Player", "Entities","Background" };
    static readonly string[] PlaneBLayers = { "GroundB", "PlayerB", "EntityB","BackgroundB" };

    // Which plane the 1/2 hotkeys last selected. Entities share a single "Entities"
    // grid target for both planes (unlike Ground/GroundB), so EntityPlaneAutoAssign
    // reads this to know which plane a freshly painted entity should land on.
    internal static Banishable.Plane CurrentPlane { get; private set; } = Banishable.Plane.A;

    static PlaneShiftHotkeys()
    {
        // Ensure the Scene view starts in a consistent state (Plane A visible) on domain reload.
        Tools.visibleLayers = (Tools.visibleLayers | LayerMaskFor(PlaneALayers)) & ~LayerMaskFor(PlaneBLayers);
    }

    [Shortcut("Plane Shift/Show Plane A", KeyCode.Alpha1)]
    static void ShowPlaneA()
    {
        CurrentPlane = Banishable.Plane.A;
        Tools.visibleLayers = (Tools.visibleLayers | LayerMaskFor(PlaneALayers)) & ~LayerMaskFor(PlaneBLayers);
        SetPaintTargetPlane(planeA: true);
        SceneView.RepaintAll();
    }

    [Shortcut("Plane Shift/Show Plane B", KeyCode.Alpha2)]
    static void ShowPlaneB()
    {
        CurrentPlane = Banishable.Plane.B;
        Tools.visibleLayers = (Tools.visibleLayers | LayerMaskFor(PlaneBLayers)) & ~LayerMaskFor(PlaneALayers);
        SetPaintTargetPlane(planeA: false);
        SceneView.RepaintAll();
    }

    // Maps a Plane A layer to its Plane B counterpart by the project's naming
    // convention (Ground -> GroundB, Entities -> EntityB, etc). Returns -1 if
    // layer isn't one of the tracked Plane A layers.
    internal static int GetPlaneBLayer(int layer)
    {
        for (int i = 0; i < PlaneALayers.Length; i++)
        {
            if (LayerMask.NameToLayer(PlaneALayers[i]) == layer)
                return LayerMask.NameToLayer(PlaneBLayers[i]);
        }
        return -1;
    }

    static int LayerMaskFor(string[] names)
    {
        int mask = 0;
        foreach (var name in names)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) mask |= 1 << layer;
        }
        return mask;
    }

    // Retargets the active Tile Palette paint target (used by every palette, since
    // GridPaintingState.scenePaintTarget is a single shared reference) to whichever
    // Grid child in the open scene(s) matches the requested plane's name convention.
    static void SetPaintTargetPlane(bool planeA)
    {
        var targets = GridPaintingState.validTargets;
        if (targets == null || targets.Length == 0) return;

        var current = GridPaintingState.scenePaintTarget;
        GameObject desired = null;

        if (current != null)
        {
            bool currentIsB = current.name.EndsWith("B", StringComparison.Ordinal);
            string pairName = planeA
                ? (currentIsB ? current.name.Substring(0, current.name.Length - 1) : current.name)
                : (currentIsB ? current.name : current.name + "B");

            desired = Array.Find(targets, t => t.name == pairName);
        }

        if (desired == null)
        {
            desired = Array.Find(targets, t => t.name.EndsWith("B", StringComparison.Ordinal) != planeA);
        }

        if (desired != null)
            GridPaintingState.scenePaintTarget = desired;
    }
}
