using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Keeps saved collision current, including undo, unopened build scenes and Play entry.</summary>
[InitializeOnLoad]
public sealed class TerrainCollisionBaking : IProcessSceneWithReport
{
    static TerrainCollisionBaking()
    {
        EditorSceneManager.sceneSaving += (scene, path) => Bake(scene);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) Bake(SceneManager.GetSceneAt(i));
        };
    }

    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null) Bake(scene);
    }

    public static void Bake(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var terrain in root.GetComponentsInChildren<TerrainCollision>(true))
            {
                terrain.GetComponent<UnityEngine.Tilemaps.Tilemap>().RefreshAllTiles();
                terrain.Rebuild();
            }
    }
}
