using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum DoorOrientation
{
    Horizontal,
    Vertical
}

public enum DoorSwitchCondition
{
    AllPressed,
    AllReleased
}

public enum DoorRetractEnd
{
    Start,
    End
}

[RequireComponent(typeof(BoxCollider2D))]
public class Door : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Axis the door spans multiple tiles along.")]
    [SerializeField] private DoorOrientation orientation = DoorOrientation.Vertical;

    [Tooltip("Length of the door in tiles (grid cells are 1 unit). Drive this instead of scaling the transform - the door's own position should stay at the tile-grid corner it starts from, so it keeps snapping cleanly regardless of length.")]
    [Min(1)]
    [SerializeField] private int lengthInTiles = 1;

    [Tooltip("Visual child kept in sync with lengthInTiles. Leave empty if this door has no separate visual child.")]
    [SerializeField] private Transform visual;

    [Header("Switches")]
    [Tooltip("Condition on the switches list that opens this door: AllPressed requires every switch pressed, AllReleased requires every switch not pressed.")]
    [SerializeField] private DoorSwitchCondition condition = DoorSwitchCondition.AllPressed;

    [Tooltip("Switches this door's open condition depends on.")]
    [SerializeField] private List<Switch> switches = new List<Switch>();

    [Header("Animation")]
    [Tooltip("Seconds the door takes to slide fully open. It stays solid (closed) until the slide finishes.")]
    [Min(0f)]
    [SerializeField] private float openDuration = 0.4f;

    [Tooltip("Seconds the door takes to slide shut. It becomes solid (closed) as soon as it starts closing.")]
    [Min(0f)]
    [SerializeField] private float closeDuration = 0.12f;

    [Tooltip("End the door slides into while opening: Start = bottom (vertical) / left (horizontal), End = top / right.")]
    [SerializeField] private DoorRetractEnd retractInto = DoorRetractEnd.End;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    [SerializeField] private bool _isOpen;
    public bool isOpen => _isOpen;

    public DoorSwitchCondition Condition => condition;
    public IReadOnlyList<Switch> Switches => switches;

    private BoxCollider2D _collider;

    // Visual slide progress, 0 = fully shut, 1 = fully retracted. Only reaching 1 opens the door.
    private float openAmount;
    private bool wantsOpen;

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Setting SpriteRenderer.size sends a message, which Unity disallows inside OnValidate.
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) ApplyShape(); };
#endif
    }

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        ApplyShape();
    }

    private void ApplyShape()
    {
        var collider = GetComponent<BoxCollider2D>();
        if (collider == null) return;

        Vector2 size = orientation == DoorOrientation.Vertical
            ? new Vector2(1, lengthInTiles)
            : new Vector2(lengthInTiles, 1);

        // The collider always covers the whole door; only the visual slides.
        collider.size = size;
        collider.offset = size * 0.5f;

        if (visual == null) return;

        var sprite = visual.GetComponent<SpriteRenderer>();
        int axis = orientation == DoorOrientation.Vertical ? 1 : 0;
        float shown = size[axis] * (1f - Mathf.SmoothStep(0f, 1f, openAmount));
        // Snap the sliding edge to whole pixels so the pixel art doesn't shimmer mid-slide.
        if (sprite != null && sprite.sprite != null)
            shown = Mathf.Round(shown * sprite.sprite.pixelsPerUnit) / sprite.sprite.pixelsPerUnit;

        Vector2 visualSize = size;
        visualSize[axis] = shown;
        Vector2 min = Vector2.zero;
        if (retractInto == DoorRetractEnd.End) min[axis] = size[axis] - shown;
        Vector2 centre = min + visualSize * 0.5f;
        visual.localPosition = new Vector3(centre.x, centre.y, 0);

        if (sprite == null)
        {
            visual.localScale = new Vector3(visualSize.x, visualSize.y, 1);
            return;
        }

        sprite.enabled = shown > 0f;
        if (shown <= 0f) return;

        // Tiled/sliced art is resized so its frame stays crisp; a simple sprite is stretched.
        if (sprite.drawMode != SpriteDrawMode.Simple)
        {
            visual.localScale = Vector3.one;
            sprite.size = visualSize;
        }
        else
        {
            visual.localScale = new Vector3(visualSize.x, visualSize.y, 1);
        }
    }

    private void OnEnable()
    {
        foreach (var s in switches)
        {
            if (s == null) continue;
            s.onPressed.AddListener(UpdateOpenState);
            s.onReleased.AddListener(UpdateOpenState);
        }

        // Start in the settled state instead of animating on load or re-enable.
        wantsOpen = EvaluateCondition();
        openAmount = wantsOpen ? 1f : 0f;
        ApplyShape();
        SetOpen(wantsOpen);
    }

    private void OnDisable()
    {
        foreach (var s in switches)
        {
            if (s == null) continue;
            s.onPressed.RemoveListener(UpdateOpenState);
            s.onReleased.RemoveListener(UpdateOpenState);
        }
    }

    private void Update()
    {
        Animate(Time.deltaTime);
    }

    private void UpdateOpenState()
    {
        wantsOpen = EvaluateCondition();

        // Anything short of fully open counts as closed, so the door is solid again the moment
        // it starts closing; opening only takes effect once the slide finishes (see Animate).
        if (!wantsOpen) SetOpen(false);
        Animate(0f);
    }

    private void Animate(float deltaTime)
    {
        float target = wantsOpen ? 1f : 0f;
        if (openAmount == target) return;

        float duration = wantsOpen ? openDuration : closeDuration;
        openAmount = duration > 0f
            ? Mathf.MoveTowards(openAmount, target, deltaTime / duration)
            : target;
        ApplyShape();

        if (wantsOpen && openAmount >= 1f) SetOpen(true);
    }

    private void SetOpen(bool open)
    {
        if (open == _isOpen) return;

        _isOpen = open;
        _collider.enabled = !open;

        if (open)
        {
            onOpened.Invoke();
        }
        else
        {
            onClosed.Invoke();
        }
    }

    private bool EvaluateCondition()
    {
        if (switches.Count == 0) return false;

        bool requirePressed = condition == DoorSwitchCondition.AllPressed;

        foreach (var s in switches)
        {
            if (s == null) return false;
            if (s.isPressed != requirePressed) return false;
        }

        return true;
    }
}
