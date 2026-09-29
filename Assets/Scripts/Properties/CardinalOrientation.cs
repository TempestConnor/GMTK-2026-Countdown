using UnityEngine;

/// <summary>Rotates the visual and hitbox together without moving the grid anchor.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CardinalOrientation : MonoBehaviour
{
    public enum Direction { Up, Right, Down, Left }
    [SerializeField] private Direction facing;
    [SerializeField] private Transform pivot;
    public Direction Facing
    {
        get => facing;
        set { facing = value; Apply(); }
    }

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void Apply()
    {
        if (pivot != null)
            pivot.localRotation = Quaternion.Euler(0f, 0f, -90f * (int)facing);
    }
}
