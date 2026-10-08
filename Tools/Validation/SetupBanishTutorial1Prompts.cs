using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Places the on-screen banish tutorial in Level01_BanishTutorial1 under LevelTemplate > Entities:
//   Zone A (spawn area)      -> "Hold [@Aim] to Aim." until Aim is held, then the banish prompt.
//   First successful banish  -> "Banished objects return..." (replaces the first two).
//   Zone B (past the door)   -> dismisses the first two.
//   Zone C (block-top ledge) -> after 30 real seconds, "BANISH YOURSELF" for 5 s.
//   Respawn after a death    -> "Slamming your face into a wall kills you" first.
// Zones are prefab instances of Assets/Prefabs/Tutorial/*. Re-running replaces them (and removes
// the retired Trigger_Return_A1). Edit mode only.
public static class SetupBanishTutorial1Prompts
{
    const string ScenePath = "Assets/Scenes/Level01/Level01_BanishTutorial1.unity";
    const string PromptZonePath = "Assets/Prefabs/Tutorial/TutorialPromptZone.prefab";
    const string DismissZonePath = "Assets/Prefabs/Tutorial/TutorialDismissZone.prefab";

    static readonly Vector2Int ZoneACell = new Vector2Int(-15, -8);
    static readonly Vector2 ZoneASize = new Vector2(8, 4);
    static readonly Vector2Int ZoneBCell = new Vector2Int(-4, -8);
    static readonly Vector2 ZoneBSize = new Vector2(10, 4);
    static readonly Vector2Int ZoneCCell = new Vector2Int(6, -4);
    static readonly Vector2 ZoneCSize = new Vector2(4, 5);

    static readonly string[] Names = { "Prompt_Aim", "Prompt_Banish", "Prompt_Return", "Trigger_Return_A1", "Dismiss_B", "Prompt_BanishYourself", "Prompt_Death" };

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var level = GameObject.Find("LevelTemplate") ?? throw new Exception("LevelTemplate missing.");
        var entities = level.transform.Find("Entities") ?? throw new Exception("LevelTemplate/Entities missing.");
        var grid = level.transform.Find("Ground").GetComponent<Tilemap>();

        foreach (var name in Names)
        {
            var old = entities.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        var aim = Prompt("Prompt_Aim", ZoneACell, ZoneASize);
        aim.text = "Hold [@Aim] to Aim.";
        aim.duration = 0f;
        aim.completeAction = "Aim";
        aim.completeHoldTime = 0.25f;

        var banish = Prompt("Prompt_Banish", ZoneACell, ZoneASize);
        banish.text = "Highlighted objects can be ~Banished~ by pressing [@Fire].";
        banish.duration = 0f;
        banish.showOnEnter = false;
        aim.thenShow = new List<TutorialPromptZone> { banish };

        var giveBack = Prompt("Prompt_Return", ZoneACell, ZoneASize);
        giveBack.text = "~Banished~ objects return after a few seconds, or after pressing [@Fire].";
        giveBack.duration = 6f;
        giveBack.showOnEnter = false;
        giveBack.showOnBanish = true;
        giveBack.replaces = new List<TutorialPromptZone> { aim, banish };

        var dismissB = Instantiate<TutorialDismissZone>(DismissZonePath, "Dismiss_B", ZoneBCell, ZoneBSize);
        dismissB.prompts = new List<TutorialPromptZone> { aim, banish };

        var yourself = Prompt("Prompt_BanishYourself", ZoneCCell, ZoneCSize);
        yourself.text = "~BANISH YOURSELF~";
        yourself.delay = 30f;
        yourself.duration = 5f;

        var death = Prompt("Prompt_Death", ZoneACell, ZoneASize);
        death.text = "Slamming your face into a wall ~kills~ you";
        death.duration = 4f;
        death.showOnEnter = false;
        death.showAfterDeath = true;

        foreach (var zone in new TutorialZone[] { aim, banish, giveBack, dismissB, yourself, death })
        {
            zone.ConfigureArea();
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone.GetComponent<BoxCollider2D>());
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Banish tutorial prompts placed in " + scene.name;

        TutorialPromptZone Prompt(string name, Vector2Int cell, Vector2 size) =>
            Instantiate<TutorialPromptZone>(PromptZonePath, name, cell, size);

        T Instantiate<T>(string prefabPath, string name, Vector2Int cell, Vector2 size) where T : TutorialZone
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new Exception(prefabPath + " missing.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, entities);
            go.name = name;
            go.transform.position = grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0));
            var zone = go.GetComponent<T>();
            zone.areaSize = size;
            return zone;
        }
    }
}
