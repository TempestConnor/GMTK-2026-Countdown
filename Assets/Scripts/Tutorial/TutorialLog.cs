using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bottom-right history of the tutorial prompts shown so far, newest at the bottom, in a
/// scroll view toggled by an icon in the corner (hidden until first opened). A prompt is logged when it starts fading in;
/// showing the same text again (e.g. after a respawn) moves it to the bottom instead of
/// repeating it. New entries scroll the log to the bottom; while it is hidden they light the
/// icon's unread marker instead.
///
/// The history and the shown/hidden choice are static, so they survive room travel and death
/// even though the display is rebuilt with each room. The history is cleared with the room
/// session (starting a level or returning to the menu). Lives on Resources/TutorialPrompts.prefab
/// next to <see cref="TutorialPrompts"/>, which resolves [@Action] bindings. Markup is described
/// in <see cref="TutorialPromptMarkup"/>; spooky text is coloured but does not wiggle here.
///
/// Rooms have no EventSystem of their own, so the prefab carries one for clicking and scrolling.
/// Its UI module has no Navigate/Submit actions, so gameplay keys never drive the log.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TutorialPrompts))]
public sealed class TutorialLog : MonoBehaviour
{
    const int MaxHistory = 50;

    [Tooltip("Box above the toggle, hidden while the log is toggled off or empty. Its height fits the text up to Max Height.")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("The scroll view's content, sized to the text by a ContentSizeFitter. Its margins pad it from the panel edge.")]
    [SerializeField] private TextMeshProUGUI text;
    [Tooltip("Tallest the panel grows, in canvas pixels; beyond it the log scrolls.")]
    [SerializeField, Min(0f)] private float maxHeight = 320f;

    [Header("Toggle")]
    [Tooltip("Corner icon that shows and hides the panel. Hidden while the log is empty.")]
    [SerializeField] private Button toggle;
    [Tooltip("Shown on the toggle when entries arrive while the panel is hidden.")]
    [SerializeField] private GameObject unreadMarker;
    [Tooltip("Disabled if the room already has an EventSystem.")]
    [SerializeField] private EventSystem eventSystem;

    [Header("Formatting")]
    [Tooltip("TMP rich text for one entry; {0} is the bullet and {1} the message.")]
    [SerializeField, TextArea(2, 4)] private string entryFormat = "{0}<indent=1em>{1}</indent>";
    [Tooltip("Bullet of the newest entry.")]
    [SerializeField] private string newestBullet = "<color=#FFE08A>></color>";
    [Tooltip("Bullet of older entries.")]
    [SerializeField] private string olderBullet = "<color=#FFFFFF55>></color>";
    [Tooltip("TMP rich text for a button prompt; {0} is the label.")]
    [SerializeField, TextArea(2, 4)] private string keyFormat = "<color=#FFE08A><b>{0}</b></color>";
    [SerializeField] private Color spookyColor = new Color(0.72f, 1f, 0.9f, 1f);

    private static readonly List<string> history = new List<string>();
    private static readonly List<TutorialLog> enabledLogs = new List<TutorialLog>();
    private static event System.Action Changed;
    private static bool shown; // hidden until the player opens it
    private static bool unread;
    private TutorialPrompts prompts;

    public static bool HasEntries => history.Count > 0;

    /// <summary>Adds <paramref name="markup"/> as the newest entry.</summary>
    public static void Add(string markup)
    {
        if (string.IsNullOrEmpty(markup)) return;
        history.Remove(markup);
        history.Add(markup);
        if (history.Count > MaxHistory) history.RemoveRange(0, history.Count - MaxHistory);
        if (!shown) unread = true;
        Changed?.Invoke();
    }

    public static void Clear()
    {
        history.Clear();
        unread = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// True when <paramref name="screenPosition"/> is over the log or its toggle, so gameplay
    /// should ignore mouse clicks there.
    /// </summary>
    public static bool IsPointerOver(Vector2 screenPosition)
    {
        foreach (var log in enabledLogs)
            if (log.Contains(log.toggle.transform, screenPosition) || log.Contains(log.panel, screenPosition))
                return true;
        return false;
    }

    // Domain reload is disabled for Play mode, so statics must be reset by hand.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        history.Clear();
        enabledLogs.Clear();
        Changed = null;
        shown = false;
        unread = false;
    }

    private void Awake()
    {
        prompts = GetComponent<TutorialPrompts>();
        toggle.onClick.AddListener(Toggle);
        if (eventSystem != null && FindObjectsByType<EventSystem>().Length > 1)
            eventSystem.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        enabledLogs.Add(this);
        Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        enabledLogs.Remove(this);
        Changed -= Refresh;
    }

    private void Toggle()
    {
        shown = !shown;
        if (shown) unread = false;
        Refresh();
    }

    private bool Contains(Transform target, Vector2 screenPosition) =>
        target.gameObject.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)target, screenPosition, null);

    private void Refresh()
    {
        bool hasEntries = HasEntries;
        toggle.gameObject.SetActive(hasEntries);
        unreadMarker.SetActive(hasEntries && unread);
        panel.gameObject.SetActive(hasEntries && shown);
        if (!panel.gameObject.activeSelf) return;

        string spookyHex = ColorUtility.ToHtmlStringRGBA(spookyColor);
        var result = new StringBuilder();
        for (int i = 0; i < history.Count; i++)
        {
            if (i > 0) result.Append('\n');
            string message = TutorialPromptMarkup.ToRichText(history[i], keyFormat, spookyHex, prompts.ActionLabel);
            result.AppendFormat(entryFormat, i == history.Count - 1 ? newestBullet : olderBullet, message);
        }
        text.text = result.ToString();

        // Fit the panel while everything fits; past Max Height the scroll view takes over.
        // The content spans the panel's width, so its preferred height is already for that width.
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Min(text.preferredHeight, maxHeight));

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
