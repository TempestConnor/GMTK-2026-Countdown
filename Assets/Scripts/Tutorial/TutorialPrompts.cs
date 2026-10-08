using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// On-screen tutorial text. One prompt shows at a time; prompts requested while another is
/// on screen wait in a queue. Each fades in, stays for its duration (or until dismissed), then
/// fades out. Markup is described in <see cref="TutorialPromptMarkup"/>.
///
/// The display is created on demand from Resources/TutorialPrompts.prefab in the active room
/// scene, so it (and its queue) resets with the room on travel or death. Edit that prefab to
/// restyle the text. All timing uses unscaled time, because banish aiming slows time down.
///
/// The prefab also holds the first-death prompt, shown when the room reloads after the
/// player's first death in any room. It is recorded in the saved player profile, so it shows
/// once per profile (until progress is reset), not once per room or play session.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPrompts : MonoBehaviour
{
    const string ResourcePath = "TutorialPrompts";
    const string FirstDeathId = "first-death";

    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CanvasGroup group;
    [Tooltip("Resolves [@Action] button prompts.")]
    [SerializeField] private InputActionAsset actions;

    [Header("Timing (real-time seconds)")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.5f;
    [Tooltip("Pause between one prompt fading out and the next queued one fading in.")]
    [SerializeField, Min(0f)] private float gapBetweenPrompts = 0.2f;

    [Header("Button prompts [LIKE_THIS]")]
    [Tooltip("TMP rich text for a button prompt; {0} is the label.")]
    [SerializeField, TextArea(2, 4)] private string keyFormat =
        "<mark=#FFFFFF2A padding=\"14,14,6,6\"><color=#FFE08A><b>{0}</b></color></mark>";

    [Header("Spooky text ~like this~")]
    [SerializeField] private Color spookyColor = new Color(0.72f, 1f, 0.9f, 1f);
    [Tooltip("Vertical bob, as a fraction of the font size.")]
    [SerializeField, Min(0f)] private float wiggleAmplitude = 0.06f;
    [SerializeField, Min(0f)] private float wiggleSpeed = 5f;
    [Tooltip("Random horizontal shiver, as a fraction of the font size.")]
    [SerializeField, Min(0f)] private float wiggleJitter = 0.025f;
    [Tooltip("Maximum tilt of each letter in degrees.")]
    [SerializeField, Min(0f)] private float wiggleTilt = 7f;

    [Header("First death (any room, once per profile)")]
    [Tooltip("Shown after the player's first death, before the room's own After Death prompts. Empty = none.")]
    [SerializeField, TextArea(2, 5)] private string firstDeathText = "~You died.~ As it turns out, slamming face first into a wall hurts.";
    [Tooltip("Real-time seconds on screen after fading in. 0 = stays until dismissed.")]
    [SerializeField, Min(0f)] private float firstDeathDuration = 4f;

    private sealed class Entry
    {
        public Object owner;
        public string markup;
        public float duration;
        public System.Action expired;
    }

    private enum State { Idle, FadingIn, Showing, FadingOut, Gap }

    private static TutorialPrompts instance;
    private readonly List<Entry> queue = new List<Entry>();
    private Entry current;
    private State state;
    private float timer;
    private bool dismissRequested;
    private bool hasSpooky;
    private TMP_MeshInfo[] restingVertices;

    /// <summary>The owner whose prompt is currently on screen, if any.</summary>
    public static Object CurrentOwner => instance != null ? instance.current?.owner : null;

    /// <summary>
    /// Shows <paramref name="markup"/> after any prompts already waiting. <paramref name="owner"/>
    /// identifies the prompt for <see cref="Dismiss"/>; a request from an owner that is already
    /// showing or queued is ignored. A duration of 0 stays until dismissed.
    /// <paramref name="expired"/> runs when the duration runs out (not when dismissed).
    /// </summary>
    public static void Show(Object owner, string markup, float duration, System.Action expired = null)
    {
        var display = GetOrCreate();
        if (display != null) display.Enqueue(owner, markup, duration, expired);
    }

    /// <summary>Fades out <paramref name="owner"/>'s prompt if it is showing, or drops it from the queue.</summary>
    public static void Dismiss(Object owner)
    {
        if (instance == null) return;
        instance.queue.RemoveAll(entry => entry.owner == owner);
        if (instance.current != null && instance.current.owner == owner) instance.dismissRequested = true;
    }

    /// <summary>Fades out whichever prompt is on screen; queued prompts follow as usual.</summary>
    public static void DismissCurrent()
    {
        if (instance != null && instance.current != null) instance.dismissRequested = true;
    }

    // Domain reload is disabled for Play mode, so statics must be reset by hand.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded runs before the room's Start methods, so this queues ahead of its After Death prompts.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!RoomTravel.ArrivedByRespawn || GameProgress.Profile.HasSeenTutorial(FirstDeathId)) return;
        // Stays marked in memory even if the save fails, so it still shows only once this session.
        GameProgress.MarkTutorialSeen(FirstDeathId);
        // Never queue on a display left over from the room being unloaded.
        if (instance != null && instance.gameObject.scene != scene) instance = null;
        var display = GetOrCreate();
        if (display != null && !string.IsNullOrEmpty(display.firstDeathText))
            display.Enqueue(display, display.firstDeathText, display.firstDeathDuration, null);
    }

    private static TutorialPrompts GetOrCreate()
    {
        if (instance != null) return instance;
        var prefab = Resources.Load<TutorialPrompts>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"Tutorial prompts need Resources/{ResourcePath}.prefab.");
            return null;
        }
        return Instantiate(prefab); // Awake assigns the instance.
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        group.alpha = 0f;
        text.text = string.Empty;
    }

    private void OnEnable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Enqueue(Object owner, string markup, float duration, System.Action expired)
    {
        if (current != null && current.owner == owner && !dismissRequested) return;
        if (queue.Exists(entry => entry.owner == owner)) return;
        queue.Add(new Entry { owner = owner, markup = markup, duration = Mathf.Max(0f, duration), expired = expired });
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        switch (state)
        {
            case State.Idle:
                if (queue.Count == 0) break;
                current = queue[0];
                queue.RemoveAt(0);
                dismissRequested = false;
                SetText(current.markup);
                state = State.FadingIn;
                break;

            case State.FadingIn:
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, delta / fadeInDuration);
                if (dismissRequested) state = State.FadingOut;
                else if (group.alpha >= 1f) { state = State.Showing; timer = 0f; }
                break;

            case State.Showing:
                timer += delta;
                if (dismissRequested) state = State.FadingOut;
                else if (current.duration > 0f && timer >= current.duration)
                {
                    state = State.FadingOut;
                    current.expired?.Invoke();
                }
                break;

            case State.FadingOut:
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, delta / fadeOutDuration);
                if (group.alpha > 0f) break;
                current = null;
                dismissRequested = false;
                SetText(string.Empty);
                state = State.Gap;
                timer = 0f;
                break;

            case State.Gap:
                timer += delta;
                if (timer >= gapBetweenPrompts) state = State.Idle;
                break;
        }
    }

    private void SetText(string markup)
    {
        string colorHex = ColorUtility.ToHtmlStringRGBA(spookyColor);
        text.text = TutorialPromptMarkup.ToRichText(markup, keyFormat, colorHex, ActionLabel);
        text.ForceMeshUpdate(); // Raises TEXT_CHANGED_EVENT, which caches the resting vertices.
    }

    private string ActionLabel(string token)
    {
        int slash = token.IndexOf('/');
        string actionName = slash >= 0 ? token.Substring(0, slash) : token;
        string part = slash >= 0 ? token.Substring(slash + 1) : null;
        var action = actions != null ? actions.FindAction(actionName) : null;
        if (action == null) Debug.LogWarning($"Tutorial prompt: no input action named '{actionName}'.", this);
        return TutorialKey.BindingLabel(action, part);
    }

    // TMP regenerates the mesh on text, size or resolution changes; refresh the wiggle's rest pose.
    private void OnTextChanged(Object changed)
    {
        if (changed != text) return;
        hasSpooky = false;
        var info = text.textInfo;
        for (int i = 0; i < info.linkCount; i++)
            if (info.linkInfo[i].GetLinkID() == TutorialPromptMarkup.SpookyLinkId) hasSpooky = true;
        restingVertices = hasSpooky ? info.CopyMeshInfoVertexData() : null;
    }

    private void LateUpdate()
    {
        if (!hasSpooky || restingVertices == null || group.alpha <= 0f) return;

        var info = text.textInfo;
        float time = Time.unscaledTime;
        float bob = wiggleAmplitude * text.fontSize;
        float jitter = wiggleJitter * text.fontSize;

        for (int l = 0; l < info.linkCount; l++)
        {
            var link = info.linkInfo[l];
            if (link.GetLinkID() != TutorialPromptMarkup.SpookyLinkId) continue;
            int last = Mathf.Min(link.linkTextfirstCharacterIndex + link.linkTextLength, info.characterCount);
            for (int c = link.linkTextfirstCharacterIndex; c < last; c++)
            {
                var character = info.characterInfo[c];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material >= restingVertices.Length || vertex + 3 >= restingVertices[material].vertices.Length) continue;

                var source = restingVertices[material].vertices;
                var target = info.meshInfo[material].vertices;
                Vector3 center = (source[vertex] + source[vertex + 2]) * 0.5f;
                var offset = new Vector3(
                    (Mathf.PerlinNoise(time * wiggleSpeed * 0.8f, c * 1.7f) - 0.5f) * 2f * jitter,
                    Mathf.Sin(time * wiggleSpeed + c * 0.6f) * bob,
                    0f);
                var tilt = Quaternion.Euler(0f, 0f, Mathf.Sin(time * wiggleSpeed * 0.7f + c * 1.3f) * wiggleTilt);
                for (int v = 0; v < 4; v++)
                    target[vertex + v] = center + offset + tilt * (source[vertex + v] - center);
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}
