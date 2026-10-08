using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Resources/RoomLinks.asset in sync with RoomTransition destinations.
/// Saving a scene refreshes its links; entering Play refreshes open scenes (including unsaved edits);
/// builds and the Tools menu rescan every scene under Assets/Scenes and validate.
/// </summary>
[InitializeOnLoad]
public static class RoomLinksBuilder
{
    public const string AssetPath = "Assets/Resources/" + RoomLinks.ResourcePath + ".asset";
    private const string ScenesRoot = "Assets/Scenes";

    public sealed class Zone
    {
        public string scene;
        public string id;
        public bool hasArrival;
        public bool triggerEnabled;
    }

    public sealed class SceneInfo
    {
        public readonly List<Zone> zones = new List<Zone>();
        public readonly List<RoomLinks.Link> declared = new List<RoomLinks.Link>();
        public int players;
    }

    static RoomLinksBuilder()
    {
        EditorSceneManager.sceneSaved += scene => Refresh(new[] { scene });
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (AssetDatabase.LoadAssetAtPath<RoomLinks>(AssetPath) == null) RebuildAll();
            else Refresh(OpenScenes());
        };
    }

    public static SceneInfo Collect(Scene scene)
    {
        var info = new SceneInfo();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var player in root.GetComponentsInChildren<PlayerLife>())
                if (player.gameObject.activeInHierarchy && player.enabled) info.players++;
            foreach (var zone in root.GetComponentsInChildren<RoomTransition>())
            {
                if (!zone.gameObject.activeInHierarchy || !zone.enabled) continue;
                var box = zone.GetComponent<BoxCollider2D>();
                info.zones.Add(new Zone { scene = scene.path, id = zone.zoneId, hasArrival = zone.arrival != null, triggerEnabled = box != null && box.enabled });
                if (!string.IsNullOrWhiteSpace(zone.destinationScene))
                    info.declared.Add(new RoomLinks.Link { fromScene = scene.path, fromZone = zone.zoneId, toScene = zone.destinationScene, toZone = zone.destinationZone });
            }
        }
        return info;
    }

    /// <summary>Uses the open scene when loaded (live edits), otherwise a preview copy of the saved file.</summary>
    public static SceneInfo Collect(string scenePath)
    {
        var open = SceneManager.GetSceneByPath(scenePath);
        if (open.IsValid() && open.isLoaded) return Collect(open);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return null;
        var preview = EditorSceneManager.OpenPreviewScene(scenePath);
        try { return Collect(preview); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static IEnumerable<Scene> OpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !string.IsNullOrEmpty(scene.path)) yield return scene;
        }
    }

    private static IEnumerable<string> AllScenePaths() =>
        AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot }).Select(AssetDatabase.GUIDToAssetPath);

    public static RoomLinks GetOrCreate()
    {
        var links = AssetDatabase.LoadAssetAtPath<RoomLinks>(AssetPath);
        if (links != null) return links;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        links = ScriptableObject.CreateInstance<RoomLinks>();
        AssetDatabase.CreateAsset(links, AssetPath);
        return links;
    }

    public static void Refresh(IEnumerable<Scene> scenes)
    {
        var links = GetOrCreate();
        var updated = new List<RoomLinks.Link>(links.links);
        foreach (var scene in scenes)
        {
            if (string.IsNullOrEmpty(scene.path)) continue;
            updated.RemoveAll(link => link.fromScene == scene.path);
            updated.AddRange(Collect(scene).declared);
        }
        Write(links, updated);
    }

    public static Dictionary<string, SceneInfo> RebuildAll()
    {
        var scenes = new Dictionary<string, SceneInfo>();
        foreach (var path in AllScenePaths())
        {
            var info = Collect(path);
            if (info != null) scenes[path] = info;
        }
        Write(GetOrCreate(), scenes.Values.SelectMany(info => info.declared).ToList());
        return scenes;
    }

    private static void Write(RoomLinks links, List<RoomLinks.Link> updated)
    {
        updated.Sort((x, y) => string.CompareOrdinal(x.fromScene + "\n" + x.fromZone, y.fromScene + "\n" + y.fromZone));
        IncludeInBuild(updated.SelectMany(link => new[] { link.fromScene, link.toScene }));
        if (updated.SequenceEqual(links.links)) return;
        links.links = updated;
        EditorUtility.SetDirty(links);
        AssetDatabase.SaveAssetIfDirty(links);
    }

    /// <summary>Linked rooms must be loadable, so adding a link enables both rooms in Build Settings.</summary>
    private static void IncludeInBuild(IEnumerable<string> paths)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        foreach (var path in paths.Distinct())
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            var existing = scenes.Find(s => s.path == path);
            if (existing == null) scenes.Add(new EditorBuildSettingsScene(path, true));
            else if (!existing.enabled) existing.enabled = true;
            else continue;
            changed = true;
        }
        if (changed) EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Rescans every scene and returns every problem found; empty means valid.
    /// With <paramref name="builtOnly"/>, rooms not enabled in the build (work in progress) are skipped;
    /// links from built rooms into them are still reported.</summary>
    public static List<string> Validate(bool builtOnly = false)
    {
        var scenes = RebuildAll();
        var errors = new List<string>();
        var built = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        // Endpoint "scene|zone" -> the distinct endpoints it is linked to.
        var partners = new Dictionary<string, HashSet<string>>();
        void Pair(string a, string b)
        {
            if (!partners.TryGetValue(a, out var set)) partners[a] = set = new HashSet<string>();
            set.Add(b);
        }

        foreach (var entry in scenes)
        {
            if (builtOnly && !built.Contains(entry.Key)) continue;
            var ids = new HashSet<string>();
            foreach (var zone in entry.Value.zones)
            {
                if (string.IsNullOrWhiteSpace(zone.id) || !ids.Add(zone.id))
                    errors.Add(entry.Key + ": empty or duplicate transition Zone ID '" + zone.id + "'.");
                if (!zone.hasArrival || !zone.triggerEnabled)
                    errors.Add(entry.Key + " / " + zone.id + ": needs an Arrival marker and an enabled trigger.");
            }
            if (entry.Value.zones.Count > 0 && entry.Value.players != 1)
                errors.Add(entry.Key + ": rooms with transitions need exactly one active player (found " + entry.Value.players + ").");
            foreach (var link in entry.Value.declared)
            {
                string from = link.fromScene + " / " + link.fromZone;
                if (link.toScene == link.fromScene) { errors.Add(from + ": cannot link to its own room."); continue; }
                if (!scenes.TryGetValue(link.toScene, out var target)) { errors.Add(from + ": destination scene not found under " + ScenesRoot + ": " + link.toScene); continue; }
                if (!target.zones.Any(z => z.id == link.toZone)) { errors.Add(from + ": no zone '" + link.toZone + "' in " + link.toScene); continue; }
                foreach (var path in new[] { link.fromScene, link.toScene })
                    if (!built.Contains(path)) errors.Add(from + ": room not enabled in build: " + path);
                string to = link.toScene + " / " + link.toZone;
                Pair(from, to);
                Pair(to, from);
            }
        }

        foreach (var entry in scenes)
        {
            if (builtOnly && !built.Contains(entry.Key)) continue;
            foreach (var zone in entry.Value.zones)
            {
                string key = entry.Key + " / " + zone.id;
                if (!partners.TryGetValue(key, out var set)) errors.Add(key + ": doorway is not linked to any room.");
                else if (set.Count > 1) errors.Add(key + ": linked to several doorways: " + string.Join(", ", set));
            }
        }
        return errors.Distinct().ToList();
    }

    [MenuItem("Tools/Rooms/Rebuild and Validate Links")]
    private static void ValidateMenu()
    {
        var errors = Validate();
        if (errors.Count == 0) Debug.Log("Room links are valid (" + GetOrCreate().links.Count + " declared).");
        foreach (var error in errors) Debug.LogError("Room link: " + error);
    }
}

public sealed class RoomLinksBuildValidation : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;
    public void OnPreprocessBuild(BuildReport report)
    {
        var errors = RoomLinksBuilder.Validate(builtOnly: true);
        if (errors.Count > 0) throw new BuildFailedException("Room links invalid:\n" + string.Join("\n", errors));
    }
}
