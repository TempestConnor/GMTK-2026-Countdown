using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(direct))]
public partial class playerController2 : MonoBehaviour
{
    [Header("Walk Settings")]
    [Space]

    public float walkSpeed = 5f;
    private bool canwalk = true;
    Vector2 moveInput;

    [Space]
    [Header("Jump Settings")]
    [Space]

    public float jumpImpulse = 11.5f;
    public float gravityMultiplier = 2f;
    public float maxFallSpeed = 7f;
    private bool canJump = true;
    [SerializeField] protected float originalGravity;

    [Space]
    [Header("Wall Jump/Slide Settings")]
    [Space]

    public float slideSpeed = 2f;
    public float wallJumpGravity = 1.5f;
    public float wallJumpBounceDuration = 0.15f;
    public float wallJumpBounceForce = 5f;
    public float wallJumpWindow;
    public bool canWallJump;
    Vector2 wallJumpDirection;

    [Space]
    [Header("Dash Settings")]
    [Space]

    public float dashSpeed = 14f;
    public float dashDuration = 1f;
    public float maxDashSpeed = 14f;
    public float dashEndSpeed = 0.25f;
    Vector2 dashDirection;

    [Space]
    [Header("Attack Settings")]
    [Space]

    public float attackCooldown = 0.55f;
    private bool canAttack = true;


    Rigidbody2D rb;
    direct touchingDirection;
    Animator animator;


    public float currentMoveSpeed
    {
        get
        {
            if (!touchingDirection.isOnWall && canwalk)
            {
                return walkSpeed;
            }
            else
            {
                return 0;
            }
        }
    }

    [SerializeField] private bool _isMoving = false;
    public bool isMoving { get
        {
            return _isMoving;
        } private set
        {
            _isMoving = value;
            animator.SetBool("isMoving", value);
        }
    }

    [SerializeField] private bool _isSliding;
    public bool isSliding
    {
        get
        {
            return _isSliding;
        }
        private set
        {
            _isSliding = value;
            animator.SetBool("wall slide", value);
        }
    }

    [SerializeField] public bool _isFacingRight = true;
    public bool isFacingRight { get
        {
            return _isFacingRight;
        }
        private set
        {
            if (_isFacingRight != value)
            {
                transform.localScale *= new Vector2(-1, 1);
            }
            _isFacingRight = value;
        }
    }

    [SerializeField] private bool _isDashing;
    public bool isDashing
    {
        get
        {
            return _isDashing;
        }
        private set
        {
            _isDashing = value;
        }
    }

    [SerializeField] private bool _canDash = true;
    public bool canDash
    {
        get
        {
            return _canDash;
        }
        private set
        {

            _canDash = value;
        }
    }

    [SerializeField] private bool _isWallJumping;


    public bool isWallJumping
    {
        get
        {
            return _isWallJumping;
        }
        private set
        {
            _isWallJumping = value;
        }
    }


    // When want something to be found ASAP, use Awake()
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        touchingDirection = GetComponent<direct>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        originalGravity = rb.gravityScale;
    }
    // Physics update should occur on Fixed update instead
    private void FixedUpdate()
    {
        animator.SetFloat("yVelocity", rb.linearVelocity.y);

        // Walking code -- turn off when dashing or wall jumping
        if (canwalk)
        {
            rb.linearVelocity = new Vector2(MathF.Round(moveInput.x) * currentMoveSpeed, rb.linearVelocity.y);
        }



        // Enable dash when on the ground
        if (touchingDirection.isGrounded && !isDashing)
        {
            canDash = true;
        }

        // maxDashSpeed implementation
        if (isDashing && rb.linearVelocity.y > maxDashSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, maxDashSpeed));
            Debug.Log("maxDash triggered");
        }

        // Wall slide code
        if (touchingDirection.isOnWall && !touchingDirection.isGrounded)
        {
            performWallSlide();

            canWallJump = true;
        }
        else if (isSliding && !touchingDirection.isOnWall || touchingDirection.isGrounded)
        {
            StartCoroutine(wallJumpWait());
            isSliding = false;
        }


        // Check for Wall Jump direction
        if (isSliding)
        {
            wallJumpDirection = isFacingRight ? Vector2.left : Vector2.right;
        }
        // This serves wallJumpWait()
        else if (!isSliding)
        {
            wallJumpDirection = isFacingRight ? Vector2.right : Vector2.left;
        }

        // Check for Dash direction
        if (moveInput != Vector2.zero)
        {
            dashDirection = moveInput;
        }
        else if (isSliding && !isMoving)
        {
            dashDirection = wallJumpDirection;
        }
        else
        {
            dashDirection = isFacingRight ? Vector2.right  : Vector2.left;
        }



        // Increase gravity when falling -- maxFallSpeed
        if (rb.linearVelocity.y < 0 && !isDashing && !touchingDirection.isGrounded)
        {
            setGravityScale(originalGravity * gravityMultiplier);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed));
        }

        // Reset gravity when grounded
        if (touchingDirection.isGrounded)
        {
            setGravityScale(originalGravity);


        }
    }

    protected void setGravityScale(float scale)
    {
        rb.gravityScale = scale;
    }
}
