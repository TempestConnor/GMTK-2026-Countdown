using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateRooms
{
    private sealed class ActionProbe : IPlayerAction
    {
        public int count;
        public void Cancel() { count++; }
    }
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, result) => { if (!result) throw new Exception(name); passed.Add(name); };
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab");
            expect("Player prefab carries shared input lock and cancellation registry", player.GetComponent<PlayerInputLock>() != null && player.GetComponent<PlayerActionCancellation>() != null);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/RoomTransition.prefab");
            expect("Transition prefab exists with safe marker and rectangular trigger", prefab != null && prefab.GetComponent<RoomTransition>().arrival != null && prefab.GetComponent<BoxCollider2D>().isTrigger);
            var collider = prefab.GetComponent<BoxCollider2D>();
            expect("Transition uses bottom-left grid pivot", collider.offset == collider.size / 2);
            expect("Transition explicitly includes both player planes", (collider.includeLayers.value & (1 << LayerMask.NameToLayer("Player"))) != 0 && (collider.includeLayers.value & (1 << LayerMask.NameToLayer("PlayerB"))) != 0);
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
            var entry = palette.transform.Find("Layer1/RoomTransition");
            expect("Palette entry retains prefab connection", entry != null && PrefabUtility.GetCorrespondingObjectFromSource(entry.gameObject) == prefab);
            float right = float.NegativeInfinity;
            foreach (Transform sibling in entry.parent)
            {
                // Only entries to the left: later entities (e.g. SafeBox) follow RoomTransition.
                if (sibling == entry || sibling.localPosition.x > entry.localPosition.x) continue;
                var box = sibling.GetComponent<BoxCollider2D>();
                if (box != null) right = Mathf.Max(right, sibling.localPosition.x + box.size.x);
                else foreach (var visual in sibling.GetComponentsInChildren<SpriteRenderer>())
                    right = Mathf.Max(right, entry.parent.InverseTransformPoint(visual.bounds.max).x);
            }
            expect("Palette has one empty column and row y=3", entry.localPosition == new Vector3(Mathf.Ceil(right) + 1, 3, 0));
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var target = level.transform.Find("Entities").gameObject;
            var brush = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEditor.Tilemaps.GameObjectBrush>("Assets/Palettes/EntityBrush.asset"));
            try
            {
                brush.Init(Vector3Int.one);
                brush.SetGameObject(Vector3Int.zero, prefab);
                int count = target.transform.childCount;
                brush.Paint(level.GetComponent<Grid>(), target, new Vector3Int(50, 0, 0));
                expect("EntityBrush paints transition onto Entities", target.transform.childCount == count + 1);
                expect("Painted transition retains prefab connection", PrefabUtility.GetCorrespondingObjectFromSource(target.transform.GetChild(count).gameObject) == prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(brush); }
            var registry = level.AddComponent<PlayerActionCancellation>();
            var ordinary = new ActionProbe(); var banish = new ActionProbe();
            registry.Register(ordinary); registry.Register(banish); registry.Register(ordinary);
            registry.CancelAll(banish);
            expect("Cancellation honors exception and ignores duplicate registrations", ordinary.count == 1 && banish.count == 0);
            registry.CancelAll();
            expect("Death-style cancellation cancels every registered action", ordinary.count == 2 && banish.count == 1);
            var links = ScriptableObject.CreateInstance<RoomLinks>();
            try
            {
                links.links.Add(new RoomLinks.Link { fromScene = "A", fromZone = "east", toScene = "B", toZone = "west" });
                expect("Declared side resolves to its destination", links.TryGetDestination("A", "east", out var toScene, out var toZone) && toScene == "B" && toZone == "west");
                expect("Undeclared side resolves back", links.TryGetDestination("B", "west", out var backScene, out var backZone) && backScene == "A" && backZone == "east");
                expect("Unknown endpoint is rejected", !links.TryGetDestination("A", "missing", out _, out _));
            }
            finally { UnityEngine.Object.DestroyImmediate(links); }
            var errors = RoomLinksBuilder.Validate();
            expect("Saved rooms have unique linked zones, arrival markers, players and build entries:\n" + string.Join("\n", errors), errors.Count == 0);
            var membership = new LevelCatalog.Level { scenePath = "start", roomScenePaths = new List<string> { "room" } };
            expect("Level membership covers start and additional rooms", membership.ContainsScene("start") && membership.ContainsScene("room") && !membership.ContainsScene("other"));
            return string.Join("\n", passed);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
