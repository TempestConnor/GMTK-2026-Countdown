using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Builds the on-screen tutorial prompt display (Resources/TutorialPrompts.prefab) and the
// TutorialPromptZone / TutorialDismissZone / TutorialTriggerZone prefabs. The zones are deliberately not on the
// EntityPalette. Existing prefabs are left alone so hand restyling survives. Idempotent.
public static class SetupTutorialPrompts
{
    const string DisplayPath = "Assets/Resources/TutorialPrompts.prefab";
    const string ZoneFolder = "Assets/Prefabs/Tutorial";
    const string PromptZonePath = ZoneFolder + "/TutorialPromptZone.prefab";
    const string DismissZonePath = ZoneFolder + "/TutorialDismissZone.prefab";
    const string TriggerZonePath = ZoneFolder + "/TutorialTriggerZone.prefab";
    const string ActionsPath = "Assets/Settings/playerActions.inputactions";
    const string ShadowMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat";

    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        if (!AssetDatabase.IsValidFolder(ZoneFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Tutorial");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath) == null) BuildDisplay();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PromptZonePath) == null) BuildZone<TutorialPromptZone>(PromptZonePath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DismissZonePath) == null) BuildZone<TutorialDismissZone>(DismissZonePath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TriggerZonePath) == null) BuildZone<TutorialTriggerZone>(TriggerZonePath);
        AssetDatabase.SaveAssets();
        return "Tutorial prompt prefabs ready.";
    }

    static void BuildDisplay()
    {
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath) ?? throw new Exception("playerActions missing.");
        var shadow = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath) ?? throw new Exception("Drop Shadow material missing.");

        var root = new GameObject("TutorialPrompts", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above gameplay UI, below the room fade curtain
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var prompt = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup));
            prompt.transform.SetParent(root.transform, false);
            var promptRect = (RectTransform)prompt.transform;
            promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0, 120);
            promptRect.sizeDelta = new Vector2(1500, 220);
            var group = prompt.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(prompt.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSharedMaterial = shadow;
            text.fontSize = 46;
            text.color = new Color(0.96f, 0.93f, 0.86f);
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = string.Empty;

            var display = root.AddComponent<TutorialPrompts>();
            var data = new SerializedObject(display);
            data.FindProperty("text").objectReferenceValue = text;
            data.FindProperty("group").objectReferenceValue = group;
            data.FindProperty("actions").objectReferenceValue = actions;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DisplayPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BuildZone<T>(string path) where T : TutorialZone
    {
        var root = new GameObject(typeof(T).Name);
        try
        {
            root.AddComponent<T>().ConfigureArea();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
