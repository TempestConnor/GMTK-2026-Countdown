using System.Collections.Generic;
using UnityEngine;

public interface IPlayerAction { void Cancel(); }
public interface IPlayerInputState { void ClearInput(); }

/// <summary>Register each action once; callers never need a list of individual abilities.</summary>
[DisallowMultipleComponent]
public sealed class PlayerActionCancellation : MonoBehaviour
{
    private readonly List<IPlayerAction> actions = new List<IPlayerAction>();
    public void Register(IPlayerAction action)
    {
        if (action != null && !actions.Contains(action)) actions.Add(action);
    }
    public void Unregister(IPlayerAction action) => actions.Remove(action);
    public void ClearInputState()
    {
        foreach (var action in actions.ToArray())
            if (action is IPlayerInputState state) state.ClearInput();
    }
    public void CancelAll(IPlayerAction except = null)
    {
        foreach (var action in actions.ToArray())
            if (!ReferenceEquals(action, except)) action.Cancel();
    }
}
