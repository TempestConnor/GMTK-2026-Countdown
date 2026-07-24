using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Switch : MonoBehaviour
{
    [Tooltip("Layers that can press this switch (e.g. Player, Entities).")]
    [SerializeField] private LayerMask activatorLayers;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    [SerializeField] private bool _isPressed;
    public bool isPressed => _isPressed;

    private readonly HashSet<Collider2D> touching = new HashSet<Collider2D>();

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
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
