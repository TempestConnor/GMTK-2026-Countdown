using UnityEngine;

/// <summary>
/// Keeps a tiled backdrop sprite filling the camera's view at a fixed world depth behind the level.
/// The pattern stays fixed in the world, so the perspective camera gives it parallax for free.
/// Lives as a child of the MainCamera prefab; built by Tools > Level > Generate Facility Backdrop.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FacilityBackdrop : MonoBehaviour
{
    [Tooltip("World Z of the backdrop plane. Further from the camera = slower parallax.")]
    [SerializeField] private float depth = 20f;

    private SpriteRenderer spriteRenderer;
    private Camera targetCamera;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        targetCamera = GetComponentInParent<Camera>();
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || spriteRenderer.sprite == null) return;

        var cam = targetCamera.transform.position;
        float distance = Mathf.Max(depth - cam.z, targetCamera.nearClipPlane);
        float halfHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize
            : distance * Mathf.Tan(targetCamera.fieldOfView * .5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * targetCamera.aspect;

        // Snap to whole tiles so the pattern never slides relative to the world, and pad by one tile
        // on each side to cover the snapping offset.
        Vector2 period = spriteRenderer.sprite.bounds.size;
        transform.SetPositionAndRotation(new Vector3(
            Mathf.Round(cam.x / period.x) * period.x,
            Mathf.Round(cam.y / period.y) * period.y,
            depth), Quaternion.identity);
        spriteRenderer.size = new Vector2(2 * halfWidth + 2 * period.x, 2 * halfHeight + 2 * period.y);
    }
}
