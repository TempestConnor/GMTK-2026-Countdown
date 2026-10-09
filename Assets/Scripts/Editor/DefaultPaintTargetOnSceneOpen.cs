using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Tile Palette picks the first valid Grid child (Background) as its paint target
// after a scene is opened. Retarget it to the Ground tilemap of the current plane
// (Ground for Plane A, GroundB for Plane B) so terrain painting is ready immediately.
[InitializeOnLoad]
static class DefaultPaintTargetOnSceneOpen
{
    static DefaultPaintTargetOnSceneOpen()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // validTargets is refreshed after the scene finishes loading, so wait a tick.
        EditorApplication.delayCall += SetGroundTarget;
    }

    static void SetGroundTarget()
    {
        var targets = GridPaintingState.validTargets;
        if (targets == null || targets.Length == 0) return;

        string groundName = PlaneShiftHotkeys.CurrentPlane == Banishable.Plane.B ? "GroundB" : "Ground";
        GameObject ground = Array.Find(targets, t => t != null && t.name == groundName);
        if (ground != null)
            GridPaintingState.scenePaintTarget = ground;
    }
}
