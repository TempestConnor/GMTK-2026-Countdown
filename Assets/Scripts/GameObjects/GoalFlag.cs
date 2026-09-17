using UnityEngine;

/// <summary>A paintable level goal. Only the player's solid collider can activate it.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class GoalFlag : MonoBehaviour
{
    private bool completionPending;
    private bool completed;

    private void Reset()
    {
        var trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1f, 2f);
        trigger.offset = trigger.size * 0.5f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed || completionPending || other.isTrigger) return;
        var player = other.GetComponentInParent<playerController2>();
        if (player == null || !player.isActiveAndEnabled) return;
        // Never award the active level for a goal in a different loaded scene.
        if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
        completionPending = true;
    }

    private void LateUpdate()
    {
        if (!completionPending) return;
        completionPending = false;
        // Finish outside the physics callback, once even with multiple player colliders.
        if (!GameProgress.CompleteCurrentLevel())
        {
            Debug.LogError("Goal completion could not be saved. Check the level catalog, unlock order, and profile save path. Leave the flag and touch it again to retry.", this);
            return;
        }
        completed = true;
        GameProgress.OpenMenu();
    }

    private void OnDisable() => completionPending = false;
}
