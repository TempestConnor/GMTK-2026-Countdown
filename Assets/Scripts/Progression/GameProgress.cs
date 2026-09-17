using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameProgress
{
    public const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private static PlayerProfile profile;
    private static LevelCatalog catalog;
    public static PlayerProfile Profile => profile ?? (profile = ProfileStore.Load());
    public static LevelCatalog Catalog => catalog != null ? catalog : (catalog = Resources.Load<LevelCatalog>("LevelCatalog"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { profile = null; catalog = null; }

    public static bool LoadLevel(int index)
    {
        if (Catalog == null || !Catalog.IsUnlocked(Profile, index)) return false;
        string path = Catalog.levels[index].scenePath;
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("Level is missing from the build: " + path);
            return false;
        }
        Time.timeScale = 1;
        SceneManager.LoadScene(path);
        return true;
    }

    public static bool CompleteCurrentLevel()
    {
        if (Catalog == null) return false;
        int index = Catalog.levels.FindIndex(level => level.scenePath == SceneManager.GetActiveScene().path);
        if (index < 0 || !Catalog.IsUnlocked(Profile, index)) return false;
        string id = Catalog.levels[index].id;
        bool alreadyCompleted = Profile.HasCompleted(id);
        Profile.Complete(id);
        if (ProfileStore.Save(Profile)) return true;
        if (!alreadyCompleted) Profile.completedLevelIds.Remove(id);
        return false;
    }

    public static void OpenMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(MenuScene);
    }
}
