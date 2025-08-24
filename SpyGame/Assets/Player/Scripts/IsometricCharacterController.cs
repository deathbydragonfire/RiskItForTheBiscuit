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

    private CharacterController controller;
    private Vector3 velocity;   // full velocity (x,z,y)
    private Vector3 horizVel;   // horizontal only
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = maxSlopeAngle;
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
        // 1) Input -> world X/Z (no camera logic here)
        Vector2 input = ReadMoveInput();
        Vector3 inputDir = new Vector3(input.x, 0f, input.y);
        if (inputDir.sqrMagnitude > 1e-4f) inputDir.Normalize();

        // 2) Ground info
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

        // 6) Combine and move
        velocity.x = horizVel.x;
        velocity.z = horizVel.z;
        controller.Move(velocity * Time.deltaTime);
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
        // Only compiled if you haven't enabled the new Input System define.
        float x = Input.GetAxisRaw(_hor);
        float y = Input.GetAxisRaw(_ver);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#endif
    }

    // Debug helpers
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
}
