using UnityEngine;

/// <summary>
/// Rigidbody-based first-person controller with dash, double jump, slide and wall-running.
/// Wall-running only works on colliders tagged "WallRun".
/// Controls:
///   WASD move, Mouse look
///   Space        jump / double jump / wall jump (slide jump while sliding)
///   Left Shift   dash
///   Left Ctrl    slide (hold) - direction locks on start, lasts while held
///   A / D        attach to a wall on the left / right while airborne
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;

    [Header("Look")]
    // Mouse sensitivity lives in GameSettings (PlayerPrefs) so the Settings menu can change it.
    [SerializeField] private float maxPitch = 85f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float airAcceleration = 20f;
    [SerializeField] private float extraGravity = 8f;

    [Header("Jumping")]
    [SerializeField] private float jumpVelocity = 7f;
    [SerializeField] private int airJumps = 1;               // 1 = double jump
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.8f;

    [Header("Slide")]
    [SerializeField] private KeyCode slideKey = KeyCode.LeftControl;
    [SerializeField] private float slideSpeed = 13f;
    [SerializeField] private float slideHeight = 1f;               // capsule height while sliding
    [SerializeField] private float slideCameraHeight = -0.2f;      // camera local Y while sliding
    [SerializeField] private float slideJumpVerticalBoost = 1.5f;  // x jumpVelocity
    [SerializeField] private float slideJumpHorizontalBoost = 1.45f;// x slideSpeed carried into the air
    [SerializeField] private float slideCameraTilt = 4f;

    [Header("Wall Run")]
    [SerializeField] private string wallRunTag = "WallRun";
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private float wallRunSpeed = 10f;          // minimum speed when grabbing a wall
    [SerializeField] private float wallRunMaxEntrySpeed = 18f;  // faster entries (slide jump, dash) keep speed up to this
    [SerializeField] private float wallRunDeceleration = 0f;    // speed lost per second along the wall (0 = keep entry speed)
    [SerializeField] private float wallRunMinSpeed = 3f;
    [SerializeField] private float wallRunGravity = 2.5f;        // downward acceleration while on the wall
    [SerializeField] private float wallRunMaxFallSpeed = 4f;    // cap so it's a slow slide down, not a drop
    [SerializeField] private float wallRunMaxEntryUpSpeed = 4f; // upward speed kept when grabbing mid-jump
    [SerializeField] private float wallRunMaxTime = 4.0f;
    [SerializeField] private float wallJumpUpVelocity = 7f;
    [SerializeField] private float wallJumpPushVelocity = 8f;
    [SerializeField] private float minWallRunHeight = 0.8f;   // must be this far off the ground
    [SerializeField] private float cameraTilt = 12f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private float yaw, pitch, currentTilt;
    private Vector2 moveInput;
    private bool jumpQueued, dashQueued, slideHeld;

    private bool isGrounded;
    private float lastGroundedTime;
    private float jumpedAt = -1f;   // last time we pushed off (jump / wall jump / slide jump)
    private int airJumpsLeft;

    private bool isDashing;
    private float dashEndTime, nextDashTime;
    private Vector3 dashDirection;
    private bool canAirDash = true;

    private bool isSliding;
    private Vector3 slideDirection;
    private float slideLeftGroundTime = -999f;   // lets a slide jump still work just after sliding off a ledge
    private float standHeight;
    private Vector3 standCenter;
    private float standCameraHeight;

    private bool isWallRunning;
    private float wallRunStartTime;
    private Vector3 wallNormal;
    private int wallSide;                 // -1 left, +1 right
    private float wallRunCurrentSpeed;
    private Collider currentWallCollider;
    private Collider lastWall;            // prevents re-grabbing the same wall instantly
    private float wallRegrabBlockUntil;

    public bool IsGrounded => isGrounded;
    public bool IsWallRunning => isWallRunning;
    private SpellCaster spellCaster;
    public bool IsDashing => isDashing;
    /// <summary>0 right after a dash, refilling to 1 when the next dash is ready (HUD meter).</summary>
    public float DashCharge => dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (nextDashTime - Time.time) / dashCooldown);
    public bool IsSliding => isSliding;
    /// <summary>Holds the slide as if the key were down (for testing / scripted sequences).</summary>
    public bool ForceSlideHeld { get; set; }
    /// <summary>Overrides WASD input when set (for testing / scripted sequences).</summary>
    public Vector2? ForceMoveInput { get; set; }
    public int WallSide => isWallRunning ? wallSide : 0;   // -1 left, +1 right, 0 none

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        spellCaster = GetComponent<SpellCaster>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Frictionless so the player doesn't stick to walls when pressing into them.
        capsule.material = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        yaw = transform.eulerAngles.y;

        standHeight = capsule.height;
        standCenter = capsule.center;
        standCameraHeight = cameraPivot != null ? cameraPivot.localPosition.y : 0.7f;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Look
        float sensitivity = GameSettings.MouseSensitivity;   // set from Settings (main menu / pause menu)
        if (Cursor.lockState == CursorLockMode.Locked && !GameFlow.Paused)
        {
            yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
            pitch -= Input.GetAxisRaw("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        }
        // Escape is handled by the pause menu. Clicking back into the game re-locks the cursor.
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !GameFlow.Paused)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        moveInput = ForceMoveInput ?? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        if (!GameFlow.Paused)   // physics is stopped while paused; a queued press would fire on resume
        {
            if (Input.GetKeyDown(KeyCode.Space)) jumpQueued = true;
            if (Input.GetKeyDown(KeyCode.LeftShift)) dashQueued = true;
        }
        slideHeld = Input.GetKey(slideKey) || ForceSlideHeld;
        ApplyLook();
    }

    // Runs at the end of Update, before animation: the arms / book IK (PlayerBody) read the camera, so turning it
    // later (LateUpdate) left the hands one frame behind the view.
    private void ApplyLook()
    {
        // Tilt the head AWAY from the wall: right wall -> lean left (positive Z roll).
        float targetTilt = 0f;
        if (isWallRunning) targetTilt = wallSide * cameraTilt;
        else if (isSliding) targetTilt = slideCameraTilt;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, 10f * Time.deltaTime);

        // Only the camera turns here, every frame. The body turns in FixedUpdate via rb.MoveRotation: writing
        // transform.rotation each frame reset the Rigidbody's interpolation, so the camera only moved on physics
        // ticks and juddered sideways while strafing (A/D) and turning.
        if (cameraPivot != null)
        {
            cameraPivot.rotation = Quaternion.Euler(new Vector3(pitch, yaw, currentTilt) + CameraShake.Offset());
            Vector3 lp = cameraPivot.localPosition;
            float targetY = isSliding ? slideCameraHeight : standCameraHeight;
            lp.y = Mathf.Lerp(lp.y, targetY, 15f * Time.deltaTime);
            cameraPivot.localPosition = lp;
        }
    }

    private void FixedUpdate()
    {
        rb.MoveRotation(YawRotation);
        CheckGround();
        UpdateSlide();
        UpdateWallRun();

        if (dashQueued) { TryDash(); dashQueued = false; }
        if (jumpQueued) { TryJump(); jumpQueued = false; }

        if (isDashing)
        {
            if (Time.time >= dashEndTime) EndDash();
            else { rb.linearVelocity = dashDirection * dashSpeed; return; }
        }

        if (isSliding) SlideMove();
        else if (isWallRunning) WallRunMove();
        else Move();
    }

    /// <summary>
    /// Instantly moves the player (checkpoint respawns, level starts), clearing momentum and
    /// any dash / slide / wall-run state, and facing the given yaw.
    /// </summary>
    public void Teleport(Vector3 position, float facingYaw)
    {
        if (isSliding) StopSlide();
        if (isWallRunning) StopWallRun();
        isDashing = false;
        rb.useGravity = true;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
        yaw = facingYaw;
        pitch = 0f;
        rb.rotation = transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        jumpQueued = dashQueued = false;
        lastWall = null;
    }

    /// <summary>Facing from the mouse, current this frame (the body's transform catches up on the next physics tick).</summary>
    private Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);
    private Vector3 Forward => YawRotation * Vector3.forward;
    private Vector3 Right => YawRotation * Vector3.right;

    // ---------- Ground ----------
    private void CheckGround()
    {
        float radius = capsule.radius * 0.95f;
        Vector3 origin = transform.position + capsule.center + Vector3.down * (capsule.height * 0.5f - capsule.radius) + Vector3.up * 0.05f;
        bool hit = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit ground, groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
        bool wasGrounded = isGrounded;
        // Walking up a ramp (stairs) moves you upward too: still grounded unless we just jumped. Before, ramps
        // counted as air (weak air control + extra gravity), so you slid out on the spiral staircase.
        bool walkingUpSlope = ground.normal.y > 0.7f && Time.time - jumpedAt > 0.2f;
        isGrounded = hit && (rb.linearVelocity.y <= 0.1f || walkingUpSlope);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            airJumpsLeft = airJumps;
            canAirDash = true;
            lastWall = null;
            if (!wasGrounded && isWallRunning) StopWallRun();
        }
    }

    // ---------- Movement ----------
    private Vector3 WishDirection()
    {
        return Forward * moveInput.y + Right * moveInput.x;
    }

    private void Move()
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        float castSlow = spellCaster != null ? spellCaster.MoveMultiplier : 1f;
        Vector3 target = WishDirection() * moveSpeed * castSlow;

        float accel = isGrounded ? groundAcceleration : airAcceleration;
        if (castSlow < 1f)
        {
            // Winding up a heavy spell: bleed speed hard (even in the air) until it goes off.
            horizontal = Vector3.MoveTowards(horizontal, target, groundAcceleration * 2f * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);
            if (!isGrounded) rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
            return;
        }
        // In the air with no input, keep momentum (from dashes / wall jumps / slide jumps).
        if (!isGrounded && moveInput == Vector2.zero) target = horizontal;
        // In the air, don't brake momentum above move speed in the input direction.
        if (!isGrounded && horizontal.magnitude > moveSpeed && moveInput != Vector2.zero)
            target = target.normalized * horizontal.magnitude;

        horizontal = Vector3.MoveTowards(horizontal, target, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);

        if (!isGrounded) rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
    }

    // ---------- Jump ----------
    private void TryJump()
    {
        if (isWallRunning)
        {
            // Carry the current along-wall speed so chaining wall jumps keeps momentum.
            Vector3 v = wallNormal * wallJumpPushVelocity + Vector3.up * wallJumpUpVelocity
                        + WallRunDirection() * wallRunCurrentSpeed;
            lastWall = currentWallCollider;
            wallRegrabBlockUntil = Time.time + 0.3f;
            StopWallRun();
            airJumpsLeft = airJumps;   // wall jump refreshes the double jump
            canAirDash = true;
            rb.linearVelocity = v;
            jumpedAt = Time.time;
            return;
        }

        bool justSlidOffLedge = Time.time - slideLeftGroundTime <= coyoteTime;
        if ((isSliding && isGrounded) || justSlidOffLedge)
        {
            SlideJump();
            return;
        }

        bool coyote = Time.time - lastGroundedTime <= coyoteTime;
        if (isGrounded || coyote)
        {
            SetVerticalVelocity(jumpVelocity);
            jumpedAt = Time.time;
            lastGroundedTime = -999f;
            isGrounded = false;
        }
        else if (airJumpsLeft > 0)
        {
            airJumpsLeft--;
            // Let the double jump redirect horizontal momentum toward current input.
            if (moveInput != Vector2.zero)
            {
                Vector3 horiz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                Vector3 redirected = WishDirection().normalized * Mathf.Max(horiz.magnitude, moveSpeed);
                rb.linearVelocity = new Vector3(redirected.x, rb.linearVelocity.y, redirected.z);
            }
            SetVerticalVelocity(jumpVelocity);
            jumpedAt = Time.time;
        }
    }

    private void SetVerticalVelocity(float y)
    {
        Vector3 v = rb.linearVelocity;
        v.y = y;
        rb.linearVelocity = v;
    }

    // ---------- Slide ----------
    private void UpdateSlide()
    {
        if (isSliding)
        {
            bool released = !slideHeld && HasHeadroom();
            if (!isGrounded)
            {
                // Slid off a ledge: keep momentum, allow a late slide jump.
                slideLeftGroundTime = Time.time;
                StopSlide();
            }
            else if (released)
            {
                StopSlide();
            }
            return;
        }

        if (slideHeld && isGrounded && !isDashing && !isWallRunning)
            StartSlide();
    }

    private void StartSlide()
    {
        Vector3 dir = WishDirection();
        if (dir.sqrMagnitude < 0.01f) dir = Forward;
        slideDirection = dir.normalized;
        isSliding = true;

        // Shrink the capsule from the top so the feet stay on the ground.
        capsule.height = slideHeight;
        capsule.center = standCenter + Vector3.down * (standHeight - slideHeight) * 0.5f;
    }

    private void StopSlide()
    {
        isSliding = false;
        capsule.height = standHeight;
        capsule.center = standCenter;
    }

    private bool HasHeadroom()
    {
        float radius = capsule.radius * 0.95f;
        Vector3 top = transform.position + capsule.center + Vector3.up * (capsule.height * 0.5f - capsule.radius);
        float needed = standHeight - capsule.height;
        return !Physics.SphereCast(top, radius, Vector3.up, out _, needed, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void SlideMove()
    {
        Vector3 v = slideDirection * slideSpeed;
        v.y = Mathf.Min(rb.linearVelocity.y, 0f);
        rb.linearVelocity = v;
        rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration); // hug slopes / ground
    }

    private void SlideJump()
    {
        Vector3 dir = slideDirection;
        if (isSliding)
        {
            if (!HasHeadroom()) return;   // can't stand up under a low ceiling
            StopSlide();
        }
        slideLeftGroundTime = -999f;
        lastGroundedTime = -999f;
        isGrounded = false;
        jumpedAt = Time.time;

        Vector3 v = dir * slideSpeed * slideJumpHorizontalBoost;
        v.y = jumpVelocity * slideJumpVerticalBoost;
        rb.linearVelocity = v;
    }

    // ---------- Dash ----------
    private void TryDash()
    {
        if (isDashing || Time.time < nextDashTime) return;
        if (!isGrounded && !isWallRunning && !canAirDash) return;

        Vector3 dir = WishDirection();
        if (dir.sqrMagnitude < 0.01f) dir = Forward;
        dashDirection = dir.normalized;

        if (isWallRunning) StopWallRun();
        if (isSliding && HasHeadroom()) StopSlide();
        if (!isGrounded) canAirDash = false;

        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        rb.useGravity = false;
    }

    private void EndDash()
    {
        isDashing = false;
        rb.useGravity = true;
        // Keep some carry-over momentum after the dash.
        rb.linearVelocity = dashDirection * moveSpeed * 1.2f;
    }

    // ---------- Wall Run ----------
    private bool FindWall(out RaycastHit hit, out int side)
    {
        Vector3 origin = transform.position + capsule.center;
        float dist = capsule.radius + wallCheckDistance;
        if (Physics.Raycast(origin, Right, out hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.collider.CompareTag(wallRunTag))
        { side = 1; return true; }
        if (Physics.Raycast(origin, -Right, out hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.collider.CompareTag(wallRunTag))
        { side = -1; return true; }
        side = 0;
        return false;
    }

    // D attaches to a right wall, A to a left wall.
    private bool HoldingTowardWall(int side)
    {
        return (side == 1 && moveInput.x > 0.1f) || (side == -1 && moveInput.x < -0.1f);
    }

    private bool HighEnoughForWallRun()
    {
        Vector3 feet = transform.position + capsule.center + Vector3.down * (capsule.height * 0.5f);
        return !Physics.Raycast(feet, Vector3.down, minWallRunHeight, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void UpdateWallRun()
    {
        bool hasWall = FindWall(out RaycastHit hit, out int side);

        if (isWallRunning)
        {
            bool timedOut = Time.time - wallRunStartTime > wallRunMaxTime;
            if (!hasWall || isGrounded || !HoldingTowardWall(side) || timedOut)
            {
                StopWallRun();
                return;
            }
            wallNormal = hit.normal;
            wallSide = side;
            currentWallCollider = hit.collider;
            return;
        }

        bool blocked = hit.collider != null && hit.collider == lastWall && Time.time < wallRegrabBlockUntil;
        if (hasWall && !blocked && !isGrounded && !isDashing && !isSliding && HoldingTowardWall(side) && HighEnoughForWallRun())
        {
            isWallRunning = true;
            wallRunStartTime = Time.time;
            wallNormal = hit.normal;
            wallSide = side;
            currentWallCollider = hit.collider;
            rb.useGravity = false;
            airJumpsLeft = airJumps;
            canAirDash = true;

            // Keep incoming speed along the wall (at least wallRunSpeed).
            Vector3 vel = rb.linearVelocity;
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            wallRunCurrentSpeed = Mathf.Clamp(Vector3.Dot(horiz, WallRunDirection()), wallRunSpeed, wallRunMaxEntrySpeed);

            // Keep a little upward speed if grabbing mid-jump; cancel any fall.
            vel.y = Mathf.Clamp(vel.y, 0f, wallRunMaxEntryUpSpeed);
            rb.linearVelocity = vel;
        }
    }

    // Along the wall, in whichever direction the camera is facing.
    private Vector3 WallRunDirection()
    {
        Vector3 along = Vector3.Cross(wallNormal, Vector3.up);
        if (Vector3.Dot(along, Forward) < 0f) along = -along;
        return along;
    }

    private void WallRunMove()
    {
        // Lose speed over time and slowly slide down the wall.
        wallRunCurrentSpeed = Mathf.MoveTowards(wallRunCurrentSpeed, wallRunMinSpeed, wallRunDeceleration * Time.fixedDeltaTime);

        Vector3 v = WallRunDirection() * wallRunCurrentSpeed;
        v.y = Mathf.Max(rb.linearVelocity.y - wallRunGravity * Time.fixedDeltaTime, -wallRunMaxFallSpeed);
        v += -wallNormal * 2f;   // stick to the wall
        rb.linearVelocity = v;
    }

    private void StopWallRun()
    {
        isWallRunning = false;
        if (!isDashing) rb.useGravity = true;
    }
}
