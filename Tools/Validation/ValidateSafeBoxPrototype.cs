using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ValidateSafeBoxPrototype
{
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Run in Edit mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var regions = new List<SafeRegion>();
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception(string.Join(" | ", passed) + " FAILED " + name); passed.Add(name); };
        try
        {
            Func<Vector2, SafeRegion> box = position =>
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Box.prefab"), scene);
                go.transform.localScale = Vector3.one;
                go.transform.position = position;
                go.GetComponent<KillsOnPenetration>().enabled = false;
                var r = go.AddComponent<SafeRegion>();
                r.Bake(go.GetComponent<BoxCollider2D>(), new[] {
                    new SafeRegion.Boundary(new Vector2(0,0),new Vector2(0,2),Vector2.left),
                    new SafeRegion.Boundary(new Vector2(2,0),new Vector2(2,2),Vector2.right),
                    new SafeRegion.Boundary(new Vector2(0,0),new Vector2(2,0),Vector2.down),
                    new SafeRegion.Boundary(new Vector2(0,2),new Vector2(2,2),Vector2.up)}, true);
                regions.Add(r); SafeBoundarySystem.Register(r); return r;
            };
            Action refresh = () => { Physics2D.SyncTransforms(); SafeBoundarySystem.RefreshNow(); Physics2D.SyncTransforms(); };
            Func<SafeRegion, Vector2, bool> blocked = (r, p) =>
            {
                foreach (var edge in Resources.FindObjectsOfTypeAll<EdgeCollider2D>())
                    if (r.Owns(edge) && edge.enabled && edge.pointCount > 0 && !edge.isTrigger && (edge.ClosestPoint(p) - p).sqrMagnitude < .00001f) return true;
                return false;
            };
            var a = box(Vector2.zero); var b = box(new Vector2(2,0)); refresh();
            expect("Full shared edge opens from BOTH sides", !blocked(a,new Vector2(2,1)) && !blocked(b,new Vector2(2,1)));
            expect("Outer boundary stays closed; center is hollow", blocked(a,new Vector2(0,1)) && !blocked(a,new Vector2(1,1)));
            b.transform.position = new Vector2(2,1); refresh();
            expect("Partial contact opens only overlap on both sides", blocked(a,new Vector2(2,.5f)) && !blocked(a,new Vector2(2,1.5f)) && !blocked(b,new Vector2(2,1.5f)) && blocked(b,new Vector2(2,2.5f)));
            b.transform.position = new Vector2(2,2); refresh();
            expect("Corner contact leaves both edges closed", blocked(a,new Vector2(2,1.5f)) && blocked(b,new Vector2(2,2.5f)));
            b.transform.position = new Vector2(2,0); b.GetComponent<Banishable>().SetPlane(Banishable.Plane.B); refresh();
            expect("Different planes do not connect", blocked(a,new Vector2(2,1)) && blocked(b,new Vector2(2,1)));
            b.GetComponent<Banishable>().SetPlane(Banishable.Plane.A); refresh();
            expect("Return reconnects", !blocked(a,new Vector2(2,1)));
            b.gameObject.SetActive(false); refresh();
            expect("Disabled neighbor restores boundary", blocked(a,new Vector2(2,1)));
            b.gameObject.SetActive(true); refresh();
            var c = box(new Vector2(0,2)); refresh();
            expect("Multiple neighbors open independent sides", !blocked(a,new Vector2(1,2)) && !blocked(a,new Vector2(2,1)));
            SafeBoundarySystem.Unregister(c); UnityEngine.Object.DestroyImmediate(c.gameObject); refresh();
            expect("Destroyed neighbor restores only its boundary", blocked(a,new Vector2(1,2)) && !blocked(a,new Vector2(2,1)));
            b.transform.position = new Vector2(5,0); refresh();
            expect("Separation restores both sides", blocked(a,new Vector2(2,1)) && blocked(b,new Vector2(5,1)));
            var level = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level/LevelTemplate.prefab"), scene);
            var map = level.transform.Find("Ground").GetComponent<Tilemap>(); map.ClearAllTiles();
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>("Assets/Tiles/Tile_SafeWall.asset");
            for(int x=-2;x<4;x++) for(int y=-2;y<0;y++) map.SetTile(new Vector3Int(x,y,0),tile);
            map.GetComponent<TerrainCollision>().Rebuild();
            var terrain = map.GetComponentInChildren<SafeRegion>(); regions.Add(terrain); SafeBoundarySystem.Register(terrain); refresh();
            expect("Baked terrain and box open their shared floor", !blocked(a,new Vector2(1,0)) && !blocked(terrain,new Vector2(1,0)));
            expect("Support remains enabled and excludes ONLY player layers", a.Support.enabled && terrain.Support.enabled && a.Support.excludeLayers.value == SafeRegion.PlayerMask);
            // Physical support is validated in Play mode (ValidateSafeBoxPlay): Edit-mode preview scenes regenerate
            // tilemap composites whenever any collider changes, which drops resting contacts there only.
            var system = UnityEngine.Object.FindAnyObjectByType<SafeBoundarySystem>();
            refresh(); refresh(); expect("Unchanged geometry rebuilds zero edges", system.LastUpdatedEdges == 0);
            var watch = Stopwatch.StartNew();
            for(int i=0;i<1000;i++) SafeBoundarySystem.RefreshNow(); watch.Stop();
            passed.Add("PROFILE idle 1000 updates: " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms (Editor, no physics)");
            watch.Restart();
            for(int i=0;i<1000;i++) { b.transform.position = new Vector2(2, (i % 100)*.01f); SafeBoundarySystem.RefreshNow(); }
            watch.Stop(); passed.Add("PROFILE moving partial contact 1000 updates: " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms; last edges=" + system.LastUpdatedEdges + ", candidate pairs=" + system.LastCandidatePairs);
            return string.Join("\n",passed);
        }
        finally
        {
            foreach(var r in regions) if(r != null) SafeBoundarySystem.Unregister(r);
            EditorSceneManager.ClosePreviewScene(scene);
            var system = UnityEngine.Object.FindAnyObjectByType<SafeBoundarySystem>();
            if(system != null) UnityEngine.Object.DestroyImmediate(system.gameObject);
        }
    }
}
