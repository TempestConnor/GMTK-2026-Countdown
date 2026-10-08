using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// Edit-mode checks for the tutorial prompt system: markup conversion, the display prefab's
// wiring, the zone prefabs, and that the zones are not on the EntityPalette.
public static class ValidateTutorialPrompts
{
    public static string Main()
    {
        var failures = new List<string>();
        void Check(bool ok, string message) { if (!ok) failures.Add(message); }

        const string key = "<k>{0}</k>";
        Func<string, string> label = token => "B:" + token;
        void Markup(string input, string expected)
        {
            string actual = TutorialPromptMarkup.ToRichText(input, key, null, label);
            Check(actual == expected, $"Markup '{input}' -> '{actual}', expected '{expected}'");
        }
        Markup("Hold [RIGHT_CLICK] to Aim, Press [LEFT_CLICK] to Banish",
            "Hold <k>RIGHT CLICK</k> to Aim, Press <k>LEFT CLICK</k> to Banish");
        Markup("~Boo~ [@Aim] [@Move/left]",
            "<link=\"spooky\">Boo</link> <k>B:Aim</k> <k>B:Move/left</k>");
        Markup("~unclosed", "<link=\"spooky\">unclosed</link>");
        Markup(@"\~ \[x] \\ [] [", @"~ [x] \ [] [");
        Check(TutorialPromptMarkup.ToRichText("~a~", key, "FF0000FF", label) == "<color=#FF0000FF><link=\"spooky\">a</link></color>",
            "Spooky colour wrap");

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/playerActions.inputactions");
        Check(TutorialKey.BindingLabel(actions.FindAction("Aim"), null) == "RMB", "Aim binding label should be RMB");
        Check(TutorialKey.BindingLabel(actions.FindAction("Fire"), null) == "LMB", "Fire binding label should be LMB");

        var display = AssetDatabase.LoadAssetAtPath<TutorialPrompts>("Assets/Resources/TutorialPrompts.prefab");
        Check(display != null, "Resources/TutorialPrompts.prefab missing");
        if (display != null)
        {
            var data = new SerializedObject(display);
            var text = data.FindProperty("text").objectReferenceValue as TextMeshProUGUI;
            var group = data.FindProperty("group").objectReferenceValue as CanvasGroup;
            Check(text != null && text.transform.IsChildOf(display.transform), "Display text not wired");
            Check(group != null && text != null && text.transform.IsChildOf(group.transform), "Display canvas group not wired");
            Check(data.FindProperty("actions").objectReferenceValue == actions, "Display input actions not wired");
            Check(display.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay, "Display canvas must be overlay");
            Check(group != null && group.alpha == 0f, "Display should start hidden");
        }

        foreach (var path in new[] { "Assets/Prefabs/Tutorial/TutorialPromptZone.prefab", "Assets/Prefabs/Tutorial/TutorialDismissZone.prefab", "Assets/Prefabs/Tutorial/TutorialTriggerZone.prefab" })
        {
            var zone = AssetDatabase.LoadAssetAtPath<TutorialZone>(path);
            Check(zone != null, path + " missing");
            if (zone == null) continue;
            var box = zone.GetComponent<BoxCollider2D>();
            Check(box.isTrigger && box.size == zone.areaSize && box.offset == zone.areaSize / 2, path + " collider not bottom-left trigger");
            Check(zone.gameObject.layer == 0, path + " should be on Default (detects both planes)");
        }

        var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Palettes/EntityPalette.prefab");
        Check(palette.GetComponentsInChildren<TutorialZone>(true).Length == 0, "Tutorial zones must not be on EntityPalette");

        return failures.Count == 0 ? "All tutorial prompt checks passed." : "FAILED:\n" + string.Join("\n", failures);
    }
}
