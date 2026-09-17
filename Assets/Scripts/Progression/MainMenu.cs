using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private Transform content;
    private Font font;
    private Text status;
    private bool selecting;

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvas.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.10f);

        var viewport = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(background.transform, false);
        var rect = viewport.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.18f, 0.06f);
        rect.anchorMax = new Vector2(0.82f, 0.94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var body = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content = body.transform;
        content.SetParent(viewport.transform, false);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 1);
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0.5f, 1);
        bodyRect.sizeDelta = Vector2.zero;
        var layout = body.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.content = bodyRect;
        scroll.viewport = rect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Show(false);
    }

    private void Show(bool levels)
    {
        selecting = levels;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Label(levels ? "SELECT LEVEL" : "BETWEEN PLANES", 38, 65);
        Label("PROFILE  /  " + GameProgress.Profile.displayName, 18, 30);
        var catalog = GameProgress.Catalog;
        int total = catalog == null ? 0 : catalog.levels.Count;
        int completed = catalog == null ? 0 : catalog.levels.FindAll(l => GameProgress.Profile.HasCompleted(l.id)).Count;
        Label(completed + " / " + total + " levels completed", 18, 30);
        if (levels && catalog != null)
        {
            for (int i = 0; i < total; i++)
            {
                int index = i;
                var level = catalog.levels[i];
                bool unlocked = catalog.IsUnlocked(GameProgress.Profile, i);
                string suffix = GameProgress.Profile.HasCompleted(level.id) ? "  /  COMPLETED" : unlocked ? "  /  AVAILABLE" : "  /  LOCKED";
                AddButton((i + 1).ToString("00") + "   " + level.title + suffix, () => Play(index), unlocked);
            }
            AddButton("Back", () => Show(false));
        }
        else
        {
            AddButton(completed == 0 ? "Play" : completed == total ? "Replay" : "Continue", () => Play(catalog.ContinueIndex(GameProgress.Profile)), total > 0);
            AddButton("Level Select", () => Show(true), total > 0);
            AddButton("Player Profile", ShowProfile);
            AddButton("Quit", Application.Quit);
        }
        status = Label(total == 0 ? "No levels configured." : "", 16, 35);
        Canvas.ForceUpdateCanvases();
        content.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        var first = content.GetComponentInChildren<Button>();
        if (first != null) first.Select();
    }

    private void ShowProfile()
    {
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Label("PLAYER PROFILE", 38, 65);
        Label("Player name", 18, 30);
        var fieldObject = new GameObject("Player Name", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        fieldObject.transform.SetParent(content, false);
        fieldObject.GetComponent<LayoutElement>().preferredHeight = 54;
        fieldObject.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.25f);
        var text = Label("", 22, 54);
        text.transform.SetParent(fieldObject.transform, false);
        Stretch(text.rectTransform);
        var field = fieldObject.GetComponent<InputField>();
        field.textComponent = text;
        field.characterLimit = 24;
        field.text = GameProgress.Profile.displayName;
        AddButton("Save name", () =>
        {
            string name = field.text.Trim();
            if (name.Length == 0) { status.text = "Enter a player name."; return; }
            GameProgress.Profile.displayName = name;
            status.text = ProfileStore.Save(GameProgress.Profile) ? "Profile saved." : "Could not save profile. Please try again.";
        });
        AddButton("Reset progress", () =>
        {
            status.text = "Reset all completed levels?";
            AddButton("Confirm reset", () =>
            {
                var old = GameProgress.Profile.completedLevelIds;
                GameProgress.Profile.completedLevelIds = new System.Collections.Generic.List<string>();
                if (ProfileStore.Save(GameProgress.Profile)) Show(false);
                else { GameProgress.Profile.completedLevelIds = old; status.text = "Reset could not be saved."; }
            });
        });
        AddButton("Back", () => Show(selecting));
        status = Label("Progress saves automatically when you finish a level.", 16, 45);
        field.Select();
    }

    private void Play(int index)
    {
        if (!GameProgress.LoadLevel(index)) status.text = "Unable to load level. Check the build scene list.";
    }

    private Text Label(string value, int size, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.86f, 0.94f, 1);
        text.alignment = TextAnchor.MiddleCenter;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private void AddButton(string title, UnityEngine.Events.UnityAction action, bool enabled = true)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<Image>().color = new Color(0.10f, 0.32f, 0.40f);
        go.GetComponent<LayoutElement>().preferredHeight = 56;
        var button = go.GetComponent<Button>();
        button.interactable = enabled;
        button.onClick.AddListener(action);
        var label = Label(title, 22, 56);
        label.transform.SetParent(go.transform, false);
        Stretch(label.rectTransform);
        label.raycastTarget = false;
        if (!enabled) label.color = Color.gray;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
