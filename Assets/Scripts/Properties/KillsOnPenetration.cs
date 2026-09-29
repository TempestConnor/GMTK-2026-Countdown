using UnityEngine;

/// <summary>Marks solid colliders on this object or its children as lethal inside.</summary>
[DisallowMultipleComponent]
public sealed class KillsOnPenetration : MonoBehaviour
{
    [Tooltip("Allowed overlap depth in world units. Keeps ordinary surface contact safe.")]
    [SerializeField, Min(0f)] private float penetrationTolerance = 0.03f;

    public float PenetrationTolerance => Mathf.Max(0f, penetrationTolerance);
}
