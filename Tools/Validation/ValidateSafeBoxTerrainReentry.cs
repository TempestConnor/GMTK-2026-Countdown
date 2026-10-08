using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Play mode (run PrepareSafeBoxPlay in Edit mode first): a SafeBox resting beside safe terrain.
/// The player walks out of the box into the terrain and must be able to walk straight back in without
/// jumping. Regression for sub-tolerance boundary slivers left by the box's resting contact offset.</summary>
public static class ValidateSafeBoxTerrainReentry
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var log = new List<string>();
        var scene = SceneManager.CreateScene("SafeBox reentry validation", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var previousUpdateMode = InputSystem.settings.updateMode;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var spawned = new List<GameObject>();
        try
        {
            GameObject Spawn(string path, Vector2 at)
            {
                var previousActive = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(scene);
                GameObject go;
                try { go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity); }
                finally { SceneManager.SetActiveScene(previousActive); }
                spawned.Add(go);
                return go;
            }
            var player = Spawn("Assets/Prefabs/Entities/Player.prefab", new Vector2(-20, -50));
            var controller = player.GetComponent<playerController2>();
            var playerBody = player.GetComponent<Rigidbody2D>();
            var capsule = player.GetComponent<CapsuleCollider2D>();
            var damageable = player.GetComponent<Damageable>();
            for (int i = 0; i < damageable.OnKilled.GetPersistentEventCount(); i++)
                damageable.OnKilled.SetPersistentListenerState(i, UnityEventCallState.Off);
            var penetration = player.GetComponent<DamageablePenetrationCheck>();
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.GetComponent<PlayerInput>().ActivateInput();
            var physics = scene.GetPhysicsScene2D();
            // Baked fixture: safe x[0,10) y[-3,0), lethal x[12,16) y[-3,0). The SafeBox fills the slot between.
            Spawn("Assets/SafeBoxPhysicsPrototype.prefab", Vector2.zero);
            var floor = new GameObject("Floor") { layer = LayerMask.NameToLayer("Ground") };
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector2(11, -3.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(2, 1);
            spawned.Add(floor);
            var box = Spawn("Assets/Prefabs/Entities/SafeBox.prefab", new Vector2(10, -3));
            var boxBody = box.GetComponent<Rigidbody2D>();

            var fixedUpdate = typeof(playerController2).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<int> step = count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Physics2D.SyncTransforms();
                    SafeBoundarySystem.RefreshNow();
                    fixedUpdate.Invoke(controller, null);
                    penetration.CheckNow();
                    physics.Simulate(Time.fixedDeltaTime);
                }
            };
            Action<Key[]> keys = held => { InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); InputSystem.Update(); };
            step(100);
            log.Add("SafeBox resting y offset from grid: " + (boxBody.position.y + 3).ToString("F4"));
            Physics2D.SyncTransforms();
            foreach (var e in Object.FindObjectsByType<EdgeCollider2D>())
            {
                if (e.gameObject.scene != scene || !e.enabled) continue;
                Vector2 a = e.transform.TransformPoint(e.points[0]), b = e.transform.TransformPoint(e.points[e.points.Length - 1]);
                if (Mathf.Min(a.x, b.x) < 10.2f && Mathf.Max(a.x, b.x) > 9.8f && Mathf.Max(a.y, b.y) > -3.2f && Mathf.Min(a.y, b.y) < 0.2f)
                    log.Add("  active section " + e.transform.parent.name + ": " + a.ToString("F3") + " -> " + b.ToString("F3"));
            }

            Physics2D.SyncTransforms();
            playerBody.position += new Vector2(11, boxBody.position.y + .05f) - new Vector2(capsule.bounds.center.x, capsule.bounds.min.y);
            playerBody.linearVelocity = Vector2.zero;
            step(30);
            keys(new[] { Key.A }); step(60); keys(Array.Empty<Key>()); step(10);
            log.Add("After walking out left: player center x=" + capsule.bounds.center.x.ToString("F3"));
            if (capsule.bounds.max.x > 9.9f) throw new Exception("FAILED: could not walk out of the SafeBox into safe terrain\n" + string.Join("\n", log));
            keys(new[] { Key.D }); step(60);
            var contacts = new List<ContactPoint2D>();
            playerBody.GetContacts(contacts);
            log.Add("Player bounds while walking back: " + capsule.bounds.min.ToString("F3") + " .. " + capsule.bounds.max.ToString("F3"));
            foreach (var c in contacts) log.Add("  contact " + c.collider.name + "/" + c.otherCollider.name + " at " + c.point.ToString("F3") + " normal " + c.normal.ToString("F3"));
            keys(Array.Empty<Key>()); step(10);
            log.Add("After walking back right: player center x=" + capsule.bounds.center.x.ToString("F3"));
            if (capsule.bounds.center.x < 10.5f) throw new Exception("FAILED: could not walk back into the SafeBox without jumping\n" + string.Join("\n", log));
            if (damageable.IsDead) throw new Exception("FAILED: player died\n" + string.Join("\n", log));
            log.Add("PASSED: walked out into safe terrain and back into the SafeBox without jumping");
            return string.Join("\n", log);
        }
        finally
        {
            foreach (var go in spawned) if (go != null) go.SetActive(false);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
