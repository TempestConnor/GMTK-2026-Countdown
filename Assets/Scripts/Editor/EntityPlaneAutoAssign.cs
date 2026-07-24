using System;
using UnityEditor;
using UnityEngine;

// The Entity Palette has no grid target of its own -- GameObjectBrush just parents
// new entities onto whatever GridLayout the Tile Palette currently has selected
// (Ground, GroundB, Level, ...), which has nothing to do with which plane is active.
// So a freshly painted entity always keeps its prefab's default Plane A regardless of
// the plane hotkeys (PlaneShiftHotkeys), and the parent it lands under isn't a
// reliable signal either. Gating on the active brush's saved cells doesn't work
// either -- the palette has multiple swatches (Door, Switch, Box, ...) and only
// whichever one was last saved to disk shows up in the brush asset's serialized
// cells. Instead this just checks whether the new object's source prefab lives in
// the project's entity prefab folder, which covers every swatch, then retargets it
// (and its children) to the active plane -- via Banishable.SetPlane where present
// (Box), or a direct Plane A -> Plane B layer swap otherwise (Door, Switch) -- without
// touching the source prefab asset.
//
// It also keeps the plane-preview material in sync for entities that don't have a
// Banishable (Door, Switch, ...): those use plain Sprite-Lit-Default/SpriteLitPlaneAHide
// rather than the _Saturation-driven PlaneAware shader, so there's no automatic signal
// tying their appearance to their plane the way Banishable.Apply() provides for the box.
// Without this, every new static entity prefab would need its material swapped to
// SpriteLitPlaneAHide by hand to hide correctly in the preview circle -- this makes that
// happen automatically off the same final layer this script already computes, for both
// painting directions (Plane A default *and* the Plane B retarget above).
[InitializeOnLoad]
static class EntityPlaneAutoAssign
{
    const string EntityPrefabFolder = "Assets/Prefabs/Entities/";
    const string PlaneAHideMaterialPath = "Assets/Materials/SpriteLitPlaneAHide.mat";
    const string PlaneANormalMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

    static EntityPlaneAutoAssign()
    {
        ObjectChangeEvents.changesPublished += OnChangesPublished;
    }

    static void OnChangesPublished(ref ObjectChangeEventStream stream)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool paintingPlaneB = PlaneShiftHotkeys.CurrentPlane == Banishable.Plane.B;

        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
                continue;

            stream.GetCreateGameObjectHierarchyEvent(i, out var evt);

            var root = EditorUtility.EntityIdToObject(evt.entityId) as GameObject;
            if (root == null || EditorUtility.IsPersistent(root))
                continue;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(root) as GameObject;
            if (source == null)
                continue;

            var assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(EntityPrefabFolder, StringComparison.Ordinal))
                continue;

            if (paintingPlaneB)
                AssignPlaneB(root);

            SyncPreviewMaterials(root);
        }
    }

    // Normalizes any plain (non-PlaneAware) sprite's material to match its final layer --
    // SpriteLitPlaneAHide on Plane A (Ground/Entities), stock Sprite-Lit-Default on Plane B
    // (GroundB/EntityB). Only touches renderers already on one of those two materials, so
    // Banishable's PlaneAware/PlaneAwareGray sprites (and anything else deliberate) are untouched.
    static void SyncPreviewMaterials(GameObject root)
    {
        var hideMat = AssetDatabase.LoadAssetAtPath<Material>(PlaneAHideMaterialPath);
        var normalMat = AssetDatabase.LoadAssetAtPath<Material>(PlaneANormalMaterialPath);
        if (hideMat == null || normalMat == null)
            return;

        int groundLayer = LayerMask.NameToLayer("Ground");
        int entitiesLayer = LayerMask.NameToLayer("Entities");

        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sharedMaterial != hideMat && sr.sharedMaterial != normalMat)
                continue;

            bool isPlaneA = sr.gameObject.layer == groundLayer || sr.gameObject.layer == entitiesLayer;
            var wanted = isPlaneA ? hideMat : normalMat;
            if (sr.sharedMaterial == wanted)
                continue;

            Undo.RecordObject(sr, "Sync Plane Preview Material");
            sr.sharedMaterial = wanted;
            EditorUtility.SetDirty(sr);
        }
    }

    static void AssignPlaneB(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            var banishable = go.GetComponent<Banishable>();
            if (banishable != null)
            {
                if (banishable.CurrentPlane == Banishable.Plane.B)
                    continue;

                Undo.RecordObject(banishable, "Assign Plane B");
                banishable.SetPlane(Banishable.Plane.B);
                EditorUtility.SetDirty(banishable);
                continue;
            }

            int planeBLayer = PlaneShiftHotkeys.GetPlaneBLayer(go.layer);
            if (planeBLayer < 0 || go.layer == planeBLayer)
                continue;

            Undo.RecordObject(go, "Assign Plane B");
            go.layer = planeBLayer;
            EditorUtility.SetDirty(go);
        }
    }
}
