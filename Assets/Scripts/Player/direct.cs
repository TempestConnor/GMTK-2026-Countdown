using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(playerController2))]
public class direct : MonoBehaviour
{
    [SerializeField] private LayerMask groundMaskA;
    [SerializeField] private LayerMask groundMaskB;
    public float groundDistance = 0.05f;
    public float wallDistance = 0.2f;
    public float ceilingDistance = 0.05f;

    // Not consulting the Physics2D collision matrix, Collider2D.Cast only respects this filter's own mask --
    // it must be swapped to the active plane's mask or the player would false-detect ground through the plane it isn't on.
    private ContactFilter2D castFilter;

    CapsuleCollider2D touchingCol;
    playerController2 player;

    Animator animator;

    readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
    readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>();
    readonly List<RaycastHit2D> ceilingHits = new List<RaycastHit2D>();

    [SerializeField]
    private bool _isGrounded;
    public bool isGrounded { get 
        { 
            return _isGrounded; 
        } 
        private set 
        { 
            _isGrounded = value;
            animator.SetBool("isGrounded", value);
        } 
    }

    [SerializeField]
    private bool _isOnWall;
    public bool isOnWall
    {
        get
        {
            return _isOnWall;
        }
        private set
        {
            _isOnWall = value;
        }
    }

    [SerializeField]
    private bool _isOnCeiling;
    public bool isOnCeiling
    {
        get
        {
            return _isOnCeiling;
        }
        private set
        {
            _isOnCeiling = value;
        }
    }



    // Reads facing directly from playerController2 instead of transform.localScale,
    // since localScale can be edited/reset independently of isFacingRight and desync.
    private Vector2 wallCheckDirection => player.isFacingRight ? Vector2.right : Vector2.left;



    // Start is called before the first frame update
    private void Awake()
    {
        touchingCol = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        player = GetComponent<playerController2>();

        castFilter = new ContactFilter2D();
        castFilter.useTriggers = false;
        castFilter.SetLayerMask(groundMaskA);
    }

    public void SetActivePlane(Banishable.Plane p)
    {
        castFilter.SetLayerMask(p == Banishable.Plane.A ? groundMaskA : groundMaskB);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        RefreshContacts();
    }

    public void RefreshContacts()
    {
        if (touchingCol == null || player == null) return;
        touchingCol.Cast(player.GravityDown, castFilter, groundHits, groundDistance);
        isGrounded = HasBlockingHit(groundHits);
        int wallCount = touchingCol.Cast(wallCheckDirection, castFilter, wallHits, wallDistance);
        isOnWall = false;
        for (int i = 0; i < wallCount; i++)
        {
            if (!SafePlayerCollision.Blocks(touchingCol, wallHits[i].collider)) continue;
            // Floor lips (e.g. a resting box sits a contact offset above the grid) are hit near the feet
            // with an upward normal; only surfaces facing against the walk direction are walls.
            if (Vector2.Dot(wallHits[i].normal, wallCheckDirection) > -0.5f) continue;
            if (player.GrabbedBody != null && (wallHits[i].rigidbody == player.GrabbedBody ||
                wallHits[i].collider.GetComponentInParent<Pushable>()?.Body == player.GrabbedBody)) continue;
            isOnWall = true;
            break;
        }
        touchingCol.Cast(-player.GravityDown, castFilter, ceilingHits, ceilingDistance);
        isOnCeiling = HasBlockingHit(ceilingHits);
    }

    private bool HasBlockingHit(List<RaycastHit2D> hits)
    {
        foreach (var hit in hits) if (SafePlayerCollision.Blocks(touchingCol, hit.collider)) return true;
        return false;
    }
}
