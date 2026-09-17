using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    public int version = 1;
    public string displayName = "Player";
    public List<string> completedLevelIds = new List<string>();

    public bool HasCompleted(string id) => completedLevelIds.Contains(id);
    public void Complete(string id)
    {
        if (!string.IsNullOrEmpty(id) && !HasCompleted(id)) completedLevelIds.Add(id);
    }
}

public static class ProfileStore
{
    public static string SavePath => Path.Combine(Application.persistentDataPath, "player-profile.json");

    public static PlayerProfile Load()
    {
        foreach (string path in new[] { SavePath, SavePath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                if (profile == null || profile.version != 1 || profile.completedLevelIds == null)
                    throw new InvalidDataException("Unsupported or invalid profile.");
                return profile;
            }
            catch (Exception e) { Debug.LogWarning("Could not load profile: " + e.Message); }
        }
        return new PlayerProfile();
    }

    public static bool Save(PlayerProfile profile)
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(profile, true));
            if (File.Exists(SavePath)) File.Replace(temporary, SavePath, SavePath + ".bak");
            else File.Move(temporary, SavePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Could not save player profile: " + e.Message);
            return false;
        }
    }
}
