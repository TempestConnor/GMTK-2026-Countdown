using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class ValidatePushable
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception(name); passed.Add(name); };
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var box = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Box.prefab");
            expect("Box is Pushable with a dynamic body and idle horizontal lock", box.GetComponent<Pushable>() != null &&
                box.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic &&
                (box.GetComponent<Rigidbody2D>().constraints & RigidbodyConstraints2D.FreezePositionX) != 0);
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
            var entry = palette.transform.Find("Layer1/Box");
            expect("Existing Box palette entry retains connection and grid position", entry != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(entry.gameObject) == box && entry.localPosition == new Vector3(-6, 3, 0));
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var target = level.transform.Find("Entities").gameObject;
            var brush = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset"));
            try
            {
                brush.Init(Vector3Int.one);
                brush.SetGameObject(Vector3Int.zero, box);
                int count = target.transform.childCount;
                brush.Paint(level.GetComponent<Grid>(), target, new Vector3Int(50, 0, 0));
                var painted = target.transform.GetChild(count).gameObject;
                expect("EntityBrush paints a connected Pushable Box onto Entities", painted.GetComponent<Pushable>() != null && PrefabUtility.GetCorrespondingObjectFromSource(painted) == box);
            }
            finally { UnityEngine.Object.DestroyImmediate(brush); }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab");
            var input = player.GetComponent<PlayerInput>();
            var action = input.actions.FindAction("Player/Interact", true);
            expect("Interact defaults to F", action.bindings.Count == 1 && action.bindings[0].path == "<Keyboard>/f");
            bool wired = false;
            foreach (var callback in input.actionEvents)
                if (callback.actionId == action.id.ToString())
                    wired = callback.GetPersistentMethodName(0) == "onInteract" && callback.GetPersistentTarget(0) == player.GetComponent<playerController2>();
            expect("Player prefab wires Interact to grab toggle", wired);
            return string.Join("\n", passed);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
