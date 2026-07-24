using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum DoorOrientation
{
    Horizontal,
    Vertical
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
    [Tooltip("Switches that must all be pressed for this door to open.")]
    [SerializeField] private List<Switch> switches = new List<Switch>();

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    [SerializeField] private bool _isOpen;
    public bool isOpen => _isOpen;

    private BoxCollider2D _collider;

    private void OnValidate()
    {
        ApplyShape();
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

        collider.size = size;
        collider.offset = size * 0.5f;

        if (visual != null)
        {
            visual.localPosition = new Vector3(size.x * 0.5f, size.y * 0.5f, 0);
            visual.localScale = new Vector3(size.x, size.y, 1);
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

        UpdateOpenState();
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

    private void UpdateOpenState()
    {
        bool open = AllSwitchesPressed();
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

    private bool AllSwitchesPressed()
    {
        if (switches.Count == 0) return false;

        foreach (var s in switches)
        {
            if (s == null || !s.isPressed) return false;
        }

        return true;
    }
}
