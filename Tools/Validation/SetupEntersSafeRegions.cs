using UnityEditor;
using UnityEngine;

/// <summary>Edit mode: adds EntersSafeRegions to the Box prefab; SafeBox, a Box variant, inherits it.</summary>
public static class SetupEntersSafeRegions
{
    public static string Main()
    {
        const string boxPath = "Assets/Prefabs/Entities/Box.prefab", safePath = "Assets/Prefabs/Entities/SafeBox.prefab";
        var root = PrefabUtility.LoadPrefabContents(boxPath);
        bool added = false;
        try
        {
            if (root.GetComponent<EntersSafeRegions>() == null) { root.AddComponent<EntersSafeRegions>(); added = true; }
            if (added) PrefabUtility.SaveAsPrefabAsset(root, boxPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        bool box = AssetDatabase.LoadAssetAtPath<GameObject>(boxPath).GetComponent<EntersSafeRegions>() != null;
        bool safe = AssetDatabase.LoadAssetAtPath<GameObject>(safePath).GetComponent<EntersSafeRegions>() != null;
        if (!box || !safe) throw new System.Exception($"FAILED: Box has component={box}, SafeBox inherits={safe}");
        return (added ? "Added" : "Already present") + ": Box and SafeBox carry EntersSafeRegions";
    }
}
