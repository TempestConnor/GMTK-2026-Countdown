using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class playerController2
{
    public void onAttack(InputAction.CallbackContext context)
    {
        if (context.started && canAttack)
        {
            canAttack = false;
            animator.SetTrigger("attack");
            StartCoroutine(attackTimer());
        }
    }

    private IEnumerator attackTimer()
    {
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }
}
