using System;
using UnityEngine;

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

    }
    private Checkpoint respawnPoint;
    public void Respawn()
    {
        
    }

}
