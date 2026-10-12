using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Switch : MonoBehaviour
{
    [Tooltip("Layers that can press this switch (e.g. Player, Entities).")]
    [SerializeField] private LayerMask activatorLayers;

    [Header("Animation")]
    [Tooltip("Visual child that sinks while pressed. Leave empty for no press animation.")]
    [SerializeField] private Transform visual;

    [Tooltip("Pressed height of the visual as a fraction of its resting height. Its bottom edge stays put.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float pressedHeight = 0.5f;

    [Tooltip("Seconds the visual takes to sink when pressed or spring back when released.")]
    [Min(0f)]
    [SerializeField] private float pressDuration = 0.08f;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    [SerializeField] private bool _isPressed;
    public bool isPressed => _isPressed;

    private readonly HashSet<Collider2D> touching = new HashSet<Collider2D>();

    private Vector3 restPosition;
    private Vector3 restScale;
    private float restHalfHeight;
    private float pressAmount;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (visual == null) return;

        restPosition = visual.localPosition;
        restScale = visual.localScale;
        var sprite = visual.GetComponent<SpriteRenderer>();
        restHalfHeight = sprite != null && sprite.sprite != null
            ? sprite.sprite.bounds.extents.y * restScale.y
            : restScale.y * 0.5f;
    }

    // Purely visual: isPressed and the events switch immediately, regardless of the animation.
    private void Update()
    {
        if (visual == null) return;

        float target = _isPressed ? 1f : 0f;
        if (pressAmount == target) return;

        pressAmount = pressDuration > 0f
            ? Mathf.MoveTowards(pressAmount, target, Time.deltaTime / pressDuration)
            : target;

        // Squash along the visual's own up axis, shifting it down so the bottom edge stays put.
        float height = Mathf.Lerp(1f, pressedHeight, pressAmount);
        visual.localScale = new Vector3(restScale.x, restScale.y * height, restScale.z);
        visual.localPosition = restPosition - visual.localRotation * Vector3.up * (restHalfHeight * (1f - height));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsActivator(other)) return;

        touching.Add(other);
        UpdatePressedState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (touching.Remove(other))
        {
            UpdatePressedState();
        }
    }

    private bool IsActivator(Collider2D other)
    {
        return (activatorLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void UpdatePressedState()
    {
        touching.RemoveWhere(c => c == null);

        bool pressed = touching.Count > 0;
        if (pressed == _isPressed) return;

        _isPressed = pressed;
        if (pressed)
        {
            onPressed.Invoke();
        }
        else
        {
            onReleased.Invoke();
        }
    }
}
