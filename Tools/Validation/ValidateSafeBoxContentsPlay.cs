using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Play mode: Box and SafeBox bodies inside safe regions (SafeBodyCollision). Inside a region a
/// body crosses open seams into connected regions; outside, regions stay solid and stacks hold.
/// Runs in a temporary local physics scene; nothing is saved.</summary>
public static class ValidateSafeBoxContentsPlay
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception("FAILED: " + name + " || Passed before failure: " + string.Join(" | ", passed)); passed.Add(name); };
        var scene = SceneManager.CreateScene("SafeBox contents validation", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        try
        {
            GameObject Spawn(string path, Vector2 at, float scale = 1)
            {
                var previousActive = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(scene);
                try
                {
                    var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity);
                    go.transform.localScale = new Vector3(scale, scale, 1);
                    return go;
                }
                finally { SceneManager.SetActiveScene(previousActive); }
            }
            var floor = new GameObject("Floor") { layer = LayerMask.NameToLayer("Ground") };
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector2(0, -1);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60, 2);

            const string boxPath = "Assets/Prefabs/Entities/Box.prefab", safePath = "Assets/Prefabs/Entities/SafeBox.prefab";
            // Two 4x4 SafeBoxes side by side: x[0,4] and x[4,8], one merged hollow space.
            var a = Spawn(safePath, new Vector2(0, 0), 2);
            var b = Spawn(safePath, new Vector2(4, 0), 2);
            Action<int> step = count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Physics2D.SyncTransforms();
                    SafeBoundarySystem.RefreshNow();
                    physics.Simulate(Time.fixedDeltaTime);
                }
            };
            Func<GameObject, Bounds> bounds = go => go.GetComponent<BoxCollider2D>().bounds;
            Action<GameObject, float, int> push = (go, speed, steps) =>
            {
                var body = go.GetComponent<Rigidbody2D>();
                body.constraints &= ~RigidbodyConstraints2D.FreezePositionX; // As a grab does.
                for (int i = 0; i < steps; i++)
                {
                    body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
                    step(1);
                }
                body.linearVelocity = new Vector2(0, body.linearVelocity.y);
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;
                step(10);
            };
            step(30);
            expect("Two touching SafeBoxes rest on the floor", Mathf.Abs(bounds(a).min.y) < .05f && Mathf.Abs(bounds(b).min.y) < .05f);

            // A regular Box placed inside A (as after a plane swap) rests on A's floor, not ejected.
            var inner = Spawn(boxPath, new Vector2(1, .5f));
            step(40);
            expect("Box inside a SafeBox settles inside instead of being pushed out",
                bounds(inner).min.x > -.05f && bounds(inner).max.x < 4.05f && Mathf.Abs(bounds(inner).min.y) < .05f);
            push(inner, 4, 60);
            expect("Box inside crosses the open seam into the neighbouring SafeBox",
                bounds(inner).center.x > 4.5f && bounds(inner).max.x < 8.05f && Mathf.Abs(bounds(inner).min.y) < .05f);
            push(inner, 4, 60);
            expect("Box inside is still contained by the merged region's outer wall", bounds(inner).max.x < 8.05f);
            push(inner, -4, 120);
            expect("Box inside is contained by the far wall after crossing back", bounds(inner).min.x > -.05f && bounds(inner).min.x < .3f);

            // A SafeBox inside a SafeBox crosses too, and its own edges never catch on the container.
            var innerSafe = Spawn(safePath, new Vector2(5.5f, .5f));
            step(40);
            expect("SafeBox inside a SafeBox settles inside", bounds(innerSafe).min.x > 3.95f && Mathf.Abs(bounds(innerSafe).min.y) < .05f &&
                Mathf.Abs(bounds(b).min.x - 4) < .05f);
            push(innerSafe, -4, 35);
            expect("SafeBox inside crosses the seam", bounds(innerSafe).center.x < 4.5f && bounds(innerSafe).min.x > bounds(inner).max.x - .05f);
            step(60);
            expect("Containers stay put and are not lifted by their contents", Mathf.Abs(bounds(a).min.x) < .05f &&
                Mathf.Abs(bounds(b).min.x - 4) < .05f && Mathf.Abs(bounds(a).min.y) < .05f && Mathf.Abs(bounds(b).min.y) < .05f &&
                Mathf.Abs(bounds(innerSafe).min.y) < .05f);

            // Outside: a Box pushed into A from the left stops at A's outer wall.
            var outside = Spawn(boxPath, new Vector2(-4, 0));
            step(20);
            push(outside, 4, 60);
            expect("Box outside is blocked by a SafeBox's wall", bounds(outside).max.x < .05f && bounds(outside).max.x > -.2f);

            // Stacks from outside: a Box and a SafeBox dropped on top rest there instead of falling in.
            var onTop = Spawn(boxPath, new Vector2(0, 5));
            var safeOnTop = Spawn(safePath, new Vector2(5, 5));
            step(80);
            expect("Box stacked on a SafeBox rests on its top", Mathf.Abs(bounds(onTop).min.y - 4) < .05f);
            expect("SafeBox stacked on a SafeBox rests on its top", Mathf.Abs(bounds(safeOnTop).min.y - 4) < .05f);
            var boxOnBox = Spawn(boxPath, new Vector2(0, 8));
            step(80);
            expect("Box stacked on a Box on a SafeBox rests on it", Mathf.Abs(bounds(boxOnBox).min.y - 6) < .05f);

            // Something stacked on B joins the merged space: a Box dropped inside it falls into B.
            var intoStack = Spawn(boxPath, bounds(safeOnTop).min);
            step(80);
            expect("Box inside a stacked SafeBox falls through the open seam into the one below",
                Mathf.Abs(bounds(intoStack).min.y) < .05f && Mathf.Abs(bounds(safeOnTop).min.y - 4) < .05f);

            // Regression: a SafeBox pushed into the bottom of a SafeBox stack. Equal sizes let each box hold
            // the other's center; containment must stay one-way or the stack drops through.
            var stackBottom = Spawn(safePath, new Vector2(20, 0));
            var stackTop = Spawn(safePath, new Vector2(20, 2));
            var stackSide = Spawn(safePath, new Vector2(22, 0));
            var mover = Spawn(safePath, new Vector2(22, 0)); // Coincides with stackSide, as after a plane swap.
            step(30);
            expect("Coincident equal SafeBoxes beside a stack stay put", Mathf.Abs(bounds(stackTop).min.y - 2) < .05f &&
                Mathf.Abs(bounds(mover).min.x - 22) < .05f && Mathf.Abs(bounds(stackSide).min.x - 22) < .05f);
            push(mover, -3, 40);
            step(60);
            expect("SafeBox pushed into the bottom of a stack enters it and the stack holds (top y=" +
                bounds(stackTop).min.y.ToString("F2") + ", bottom y=" + bounds(stackBottom).min.y.ToString("F2") + ")",
                Mathf.Abs(bounds(stackTop).min.y - 2) < .05f && Mathf.Abs(bounds(stackBottom).min.y) < .05f &&
                Mathf.Abs(bounds(mover).min.x - 20) < .05f && Mathf.Abs(bounds(mover).min.y) < .05f);

            // Safe terrain (fixture from PrepareSafeBoxPlay: safe cells x[0,10) y[-3,0), placed at x+40).
            const string fixturePath = "Assets/SafeBoxPhysicsPrototype.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fixturePath) == null) passed.Add("SKIPPED terrain: run PrepareSafeBoxPlay first");
            else
            {
                Spawn(fixturePath, new Vector2(40, 0));
                var onTerrain = Spawn(safePath, new Vector2(42, 0), 2);
                var beside = Spawn(boxPath, new Vector2(47, 0));
                step(40);
                expect("SafeBox and Box rest on safe terrain from outside",
                    Mathf.Abs(bounds(onTerrain).min.y) < .05f && Mathf.Abs(bounds(beside).min.y) < .05f);
                var dropIn = Spawn(boxPath, new Vector2(43, .5f));
                step(120);
                expect("Box inside a SafeBox falls through the open seam into safe terrain and rests on its floor",
                    Mathf.Abs(bounds(dropIn).min.y + 3) < .05f && Mathf.Abs(bounds(onTerrain).min.y) < .05f);
                push(dropIn, 4, 100);
                expect("Box inside safe terrain is contained by the terrain's wall", bounds(dropIn).max.x < 50.05f && bounds(dropIn).max.x > 49.7f);
            }
            return "PASS: " + string.Join(" | ", passed);
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
    }
}
