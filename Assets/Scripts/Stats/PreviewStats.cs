using UnityEngine;

[CreateAssetMenu(fileName = "PreviewStats", menuName = "Player/Preview Stats")]
public class PreviewStats : ScriptableObject
{
    [Tooltip("How fast (in world units/second) the preview circle grows while E is held, and shrinks back once released.")]
    public float expansionSpeed = 10f;

    [Tooltip("Radius (in world units) the preview circle grows to once fully expanded.")]
    public float maxRadius = 4f;

    [Tooltip("Width (in world units) of the soft blend at the circle's edge, so the plane A/B border isn't a hard cutout.")]
    public float edgeSoftness = 0.5f;
}
