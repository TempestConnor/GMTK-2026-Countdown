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
        if (!isAlive) return;

        var gameplayCamera = Camera.main;
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) return;

        // Use the body center so sprite animation and plane visibility do not affect death.
        Vector3 position = bodyCollider != null && bodyCollider.enabled
            ? bodyCollider.bounds.center
            : transform.position;
        Vector3 viewport = gameplayCamera.WorldToViewportPoint(position);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
            viewport.y < 0f || viewport.y > 1f)
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
        if(!isAlive)
        {
            return;
        }
        currentState = LifeState.Dead;
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

        SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
    }

    

}
