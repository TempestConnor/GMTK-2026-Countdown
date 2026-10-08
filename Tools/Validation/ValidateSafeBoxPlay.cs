using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Play mode (run PrepareSafeBoxPlay in Edit mode first): SafeBox gameplay in a temporary local
/// physics scene with a virtual keyboard. Player death is detected via Damageable.IsDead; the scene-reload
/// listener is muted on the test instance only.</summary>
public static class ValidateSafeBoxPlay
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception("FAILED: " + name + " || Passed before failure: " + string.Join(" | ", passed)); passed.Add(name); };
        var scene = SceneManager.CreateScene("SafeBox validation", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var previousUpdateMode = InputSystem.settings.updateMode;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        // The editor is usually unfocused while scripted; route virtual keys to the Game view regardless.
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var spawned = new List<GameObject>();
        try
        {
            GameObject Spawn(string path, Vector2 at)
            {
                // Instantiate directly into the test scene, as a level load would. Moving a baked tilemap
                // composite between physics scenes afterwards regenerates it without its tile shapes.
                var previousActive = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(scene);
                GameObject go;
                try { go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity); }
                finally { SceneManager.SetActiveScene(previousActive); }
                spawned.Add(go);
                return go;
            }
            GameObject Solid(string name, Rect r, bool lethal)
            {
                var go = new GameObject(name) { layer = LayerMask.NameToLayer("Ground") };
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = r.center;
                go.AddComponent<BoxCollider2D>().size = r.size;
                if (lethal) go.AddComponent<KillsOnPenetration>();
                spawned.Add(go);
                return go;
            }
            const string safeBoxPath = "Assets/Prefabs/Entities/SafeBox.prefab";
            var player = Spawn("Assets/Prefabs/Entities/Player.prefab", new Vector2(-20, 0));
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
            Spawn("Assets/SafeBoxPhysicsPrototype.prefab", Vector2.zero);
            var terrain = Object.FindObjectsByType<SafeRegion>();
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
            Action<Vector2> placeFeet = feet =>
            {
                Physics2D.SyncTransforms();
                playerBody.position += feet - new Vector2(capsule.bounds.center.x, capsule.bounds.min.y);
                playerBody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            };
            Func<GameObject, bool> inside = box =>
            {
                var b = box.GetComponent<BoxCollider2D>().bounds; var p = capsule.bounds;
                return p.min.x > b.min.x - .02f && p.max.x < b.max.x + .02f && p.min.y > b.min.y - .02f && p.max.y < b.max.y + .02f;
            };
            // Any blocking geometry (lethal or not) intersecting the player beyond solver slop.
            var overlaps = new List<Collider2D>();
            Func<string> obstruction = () =>
            {
                Physics2D.SyncTransforms();
                var filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(Physics2D.GetLayerCollisionMask(player.layer));
                capsule.Overlap(filter, overlaps);
                foreach (var other in overlaps)
                {
                    if (other.attachedRigidbody == playerBody || !SafePlayerCollision.Blocks(capsule, other)) continue;
                    var d = capsule.Distance(other);
                    if (d.isValid && d.distance < -.03f) return other.name + " d=" + d.distance.ToString("F3");
                }
                return null;
            };
            var probeHits = new List<Collider2D>();
            // Queries the live physics world: is a player edge of this region present at p?
            Func<SafeRegion, Vector2, bool> blocked = (region, p) =>
            {
                Physics2D.SyncTransforms();
                var f = ContactFilter2D.noFilter;
                physics.OverlapCircle(p, .005f, f, probeHits);
                foreach (var c in probeHits) if (region.Owns(c)) return true;
                return false;
            };
            Action<string> alive = name => expect(name + " (alive, not obstructed: " + (obstruction() ?? "clear") + ")", !damageable.IsDead && obstruction() == null);

            // ---------- Lethal floor y=0 from x=20..80: player interaction ----------
            Solid("Lethal floor", new Rect(20, -1, 60, 1), true);
            var s1 = Spawn(safeBoxPath, new Vector2(30, 0));
            var s1Body = s1.GetComponent<Rigidbody2D>();
            var s1Region = s1.GetComponent<SafeRegion>();
            placeFeet(new Vector2(29.5f, .02f));
            controller._isFacingRight = true;
            step(10);
            float idle = s1Body.position.x;
            keys(new[] { Key.D }); step(25);
            expect("Outer boundary blocks walking in; ungrabbed SafeBox does not move",
                Mathf.Abs(s1Body.position.x - idle) < .01f && capsule.bounds.max.x < 30.03f);
            keys(new[] { Key.F }); keys(Array.Empty<Key>());
            expect("Grab from outside", controller.GrabbedBody == s1Body);
            keys(new[] { Key.D }); step(20);
            expect("Push right", s1Body.position.x > idle + .5f);
            float pushed = s1Body.position.x;
            keys(new[] { Key.A }); step(20);
            expect("Pull left", s1Body.position.x < pushed - .5f);
            keys(new[] { Key.Space });
            expect("Jump while grabbing lifts SafeBox", playerBody.linearVelocity.y > 0 && Mathf.Abs(s1Body.linearVelocity.y - playerBody.linearVelocity.y) < .01f);
            step(3); keys(new[] { Key.F }); keys(Array.Empty<Key>()); step(80);
            expect("Release relocks; SafeBox rests on floor", controller.GrabbedBody == null && Mathf.Abs(s1Body.position.y) < .05f);
            alive("Grab/push/pull/jump");

            // Phase in (as after a plane swap) and move inside.
            s1Body.position = new Vector2(30, s1Body.position.y); Physics2D.SyncTransforms();
            placeFeet(new Vector2(31, s1Body.position.y + .05f));
            penetration.CheckNow();
            step(30);
            expect("Phased-in player stays inside the hollow", inside(s1));
            alive("Phase in");
            keys(new[] { Key.D }); step(40);
            expect("Walking right inside stops at the boundary", inside(s1) && capsule.bounds.max.x > 31.8f);
            keys(new[] { Key.A }); step(40);
            expect("Walking left inside stops at the boundary", inside(s1) && capsule.bounds.min.x < 30.2f);
            keys(Array.Empty<Key>()); step(10);
            keys(new[] { Key.Space }); step(4);
            expect("Jumping inside rises", playerBody.linearVelocity.y > 0 || capsule.bounds.min.y > s1Body.position.y + .1f);
            keys(Array.Empty<Key>()); step(60);
            expect("Jump stays within the box", inside(s1));
            alive("Movement inside");
            controller._isFacingRight = true;
            keys(new[] { Key.F }); keys(Array.Empty<Key>());
            expect("Cannot grab from inside", controller.GrabbedBody == null);

            // Banish targeting and plane change.
            var reticule = player.GetComponentInChildren<BanishReticule>(true);
            expect("Player has a BanishReticule", reticule != null);
            var reticuleParent = reticule.transform.parent;
            bool wasActive = reticule.gameObject.activeSelf;
            reticule.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(reticule.gameObject, scene);
            reticule.transform.position = new Vector3(31, 1, 0);
            reticule.gameObject.SetActive(true);
            step(2);
            bool targeted = false;
            foreach (var m in reticule.TouchingMembers) targeted |= m == s1.GetComponent<Banishable>();
            reticule.gameObject.SetActive(wasActive);
            reticule.transform.SetParent(reticuleParent, true);
            expect("Banish reticule targets SafeBox", targeted);
            s1.GetComponent<Banishable>().SetPlane(Banishable.Plane.B);
            keys(new[] { Key.D }); step(40);
            expect("Banished SafeBox no longer blocks the plane-A player", capsule.bounds.min.x > 32.05f);
            keys(Array.Empty<Key>()); step(5);
            s1.GetComponent<Banishable>().SetPlane(Banishable.Plane.A);
            step(5);
            alive("Banish/return");
            placeFeet(new Vector2(31, s1Body.position.y + .05f)); step(10);
            keys(new[] { Key.D }); step(40); keys(Array.Empty<Key>());
            expect("Returned SafeBox blocks again", inside(s1));

            // ---------- Connections between SafeBoxes (on lethal floor) ----------
            var s2 = Spawn(safeBoxPath, new Vector2(40, 0));
            var s3 = Spawn(safeBoxPath, new Vector2(42, 0));
            var s2Region = s2.GetComponent<SafeRegion>(); var s3Region = s3.GetComponent<SafeRegion>();
            step(5);
            expect("Full contact opens both shared edges", !blocked(s2Region, new Vector2(42, 1)) && !blocked(s3Region, new Vector2(42, 1)));
            expect("Outer edges stay closed", blocked(s2Region, new Vector2(40, 1)) && blocked(s3Region, new Vector2(44, 1)) && blocked(s2Region, new Vector2(41, 2)));
            placeFeet(new Vector2(41, s2.transform.position.y + .05f)); step(5);
            keys(new[] { Key.D }); step(50); keys(Array.Empty<Key>());
            expect("Player walks from one SafeBox into its neighbor", capsule.bounds.center.x > 42.5f && inside(s3));
            alive("Walk through connection");

            // Separation while straddling: closure displaces the player, no death.
            placeFeet(new Vector2(42, s2.transform.position.y + .05f)); step(3);
            var s3Body = s3.GetComponent<Rigidbody2D>();
            s3Body.position = new Vector2(47, 0); Physics2D.SyncTransforms();
            // A lethal obstacle where the right-hand clear pose would be forces displacement into s2.
            var wall = Solid("Lethal obstacle", new Rect(42.45f, 0, 2, 2), true);
            step(2);
            expect("Separation closes both edges", blocked(s2Region, new Vector2(42, 1)) && blocked(s3Region, new Vector2(47, 1)));
            alive("Closure displacement beside a lethal obstacle");
            expect("Displaced into the SafeBox interior, not the obstacle", inside(s2));
            Object.DestroyImmediate(wall);

            // Partial edge, corner, multiple neighbors.
            var pedestal = Solid("Pedestal", new Rect(42, 0, 2, 1), false);
            s3Body.position = new Vector2(42, 1); s3Body.linearVelocity = Vector2.zero; step(3);
            expect("Partial contact opens only the shared span on both boxes",
                blocked(s2Region, new Vector2(42, .5f)) && !blocked(s2Region, new Vector2(42, 1.5f)) &&
                !blocked(s3Region, new Vector2(42, 1.5f)) && blocked(s3Region, new Vector2(42, 2.5f)));
            Object.DestroyImmediate(pedestal);
            pedestal = Solid("Pedestal", new Rect(42, 0, 2, 2), false);
            s3Body.position = new Vector2(42, 2); s3Body.linearVelocity = Vector2.zero; step(3);
            expect("Corner-only contact stays closed", blocked(s2Region, new Vector2(42, 1.9f)) && blocked(s3Region, new Vector2(42, 2.1f)) && blocked(s2Region, new Vector2(41.9f, 2)));
            Object.DestroyImmediate(pedestal);
            s3Body.position = new Vector2(42, 0); s3Body.linearVelocity = Vector2.zero;
            var s4 = Spawn(safeBoxPath, new Vector2(38, 0));
            step(3);
            expect("Multiple neighbors open independently", !blocked(s2Region, new Vector2(40, 1)) && !blocked(s2Region, new Vector2(42, 1)) && blocked(s2Region, new Vector2(41, 2)));

            // Plane change, disable, destroy restore boundaries; player in a passage is displaced.
            placeFeet(new Vector2(42, .05f)); step(3);
            s3.GetComponent<Banishable>().SetPlane(Banishable.Plane.B); step(2);
            expect("Neighbor on other plane closes the edge", blocked(s2Region, new Vector2(42, 1)) && blocked(s3Region, new Vector2(42, 1)));
            alive("Plane-change closure");
            s3.GetComponent<Banishable>().SetPlane(Banishable.Plane.A); step(5);
            expect("Return reopens", !blocked(s2Region, new Vector2(42, 1)));
            placeFeet(new Vector2(42, .05f)); step(3);
            s3.SetActive(false); step(2);
            expect("Disabled neighbor restores boundary", blocked(s2Region, new Vector2(42, 1)));
            alive("Disable closure");
            s3.SetActive(true); step(3);
            expect("Re-enabled neighbor reopens", !blocked(s2Region, new Vector2(42, 1)));
            Object.DestroyImmediate(s4); step(2);
            expect("Destroyed neighbor restores only its side", blocked(s2Region, new Vector2(40, 1)) && !blocked(s2Region, new Vector2(42, 1)));

            // ---------- Safe terrain (baked fixture, safe x[0,10) y[-3,0)) ----------
            var t1 = Spawn(safeBoxPath, new Vector2(2, .1f));
            var t1Body = t1.GetComponent<Rigidbody2D>(); var t1Region = t1.GetComponent<SafeRegion>();
            var plainOnSafe = Spawn("Assets/Prefabs/Entities/Box.prefab", new Vector2(7, .1f));
            var plainOnLethal = Spawn("Assets/Prefabs/Entities/Box.prefab", new Vector2(13, .1f));
            placeFeet(new Vector2(-10, -50)); step(100);
            SafeRegion ground = null;
            foreach (var r in terrain) if (r != null && !r.Movable && r.transform.parent.name == "Ground") ground = r;
            expect("Baked terrain carries a SafeRegion", ground != null);
            expect("SafeBox rests on safe terrain with the passage open (y=" + t1Body.position.y.ToString("F3") + " plainSafe=" + plainOnSafe.transform.position.y.ToString("F3") + " support=" + ground.Support.bounds + " shapes=" + ground.Support.shapeCount + ")",
                Mathf.Abs(t1Body.position.y) < .05f && !blocked(t1Region, new Vector2(3, 0)) && !blocked(ground, new Vector2(3, 0)));
            expect("Terrain edge outside the box stays closed", blocked(ground, new Vector2(1, 0)) && blocked(ground, new Vector2(5, 0)));
            expect("Ordinary Boxes still rest on safe and lethal terrain",
                Mathf.Abs(plainOnSafe.transform.position.y) < .05f && Mathf.Abs(plainOnLethal.transform.position.y) < .05f);
            placeFeet(new Vector2(3, t1Body.position.y + .3f)); step(60);
            expect("Player inside a SafeBox drops into connected safe terrain", capsule.bounds.min.y < -.5f);
            alive("Connected terrain");
            // Push along the terrain while connected: support must persist during continuous motion.
            placeFeet(new Vector2(1.5f, .02f)); controller._isFacingRight = true; step(10);
            keys(new[] { Key.F });
            expect("Grab SafeBox on safe terrain from outside", controller.GrabbedBody == t1Body);
            keys(new[] { Key.D }); step(30); keys(Array.Empty<Key>()); keys(new[] { Key.F }); keys(Array.Empty<Key>()); step(30);
            expect("Pushed SafeBox stays supported on safe terrain (x=" + t1Body.position.x.ToString("F2") + ", y=" + t1Body.position.y.ToString("F3") + ")",
                t1Body.position.x > 2.5f && Mathf.Abs(t1Body.position.y) < .05f);
            alive("Push on terrain");

            // ---------- Profiling (Play mode, this machine) ----------
            var system = Object.FindAnyObjectByType<SafeBoundarySystem>();
            var crowd = new List<Rigidbody2D>();
            for (int i = 0; i < 48; i++) crowd.Add(Spawn(safeBoxPath, new Vector2(100 + (i % 12) * 2, (i / 12) * 2 + 10)).GetComponent<Rigidbody2D>());
            SafeBoundarySystem.RefreshNow();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) SafeBoundarySystem.RefreshNow();
            watch.Stop();
            passed.Add("PROFILE idle refresh, " + (crowd.Count + 6) + " regions: " + (watch.Elapsed.TotalMilliseconds / 1000).ToString("F4") + " ms/step");
            watch.Restart(); int edges = 0;
            for (int i = 0; i < 1000; i++)
            {
                for (int k = 0; k < 6; k++) crowd[k].transform.position = new Vector2(100 + k * 2, 10 + (i % 50) * .02f);
                SafeBoundarySystem.RefreshNow(); edges += system.LastUpdatedEdges;
            }
            watch.Stop();
            passed.Add("PROFILE 6 of " + crowd.Count + " packed SafeBoxes moving: " + (watch.Elapsed.TotalMilliseconds / 1000).ToString("F4") +
                " ms/step, " + (edges / 1000f).ToString("F1") + " edges rebuilt/step");
            return string.Join("\n", passed);
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
