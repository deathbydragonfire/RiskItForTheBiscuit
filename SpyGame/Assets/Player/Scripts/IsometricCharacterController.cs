using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class IsometricCharacterController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 25f;

    [Header("Gravity / Grounding")]
    public float gravity = 30f;           // positive number
    public float groundStickForce = 4f;   // mild downward push while grounded
    [Range(0f, 89f)] public float maxSlopeAngle = 45f;
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayers = ~0;

#if ENABLE_INPUT_SYSTEM
    [Header("Input System")]
    [Tooltip("Optional. If left empty, the script creates a WASD/Arrows + Left Stick action at runtime.")]
    public InputActionReference moveActionRef;
    private InputAction _moveAction;
#else
    // Legacy input fallback ONLY if the new input system isn't enabled in this project.
    private readonly string _hor = "Horizontal";
    private readonly string _ver = "Vertical";
#endif

    [Header("Camera Follow (for Shake)")]
    [Tooltip("CameraFollow component to shake. If left empty, will try FindObjectOfType at runtime.")]
    public CameraFollow cameraFollow;

    [Header("Landing Shake — Small Fall")]
    [Tooltip("Minimum downward speed (units/sec) reached during a fall to trigger a SMALL shake on landing.")]
    public float smallFallThreshold = 8f;
    [Tooltip("Base duration for a SMALL landing shake.")]
    public float smallFallDuration = 0.18f;
    [Tooltip("Magnitude per unit of impact speed above the SMALL threshold.")]
    public float smallMagPerUnit = 0.015f;
    [Tooltip("Maximum magnitude clamp for SMALL landing shakes.")]
    public float smallMaxMagnitude = 0.25f;

    [Header("Landing Shake — Big Fall")]
    [Tooltip("Minimum downward speed (units/sec) reached during a fall to trigger a BIG shake on landing.")]
    public float bigFallThreshold = 16f;
    [Tooltip("Base duration for a BIG landing shake.")]
    public float bigFallDuration = 0.32f;
    [Tooltip("Magnitude per unit of impact speed above the BIG threshold.")]
    public float bigMagPerUnit = 0.03f;
    [Tooltip("Maximum magnitude clamp for BIG landing shakes.")]
    public float bigMaxMagnitude = 0.55f;

    [Header("Shake Misc")]
    [Tooltip("Cooldown between landing shakes to avoid spam (seconds).")]
    public float fallShakeCooldown = 0.10f;

    private CharacterController controller;
    private Vector3 velocity;   // full velocity (x,z,y)
    private Vector3 horizVel;   // horizontal only
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;

    // Internal fall tracking
    private float maxDownwardSpeedWhileAirborne = 0f; // positive value for speed
    private float lastShakeTime = -999f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = maxSlopeAngle;

        if (cameraFollow == null)
            cameraFollow = FindObjectOfType<CameraFollow>(); // safe fallback
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveActionRef != null)
        {
            _moveAction = moveActionRef.action;
        }
        else
        {
            // Create a simple 2D vector action: WASD + Arrow Keys + Gamepad Left Stick
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            var wasd = _moveAction.AddCompositeBinding("2DVector");
            wasd.With("Up", "<Keyboard>/w");
            wasd.With("Down", "<Keyboard>/s");
            wasd.With("Left", "<Keyboard>/a");
            wasd.With("Right", "<Keyboard>/d");

            var arrows = _moveAction.AddCompositeBinding("2DVector");
            arrows.With("Up", "<Keyboard>/upArrow");
            arrows.With("Down", "<Keyboard>/downArrow");
            arrows.With("Left", "<Keyboard>/leftArrow");
            arrows.With("Right", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick");
        }
        _moveAction.Enable();
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        _moveAction?.Disable();
#endif
    }

    void Update()
    {
        // Cache grounded state at start of frame for landing detection after Move()
        bool wasGrounded = isGrounded;

        // 1) Input -> world X/Z
        Vector2 input = ReadMoveInput();
        Vector3 inputDir = new Vector3(input.x, 0f, input.y);
        if (inputDir.sqrMagnitude > 1e-4f) inputDir.Normalize();

        // 2) Ground info (pre-move)
        UpdateGroundInfo();

        // 3) Smooth toward target horizontal velocity
        Vector3 targetHorizVel = inputDir * moveSpeed;
        float accel = (targetHorizVel.sqrMagnitude > 1e-4f) ? acceleration : deceleration;
        horizVel = Vector3.MoveTowards(horizVel, targetHorizVel, accel * Time.deltaTime);

        // 4) Conform to slopes
        if (isGrounded && Vector3.Angle(groundNormal, Vector3.up) <= maxSlopeAngle + 0.01f)
            horizVel = Vector3.ProjectOnPlane(horizVel, groundNormal);

        // 5) Vertical velocity
        velocity.y = isGrounded ? -groundStickForce : velocity.y - gravity * Time.deltaTime;

        // Track max downward speed while airborne (positive value)
        if (!isGrounded && velocity.y < 0f)
            maxDownwardSpeedWhileAirborne = Mathf.Max(maxDownwardSpeedWhileAirborne, -velocity.y);

        // 6) Combine and move
        velocity.x = horizVel.x;
        velocity.z = horizVel.z;
        controller.Move(velocity * Time.deltaTime);

        // Landing detection (post-move)
        bool groundedNow = controller.isGrounded;
        if (!wasGrounded && groundedNow)
        {
            HandleLandingShake(maxDownwardSpeedWhileAirborne);
            maxDownwardSpeedWhileAirborne = 0f; // reset after landing
        }

        // Keep our cached flag in sync
        isGrounded = groundedNow;
    }

    void UpdateGroundInfo()
    {
        isGrounded = controller.isGrounded;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float castDist = controller.skinWidth + groundCheckDistance + 0.1f;
        float radius = controller.radius * 0.95f;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, castDist, groundLayers, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle + 0.01f)
                isGrounded = false; // too steep
        }
        else
        {
            groundNormal = Vector3.up;
        }
    }

    Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        return _moveAction != null ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
#else
        float x = Input.GetAxisRaw(_hor);
        float y = Input.GetAxisRaw(_ver);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#endif
    }

    void HandleLandingShake(float impactSpeed)
    {
        if (cameraFollow == null) return;
        if (Time.time - lastShakeTime < fallShakeCooldown) return;

        // Decide tier first (BIG takes precedence)
        if (impactSpeed >= bigFallThreshold)
        {
            float over = impactSpeed - bigFallThreshold;
            float magnitude = Mathf.Min(over * bigMagPerUnit, bigMaxMagnitude);
            cameraFollow.Shake(bigFallDuration, magnitude);
            lastShakeTime = Time.time;
        }
        else if (impactSpeed >= smallFallThreshold)
        {
            float over = impactSpeed - smallFallThreshold;
            float magnitude = Mathf.Min(over * smallMagPerUnit, smallMaxMagnitude);
            cameraFollow.Shake(smallFallDuration, magnitude);
            lastShakeTime = Time.time;
        }
        // else: no shake for soft landings
    }

    // ------- Debug Overlay -------
    void OnGUI()
    {
        GUI.color = Color.white;
        string groundedText = $"IsGrounded: {isGrounded}";
        string fallSpeedText = $"Falling Speed: {(-velocity.y).ToString("F2")}";

        GUI.Label(new Rect(10, 10, 300, 20), groundedText);
        GUI.Label(new Rect(10, 30, 300, 20), fallSpeedText);
    }

    // Debug helpers
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
}
