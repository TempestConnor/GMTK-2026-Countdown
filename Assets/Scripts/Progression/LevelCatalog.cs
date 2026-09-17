using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [Serializable]
    public class Level
    {
        [Tooltip("Permanent save identifier. Do not change after release.")]
        public string id;
        public string title;
        [Tooltip("Full scene asset path, including .unity.")]
        public string scenePath;
    }

    [Tooltip("Drag entries to change progression order. First level is always unlocked.")]
    public List<Level> levels = new List<Level>();

    public bool IsUnlocked(PlayerProfile profile, int index)
    {
        if (index < 0 || index >= levels.Count) return false;
        // Previously completed levels remain replayable after a reorder.
        if (profile.HasCompleted(levels[index].id)) return true;
        for (int i = 0; i < index; i++)
            if (!profile.HasCompleted(levels[i].id)) return false;
        return true;
    }

    public int ContinueIndex(PlayerProfile profile)
    {
        for (int i = 0; i < levels.Count; i++)
            if (!profile.HasCompleted(levels[i].id)) return i;
        return levels.Count > 0 ? levels.Count - 1 : -1;
    }
}
