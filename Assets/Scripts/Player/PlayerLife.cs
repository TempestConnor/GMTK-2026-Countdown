using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLife : MonoBehaviour
{
    private playerController2 playerController;
    private Collider2D bodyCollider;

    private void Awake()
    {
        playerController = GetComponent<playerController2>();
        foreach (var candidate in GetComponents<Collider2D>())
        {
            if (candidate.isTrigger) continue;
            bodyCollider = candidate;
            break;
        }
    }

    private void LateUpdate()
    {
        if (!isAlive || RoomTravel.IsLoading) return;

        var gameplayCamera = Camera.main;
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) return;

        // Use the body center so sprite animation and plane visibility do not affect death.
        Vector3 position = bodyCollider != null && bodyCollider.enabled
            ? bodyCollider.bounds.center
            : transform.position;
        Vector3 viewport = gameplayCamera.WorldToViewportPoint(position);
        // LevelTemplate uses one world unit per tile: a fixed four-tile strip on every edge.
        Vector3 lower = gameplayCamera.ViewportToWorldPoint(new Vector3(0, 0, viewport.z));
        Vector3 upper = gameplayCamera.ViewportToWorldPoint(new Vector3(1, 1, viewport.z));
        float marginX = 4f / Mathf.Max(0.001f, Mathf.Abs(upper.x - lower.x));
        float marginY = 4f / Mathf.Max(0.001f, Mathf.Abs(upper.y - lower.y));
        if (viewport.z <= 0f || viewport.x < -marginX || viewport.x > 1f + marginX ||
            viewport.y < -marginY || viewport.y > 1f + marginY)
        {
            Kill();
        }
    }

    public enum LifeState
    {
        Alive,
        Dead
    }

    private LifeState currentState = LifeState.Alive;
    public LifeState CurrentState => currentState;
    public bool isAlive => currentState == LifeState.Alive;

    public void Kill()
    {
        if(!isAlive || RoomTravel.IsLoading)
        {
            return;
        }
        currentState = LifeState.Dead;
        // Keep non-hazard deaths (such as leaving the camera) in the shared life state.
        if (TryGetComponent<Damageable>(out var damageable) && !damageable.IsDead)
            damageable.Kill();
        playerController.onPlayerDeath();

        Respawn();

    }
    public void Respawn()
    {
        if(!Application.isPlaying) return;

        Scene scene = gameObject.scene;

        if(scene.buildIndex < 0)
        {
            Debug.LogError("PlayerLife.Respawn: Scene is not valid. Cannot respawn player.", this);
            return;
        }

        RoomTravel.Respawn(this);
    }

    

}
