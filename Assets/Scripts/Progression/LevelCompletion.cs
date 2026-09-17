using UnityEngine;

/// <summary>Scene-level hook for a win condition or UnityEvent.</summary>
public class LevelCompletion : MonoBehaviour
{
    private bool completed;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            ReturnToMenu();
    }

    public void CompleteLevel()
    {
        if (completed) return;
        if (!GameProgress.CompleteCurrentLevel())
        {
            Debug.LogError("Completion could not be saved. Check the catalog and profile save path.");
            return;
        }
        completed = true;
        GameProgress.OpenMenu();
    }

    public void ReturnToMenu() => GameProgress.OpenMenu();
}
