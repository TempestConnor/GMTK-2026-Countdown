using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLife : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private playerController2 playerController;
    private void Awake()
    {
        playerController = GetComponent<playerController2>();
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
