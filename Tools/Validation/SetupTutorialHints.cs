using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Builds the TutorialArrow / TutorialKey prefabs and their EntityPalette entries, then
// paints the Level01 tutorial hints: JumpTutorial (A/D move arrows, SPACE jump arcs) and
// GrabWallJumpTutorial (Grab [F] box push, wall-jump arcs). Idempotent.
public static class SetupTutorialHints
{
    const string ArrowPath = "Assets/Prefabs/Entities/TutorialArrow.prefab";
    const string KeyPath = "Assets/Prefabs/Entities/TutorialKey.prefab";
    const string PalettePath = "Assets/Palettes/EntityPalette.prefab";
    const string ActionsPath = "Assets/Settings/playerActions.inputactions";
    const string JumpScenePath = "Assets/Scenes/Level01/Level01_JumpTutorial.unity";
    const string GrabWallJumpScenePath = "Assets/Scenes/Level01/Level01_GrabWallJumpTutorial.unity";
    const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const int SortingOrder = 1; // above Background (-1) and Ground (0), below the player (5)

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath) ?? throw new Exception("Sprite-Unlit-Default missing.");
        var move = ActionRef("Player/Move");
        var jump = ActionRef("Player/Jump");
        var interact = ActionRef("Player/Interact");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath) == null)
        {
            var root = new GameObject("TutorialArrow");
            try
            {
                var arrow = root.AddComponent<TutorialArrow>();
                var data = new SerializedObject(arrow);
                data.FindProperty("shaft").objectReferenceValue = Line(root, "Shaft", unlit);
                data.FindProperty("head").objectReferenceValue = Line(root, "Head", unlit);
                data.ApplyModifiedPropertiesWithoutUndo();
                arrow.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(KeyPath) == null)
        {
            var root = new GameObject("TutorialKey");
            try
            {
                var key = root.AddComponent<TutorialKey>();
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(root.transform, false);
                var label = labelObject.AddComponent<TextMeshPro>();
                label.font = TMP_Settings.defaultFontAsset;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.text = "Ag";
                label.fontSize = 4f;
                // Scale the font so a line of text is ~0.55 cells tall inside the 1-cell cap.
                label.fontSize = 4f * 0.55f / label.GetPreferredValues("Ag").y;
                label.sortingOrder = SortingOrder + 1;
                var data = new SerializedObject(key);
                data.FindProperty("outline").objectReferenceValue = Line(root, "Outline", unlit);
                data.FindProperty("label").objectReferenceValue = label;
                data.FindProperty("action").objectReferenceValue = move;
                data.FindProperty("compositePart").stringValue = "left";
                data.ApplyModifiedPropertiesWithoutUndo();
                key.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, KeyPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        var arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
        var keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPath);

        // Palette: SafeBox ends at x=20, so the arrow (3 wide by default) starts at 21 and the
        // 1-wide key at 25, each after one empty column. Next free start: x=27.
        var palette = PrefabUtility.LoadPrefabContents(PalettePath);
        try
        {
            var row = palette.transform.Find("Layer1");
            AddEntry(row, arrowPrefab, 21);
            AddEntry(row, keyPrefab, 25);
            PrefabUtility.SaveAsPrefabAsset(palette, PalettePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(palette); }

        // Hints in Entities-local cell coordinates.
        EditScene(JumpScenePath, entities =>
        {
            // Starting room: A / D above arrows pointing left / right.
            PlaceKey(entities, keyPrefab, "Hint Move Left Key", new Vector2(-30, -1), move, "left");
            PlaceKey(entities, keyPrefab, "Hint Move Right Key", new Vector2(-26, -1), move, "right");
            PlaceArrow(entities, arrowPrefab, "Hint Move Left Arrow", new Vector2(-29, -3), new Vector2(-3, 0), 0);
            PlaceArrow(entities, arrowPrefab, "Hint Move Right Arrow", new Vector2(-27, -3), new Vector2(3, 0), 0);
            // Pit and step: SPACE above two jump arcs (out of the pit, then onto the block).
            PlaceKey(entities, keyPrefab, "Hint Jump Key", new Vector2(-2, 0), jump, "");
            PlaceArrow(entities, arrowPrefab, "Hint Jump Arc Pit", new Vector2(-4, -7), new Vector2(4, 2), 2f);
            PlaceArrow(entities, arrowPrefab, "Hint Jump Arc Step", new Vector2(1, -4), new Vector2(5, 1), 1.75f);
        });
        EditScene(GrabWallJumpScenePath, entities =>
        {
            // Box at (-11,-6): "Grab [F]" above it, arrow pushing it right toward the block at x=-3.
            PlaceKey(entities, keyPrefab, "Hint Grab Word", new Vector2(-11, -2), null, "", "Grab", false);
            PlaceKey(entities, keyPrefab, "Hint Grab Key", new Vector2(-9, -2), interact, "");
            PlaceArrow(entities, arrowPrefab, "Hint Grab Push Arrow", new Vector2(-9, -6), new Vector2(4.5f, 0), 0);
            // Wall jumps between the pillar (x 6..8) and the right ledge face (x=11), then onto the ledge.
            PlaceArrow(entities, arrowPrefab, "Hint Wall Jump Arc 1", new Vector2(8, -6), new Vector2(2, 2.5f), 0.75f);
            PlaceArrow(entities, arrowPrefab, "Hint Wall Jump Arc 2", new Vector2(10, -3), new Vector2(-2, 1.5f), 0.5f);
            PlaceArrow(entities, arrowPrefab, "Hint Wall Jump Arc 3", new Vector2(8, 0), new Vector2(4, 1), 0.75f);
            // Compact 2-cell SPACE keys painted on the terrain at each take-off: floor, right wall, pillar face.
            PlaceKey(entities, keyPrefab, "Hint Wall Jump Key Floor", new Vector2(8, -7), jump, "", labelScale: 0.75f);
            PlaceKey(entities, keyPrefab, "Hint Wall Jump Key Wall", new Vector2(11, -4), jump, "", labelScale: 0.75f);
            PlaceKey(entities, keyPrefab, "Hint Wall Jump Key Pillar", new Vector2(6, -1), jump, "", labelScale: 0.75f);
        });

        AssetDatabase.SaveAssets();
        return "Tutorial hint prefabs, palette entries (21,3,0) and (25,3,0), JumpTutorial and GrabWallJumpTutorial hints set up.";
    }

    static void EditScene(string path, Action<Transform> place)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            place(scene.GetRootGameObjects().Select(r => r.transform.Find("Entities")).First(t => t != null));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static InputActionReference ActionRef(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>()
            .FirstOrDefault(r => r.action != null && $"{r.action.actionMap.name}/{r.action.name}" == name)
        ?? throw new Exception("No InputActionReference for " + name);

    static LineRenderer Line(GameObject root, string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = SortingOrder;
        return line;
    }

    static void AddEntry(Transform row, GameObject prefab, float x)
    {
        var existing = row.Find(prefab.name);
        var entry = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, row);
        entry.transform.localPosition = new Vector3(x, 3, 0);
    }

    static void PlaceKey(Transform parent, GameObject prefab, string name, Vector2 cell, InputActionReference action, string part,
        string labelOverride = "", bool showKeycap = true, float labelScale = 1f)
    {
        var go = Place(parent, prefab, name, cell);
        var key = go.GetComponent<TutorialKey>();
        var data = new SerializedObject(key);
        data.FindProperty("action").objectReferenceValue = action;
        data.FindProperty("compositePart").stringValue = part;
        data.FindProperty("labelOverride").stringValue = labelOverride;
        data.FindProperty("showKeycap").boolValue = showKeycap;
        data.FindProperty("labelScale").floatValue = labelScale;
        data.ApplyModifiedPropertiesWithoutUndo();
        key.Refresh();
    }

    static void PlaceArrow(Transform parent, GameObject prefab, string name, Vector2 cell, Vector2 end, float arc)
    {
        var arrow = Place(parent, prefab, name, cell).GetComponent<TutorialArrow>();
        var data = new SerializedObject(arrow);
        data.FindProperty("end").vector2Value = end;
        data.FindProperty("arcHeight").floatValue = arc;
        data.ApplyModifiedPropertiesWithoutUndo();
        arrow.Refresh();
    }

    static GameObject Place(Transform parent, GameObject prefab, string name, Vector2 cell)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = cell;
        return go;
    }
}
