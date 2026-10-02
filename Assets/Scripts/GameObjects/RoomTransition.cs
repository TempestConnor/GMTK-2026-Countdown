using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
public sealed class RoomTransition : MonoBehaviour
{
    public RoomConnection connection;
    [Tooltip("Unique within this room. Must match this endpoint in the connection asset.")]
    public string zoneId;
    public Vector2 areaSize = new Vector2(1, 4);
    [Tooltip("Player root position after arrival. Place safely inside the camera and clear of terrain.")]
    public Transform arrival;
    [SerializeField] private SpriteRenderer paletteVisual;
    private PlayerLife blockedPlayer;
    public Vector3 ArrivalPosition => arrival != null ? arrival.position : transform.position;

    private void Reset() => ConfigureArea();
    private void Awake()
    {
        if (paletteVisual != null) paletteVisual.enabled = false;
    }
    private void OnValidate() => ConfigureArea();
    public void ConfigureArea()
    {
        areaSize = Vector2.Max(Vector2.one, areaSize);
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = areaSize;
        box.offset = areaSize / 2;
        if (paletteVisual != null && paletteVisual.sprite != null)
        {
            paletteVisual.transform.localPosition = areaSize / 2;
            var size = paletteVisual.sprite.bounds.size;
            paletteVisual.transform.localScale = new Vector3(areaSize.x / size.x, areaSize.y / size.y, 1);
        }
    }

    public void BlockUntilClear(PlayerLife player) => blockedPlayer = player;
    private void FixedUpdate()
    {
        if (blockedPlayer == null) return;
        var box = GetComponent<BoxCollider2D>();
        foreach (var body in blockedPlayer.GetComponents<Collider2D>())
            if (body.enabled && !body.isTrigger && box.Distance(body).isOverlapped) return;
        blockedPlayer = null;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryEnter(other);
    // Stay also allows retry after another lock ends while standing in the doorway.
    private void OnTriggerStay2D(Collider2D other) => TryEnter(other);
    private void TryEnter(Collider2D other)
    {
        if (!isActiveAndEnabled || other.isTrigger || other.attachedRigidbody == null) return;
        var player = other.attachedRigidbody.GetComponent<PlayerLife>();
        if (player == null || player == blockedPlayer || !player.isAlive || RoomTravel.IsLoading) return;
        if (player.GetComponent<PlayerInputLock>().IsLocked) return;
        if (!RoomTravel.TryTransition(this, player)) blockedPlayer = player;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(areaSize / 2, areaSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(areaSize / 2, areaSize);
        Gizmos.matrix = Matrix4x4.identity;
        if (arrival == null) return;
        Gizmos.DrawLine(transform.TransformPoint(areaSize / 2), arrival.position);
        Gizmos.DrawWireSphere(arrival.position, 0.25f);
    }
}
