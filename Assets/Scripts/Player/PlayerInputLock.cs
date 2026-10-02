using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Dispose each owner's lease to unlock. Does not freeze physics or cancel abilities.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputLock : MonoBehaviour
{
    private readonly HashSet<Lease> leases = new HashSet<Lease>();
    private readonly List<InputAction> enabledActions = new List<InputAction>();
    private PlayerInput input;
    public bool IsLocked => leases.Count != 0;
    public event Action Locked;

    public IDisposable Acquire()
    {
        var lease = new Lease(this);
        bool wasLocked = IsLocked;
        leases.Add(lease);
        if (!wasLocked)
        {
            input = GetComponent<PlayerInput>();
            enabledActions.Clear();
            foreach (var action in input.actions)
                if (action.enabled) enabledActions.Add(action);
            input.actions.Disable();
            Locked?.Invoke();
        }
        return lease;
    }

    private void Release(Lease lease)
    {
        if (!leases.Remove(lease) || IsLocked) return;
        if (input != null && input.isActiveAndEnabled)
            foreach (var action in enabledActions) action.Enable();
        enabledActions.Clear();
    }

    private sealed class Lease : IDisposable
    {
        private PlayerInputLock owner;
        public Lease(PlayerInputLock owner) { this.owner = owner; }
        public void Dispose()
        {
            if (owner != null) owner.Release(this);
            owner = null;
        }
    }
}
