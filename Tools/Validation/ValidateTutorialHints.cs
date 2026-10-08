using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Edit mode: tutorial hint prefabs, connected palette entries/spacing, binding labels,
// keycap widths, and actual EntityBrush painting onto Level > Entities in a preview scene.
public static class ValidateTutorialHints
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, value) => { if (!value) throw new Exception(name); passed.Add(name); };

        var arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/TutorialArrow.prefab");
        var keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/TutorialKey.prefab");
        expect("Prefabs exist", arrowPrefab != null && keyPrefab != null);
        expect("Hints have no colliders", arrowPrefab.GetComponentInChildren<Collider2D>() == null && keyPrefab.GetComponentInChildren<Collider2D>() == null);

        var row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab").transform.Find("Layer1");
        var arrowEntry = row.Find("TutorialArrow");
        var keyEntry = row.Find("TutorialKey");
        expect("Arrow palette entry is prefab-connected at (21,3,0)", arrowEntry != null && arrowEntry.localPosition == new Vector3(21, 3, 0) && PrefabUtility.GetCorrespondingObjectFromSource(arrowEntry.gameObject) == arrowPrefab);
        expect("Key palette entry is prefab-connected at (25,3,0)", keyEntry != null && keyEntry.localPosition == new Vector3(25, 3, 0) && PrefabUtility.GetCorrespondingObjectFromSource(keyEntry.gameObject) == keyPrefab);
        var safeBox = row.Find("SafeBox");
        expect("One empty column after SafeBox [18,20]", safeBox != null && safeBox.localPosition.x == 18);

        var scene = EditorSceneManager.NewPreviewScene();
        var brush = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset"));
        try
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var target = level.transform.Find("Entities").gameObject;
            int column = 40;
            foreach (var prefab in new[] { arrowPrefab, keyPrefab })
            {
                column += 4; // GameObjectBrush won't stack two objects in one cell
                var cell = new Vector3Int(column, 2, 0);
                int count = target.transform.childCount;
                brush.Init(Vector3Int.one);
                brush.SetGameObject(Vector3Int.zero, prefab);
                brush.Paint(level.GetComponent<Grid>(), target, cell);
                expect("EntityBrush paints " + prefab.name + " onto Entities", target.transform.childCount == count + 1);
                var painted = target.transform.GetChild(count).gameObject;
                expect(prefab.name + " stays prefab-connected and grid-aligned",
                    PrefabUtility.GetCorrespondingObjectFromSource(painted) == prefab && painted.transform.localPosition == cell);
            }

            var arrow = target.GetComponentInChildren<TutorialArrow>();
            arrow.End = new Vector2(4, 2); arrow.ArcHeight = 1.5f;
            var shaft = arrow.transform.Find("Shaft").GetComponent<LineRenderer>();
            expect("Arc shaft starts at the tail cell's center and ends at the tip",
                shaft.positionCount > 2 && shaft.GetPosition(0) == (Vector3)TutorialArrow.Tail && Vector3.Distance(shaft.GetPosition(shaft.positionCount - 1), TutorialArrow.Tail + new Vector2(4, 2)) < 1e-4f);

            var key = target.GetComponentInChildren<TutorialKey>();
            var data = new SerializedObject(key);
            foreach (var (action, part, label) in new[] { ("Player/Move", "left", "A"), ("Player/Move", "right", "D"), ("Player/Jump", "", "SPACE"), ("Player/Interact", "", "F") })
            {
                data.FindProperty("action").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/playerActions.inputactions")
                    .OfType<UnityEngine.InputSystem.InputActionReference>().First(r => $"{r.action.actionMap.name}/{r.action.name}" == action);
                data.FindProperty("compositePart").stringValue = part;
                data.ApplyModifiedPropertiesWithoutUndo();
                key.Refresh();
                expect($"{action} {part} labels as {label}", key.Label == label);
                expect($"{label} keycap is {(label.Length == 1 ? 1 : 3)} cells wide", key.WidthCells == (label.Length == 1 ? 1 : 3));
            }
            data.FindProperty("action").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/playerActions.inputactions")
                .OfType<UnityEngine.InputSystem.InputActionReference>().First(r => r.action.name == "Jump");
            data.FindProperty("labelScale").floatValue = 0.75f;
            data.ApplyModifiedPropertiesWithoutUndo();
            key.Refresh();
            expect("Compact SPACE (label scale 0.75) fits 2 cells", key.WidthCells == 2);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(brush);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return "PASS " + passed.Count + ":\n" + string.Join("\n", passed);
    }
}
