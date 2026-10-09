using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tilemaps cache each cell's sprite in the scene file, so scenes that were closed when terrain
/// art was regenerated keep showing the old sprites. This re-evaluates every tile in every scene
/// under Assets/Scenes and saves the scenes that changed.
/// </summary>
public static class TerrainSceneRefresh
{
    private const string MenuPath = "Tools/Level/Refresh Terrain In All Scenes";

    [MenuItem(MenuPath)]
    public static void RefreshAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var setup = EditorSceneManager.GetSceneManagerSetup();
        int saved = 0;
        try
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Refreshing terrain", path, (float)i / guids.Length);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool any = false;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(true))
                {
                    tilemap.RefreshAllTiles();
                    any = true;
                }
                if (!any) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                saved++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log($"Terrain refresh: re-evaluated tiles and saved {saved} scene(s).");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRefreshAll() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
