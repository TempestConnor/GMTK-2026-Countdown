using UnityEngine;

[CreateAssetMenu(fileName = "BanishStats", menuName = "Player/Banish Stats")]
public class BanishStats : ScriptableObject
{
    [Tooltip("How long (in seconds) right-click must be held before the targeting circle is armed and ready to fire.")]
    public float armTime = 0.5f;

    [Tooltip("Radius (in world units) the targeting circle grows to once fully armed.")]
    public float targetRadius = 3f;

    [Tooltip("How long (in seconds) banished objects stay on plane B before automatically returning to plane A.")]
    public float returnDelay = 3f;
}
