using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives Animator booleans (no speed multiplier):
///  - "isMoving"    : true if horizontal speed (XZ) > threshold
///  - "isCarrying"  : always true (for now)
///  - "isFalling"   : true if CharacterController exists and !isGrounded
///  - "isSlowing"   : true if no input OR input opposes current horizontal velocity
///  - "isBackwards" : true if moving along world -X (vx < -epsilon)
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [Tooltip("Optional: interpret input relative to this (e.g., camera). If null, uses world X/Z.")]
    [SerializeField] private Transform inputSpace;

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New Input System)")]
    [Tooltip("Move action (Vector2). If not set, a default WASD/Arrows + LeftStick action is created.")]
    [SerializeField] private InputActionReference moveActionRef;
    private InputAction _moveAction;
    private InputAction _defaultMove;
#endif

    [Header("Motion Detection")]
    [Tooltip("Horizontal speed above this counts as moving.")]
    [SerializeField] private float moveSpeedEpsilon = 0.1f;

    [Tooltip("Min speed before 'opposite input' logic is considered.")]
    [SerializeField] private float minSpeedForOpposite = 0.5f;

    [Tooltip("Dot threshold for 'opposite direction' check (<= means opposite).")]
    [SerializeField, Range(-1f, 0f)] private float oppositeDirDot = -0.35f;

    [Header("Carry (temp)")]
    [Tooltip("Spec says carrying is always true for now.")]
    [SerializeField] private bool alwaysCarrying = true;

    [Header("Backwards (-X)")]
    [Tooltip("Small world-space X speed needed before we consider motion as -X.")]
    [SerializeField] private float backwardsXEpsilon = 0.1f;

    // Fallback velocity tracker when no CharacterController is present
    private Vector3 _prevPos;

    public bool IsMoving { get; private set; } 
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
                characterController = GetComponentInParent<CharacterController>();
        }
        _prevPos = transform.position;
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveActionRef != null && moveActionRef.action != null)
        {
            _moveAction = moveActionRef.action;
            _moveAction.Enable();
        }
        else
        {
            _defaultMove = new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");
            _defaultMove.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _defaultMove.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _defaultMove.AddBinding("<Gamepad>/leftStick");
            _defaultMove.Enable();
            _moveAction = _defaultMove;
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_defaultMove != null && _defaultMove.enabled) _defaultMove.Disable();
#endif
    }

    private void Update()
    {
        if (animator == null) return;

        // --- Horizontal velocity (XZ) ---
        Vector3 horizVel = GetHorizontalVelocity();
        float speed = horizVel.magnitude;

        // --- Input as world XZ (for slowing logic) ---
        Vector3 inputWorld = GetWorldSpaceInputXZ();

        // --- Flags ---
        bool isFalling = (characterController != null) && !characterController.isGrounded;
        bool isCarrying = alwaysCarrying;

        bool noInput = inputWorld.sqrMagnitude < 0.0001f; 
        bool isMoving = speed > moveSpeedEpsilon && !noInput;
        bool opposite = false;
        if (!noInput && speed >= minSpeedForOpposite)
        {
            float dot = Vector3.Dot(horizVel.normalized, inputWorld.normalized);
            opposite = dot <= oppositeDirDot;
        }
        bool isSlowing = noInput || opposite;

        // world -X check
        bool isBackwards = (horizVel.x < -backwardsXEpsilon);

        // --- Push to Animator ---
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isCarrying", isCarrying);
        animator.SetBool("isFalling", isFalling);
        animator.SetBool("isSlowing", isSlowing);
        animator.SetBool("isBackwards", isBackwards);
        IsMoving = isMoving;
        
        _prevPos = transform.position;
    }

    private Vector3 GetHorizontalVelocity()
    {
        if (characterController != null)
        {
            Vector3 v = characterController.velocity;
            v.y = 0f;
            return v;
        }
        // Fallback: estimate from position delta
        Vector3 delta = transform.position - _prevPos;
        delta.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        return delta / dt;
    }

    private Vector3 GetWorldSpaceInputXZ()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
#else
        Vector2 move = Vector2.zero; // New Input System only
#endif
        if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 xz;
        if (inputSpace != null)
        {
            Vector3 f = inputSpace.forward; f.y = 0f; f.Normalize();
            Vector3 r = inputSpace.right; r.y = 0f; r.Normalize();
            xz = r * move.x + f * move.y;
        }
        else
        {
            xz = new Vector3(move.x, 0f, move.y);
        }
        return xz.sqrMagnitude > 1f ? xz.normalized : xz;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeedEpsilon = Mathf.Max(0f, moveSpeedEpsilon);
        minSpeedForOpposite = Mathf.Max(0f, minSpeedForOpposite);
        backwardsXEpsilon = Mathf.Max(0f, backwardsXEpsilon);
    }
#endif

}
