using System;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RoomPlaySession
{
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Already playing.");
        SessionState.SetString("RoomValidation.PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Rooms/ExampleRoom_A.unity");
        EditorApplication.EnterPlaymode();
        return "Starting example room A; open authoring scenes remain unchanged.";
    }
    public static string Stop()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("RoomValidation.PreviousStartScene", ""));
        EditorApplication.ExitPlaymode();
        return "Stopping play and restoring previous start-scene setting.";
    }
}
