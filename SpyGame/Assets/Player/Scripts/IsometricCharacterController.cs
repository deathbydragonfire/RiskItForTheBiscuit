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
    public float gravity = 30f;            // positive
    public float groundStickForce = 4f;    // small downward while grounded

#if ENABLE_INPUT_SYSTEM
    [Header("Input System")]
    public InputActionReference moveActionRef;
    private InputAction _moveAction;
#else
    private readonly string _hor = "Horizontal";
    private readonly string _ver = "Vertical";
#endif

    [Header("External Motion (simple)")]
    public PlayerCollisionDebugger collisionDebugger;   // reports StandingOnCollider & PushingContacts
    [Tooltip("Inherit horizontal velocity from the platform you're standing on.")]
    public bool inheritPlatformHorizontal = true;
    [Tooltip("Inherit upward velocity from the platform (ignore downward).")]
    public bool inheritPlatformVerticalUp = false;

    [Header("Side Push (optional, off by default)")]
    public bool enableSidePush = false;
    [Tooltip("Clamp for side push speed (m/s).")]
    public float sidePushMaxSpeed = 2.5f;
    [Tooltip("Smooth time (s) for side push velocity.")]
    public float sidePushSmoothTime = 0.08f;
    [Tooltip("Ignore vertical from side pushers.")]
    public bool sidePushIgnoreVertical = true;

    [Header("Camera Follow (for Shake)")]
    [Tooltip("CameraFollow component to shake. If left empty, will try FindObjectOfType at runtime.")]
    public CameraFollow cameraFollow;

    [Header("Landing Shake — Small Fall")]
    public float smallFallThreshold = 8f;
    public float smallFallDuration = 0.18f;
    public float smallMagPerUnit = 0.015f;
    public float smallMaxMagnitude = 0.25f;

    [Header("Landing Shake — Big Fall")]
    public float bigFallThreshold = 16f;
    public float bigFallDuration = 0.32f;
    public float bigMagPerUnit = 0.03f;
    public float bigMaxMagnitude = 0.55f;

    [Header("Shake Misc")]
    public float fallShakeCooldown = 0.10f;

    private CharacterController controller;
    private Vector3 velocity;   // (x,z,y) final
    private Vector3 horizVel;   // player-intent horizontal
    private bool isGrounded;

    // side push smoothing state
    private Vector3 sidePushVelCurrent;
    private Vector3 sidePushVelRef;

    // landing shake tracking
    private float maxDownwardSpeedWhileAirborne = 0f;
    private float lastShakeTime = -999f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (collisionDebugger == null) collisionDebugger = GetComponent<PlayerCollisionDebugger>();
        if (cameraFollow == null) cameraFollow = FindObjectOfType<CameraFollow>();
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveActionRef != null) _moveAction = moveActionRef.action;
        else
        {
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            var wasd = _moveAction.AddCompositeBinding("2DVector");
            wasd.With("Up", "<Keyboard>/w"); wasd.With("Down", "<Keyboard>/s");
            wasd.With("Left", "<Keyboard>/a"); wasd.With("Right", "<Keyboard>/d");
            var arrows = _moveAction.AddCompositeBinding("2DVector");
            arrows.With("Up", "<Keyboard>/upArrow"); arrows.With("Down", "<Keyboard>/downArrow");
            arrows.With("Left", "<Keyboard>/leftArrow"); arrows.With("Right", "<Keyboard>/rightArrow");
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
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        bool wasGrounded = controller.isGrounded;

        // --- Input → target horizontal velocity
        Vector2 input = ReadMoveInput();
        Vector3 inputDir = new Vector3(input.x, 0f, input.y);
        if (inputDir.sqrMagnitude > 1e-4f) inputDir.Normalize();

        Vector3 targetHorizVel = inputDir * moveSpeed;
        float accel = (targetHorizVel.sqrMagnitude > 1e-4f) ? acceleration : deceleration;
        horizVel = Vector3.MoveTowards(horizVel, targetHorizVel, accel * dt);

        // --- Platform inherit (very simple)
        Vector3 platHorizVel = Vector3.zero;
        float platUp = 0f;

        if (collisionDebugger && collisionDebugger.StandingOnCollider)
        {
            var rbPlat = collisionDebugger.StandingOnCollider.attachedRigidbody;
            if (rbPlat)
            {
                Vector3 pv = rbPlat.GetPointVelocity(transform.position);
                if (inheritPlatformHorizontal) platHorizVel = new Vector3(pv.x, 0f, pv.z);
                if (inheritPlatformVerticalUp && pv.y > 0f) platUp = pv.y;
            }
        }

        // --- Optional side push (simple & tame)
        Vector3 sidePushTarget = Vector3.zero;
        if (enableSidePush && collisionDebugger && collisionDebugger.PushingContacts.Count > 0)
        {
            Vector3 sum = Vector3.zero; int count = 0;
            foreach (var col in collisionDebugger.PushingContacts)
            {
                if (!col) continue;
                var rbOther = col.attachedRigidbody;
                if (!rbOther) continue;

                Vector3 v = rbOther.GetPointVelocity(transform.position);

                // Project off inward normal so we slide instead of bouncing
                Vector3 closest = col.ClosestPoint(transform.position);
                Vector3 normal = (transform.position - closest);
                if (normal.sqrMagnitude < 1e-6f) normal = (transform.position - col.bounds.center);
                if (normal.sqrMagnitude > 1e-6f) normal.Normalize(); else normal = Vector3.forward;

                v = Vector3.ProjectOnPlane(v, normal);
                if (sidePushIgnoreVertical) v.y = 0f;

                sum += v; count++;
            }
            if (count > 0)
            {
                sidePushTarget = sum / count;

                // clamp horizontal magnitude
                Vector3 h = new Vector3(sidePushTarget.x, 0f, sidePushTarget.z);
                float hMag = h.magnitude;
                if (hMag > sidePushMaxSpeed && hMag > 1e-4f)
                    h *= sidePushMaxSpeed / hMag;
                sidePushTarget.x = h.x; sidePushTarget.z = h.z;
                if (sidePushIgnoreVertical) sidePushTarget.y = 0f;
            }
        }
        sidePushVelCurrent = Vector3.SmoothDamp(sidePushVelCurrent, sidePushTarget, ref sidePushVelRef, sidePushSmoothTime, Mathf.Infinity, dt);

        // --- Vertical + landing tracking
        isGrounded = controller.isGrounded;
        if (isGrounded)
        {
            // stick + optional platform lift
            velocity.y = -groundStickForce + platUp;
        }
        else
        {
            velocity.y -= gravity * dt;

            // Track peak downward speed for landing shake
            if (velocity.y < 0f)
                maxDownwardSpeedWhileAirborne = Mathf.Max(maxDownwardSpeedWhileAirborne, -velocity.y);
        }

        // --- Combine & move
        Vector3 totalHoriz = horizVel + platHorizVel + new Vector3(sidePushVelCurrent.x, 0f, sidePushVelCurrent.z);
        velocity.x = totalHoriz.x;
        velocity.z = totalHoriz.z;

        controller.Move(velocity * dt);

        // --- Landing detection -> shake
        bool groundedNow = controller.isGrounded;
        if (!wasGrounded && groundedNow)
        {
            HandleLandingShake(maxDownwardSpeedWhileAirborne);
            maxDownwardSpeedWhileAirborne = 0f;
        }
        isGrounded = groundedNow;
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

    // --- Camera shake on landing (same logic you had) ---
    void HandleLandingShake(float impactSpeed)
    {
        if (cameraFollow == null) return;
        if (Time.time - lastShakeTime < fallShakeCooldown) return;

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
    }

    public bool IsGrounded => isGrounded;
}
