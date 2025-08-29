using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Keeps this kinematic Rigidbody at a fixed local offset (and optional rotation)
/// relative to a target Transform, without parenting it.
///
/// Shift (press) to TOGGLE:
///  - If following: stop following and LOWER this item + all "Stack" items above it by dropAmount
///    (true teleport; preserves each item's offset relative to THIS object).
///  - If NOT following and a Player-layer collider is within range: teleport the same group to the stored
///    hold pose and RESUME following (offsets preserved).
/// Also sets the player's Animator bool (isCarrying) on pickup/putdown.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(100)]
public class MaintainRelativeToTarget : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Behavior")]
    public bool captureOffsetOnEnable = true;
    public bool maintainRotation = false;
    public bool recalcOffsetOnTargetChange = true;

    [Header("Smoothing")]
    [Range(0f, 30f)] public float positionLerpSpeed = 0f;
    [Range(0f, 30f)] public float rotationLerpSpeed = 0f;

    [Header("Axis Constraints (optional)")]
    public bool lockX, lockY, lockZ = false;
    public bool lockRotX, lockRotY, lockRotZ = false;

    [Header("Teleport Handling")]
    public float teleportDistance = 5f;

    // --- Pickup / Putdown toggle ---
    [Header("Pickup / Putdown Toggle")]
    public float dropAmount = 0.5f;
    public float playerPickupRange = 2.0f;

    [Tooltip("Layer name used to detect the player for pickup (e.g., 'Player').")]
    public string playerLayerName = "Player";

    [Tooltip("Layer name for stacked items (e.g., 'Stack').")]
    public string stackLayerName = "Stack";

    [Header("Stack Search (above held item)")]
    [Tooltip("Horizontal radius of the vertical capsule used to collect stack pieces above.")]
    public float stackSearchRadius = 0.75f;
    [Tooltip("Height of the vertical capsule (starts at this object's position and extends upward).")]
    public float stackSearchHeight = 6f;
    [Tooltip("How much below this object is still considered 'above' (tolerance).")]
    public float aboveYTolerance = 0.01f;

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New Input System)")]
    [Tooltip("Optional: if provided, this action toggles pickup/putdown. Otherwise we read Left/Right Shift from Keyboard.current.")]
    public InputActionReference toggleActionRef;
    private InputAction _toggleAction;
#endif

    [Header("Animator Hook (Player)")]
    [Tooltip("Optional: player's Animator. If empty, the script tries to find one via 'target' or nearby Player-layer colliders.")]
    public Animator playerAnimator;
    [Tooltip("Animator Bool parameter to flip on pickup/putdown.")]
    public string carryingBoolParam = "isCarrying";

    [Header("Defaults")]
    [Tooltip("If true, sets the Animator 'isCarrying' to true on Start().")]
    public bool startWithCarryingTrue = true;

    // Captured offsets to target/hold
    [SerializeField] private Vector3 localOffset;
    [SerializeField] private Quaternion localRotationOffset = Quaternion.identity;

    private int carryingHash;
    private bool animatorParamCached;
    private bool animatorHasParam;

    private Rigidbody rb;
    private Transform cachedTarget;
    private bool isFollowing = false;
    private int stackLayer = -1;
    private int playerLayer = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // REQUIRED
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        stackLayer = LayerMask.NameToLayer(stackLayerName);
        playerLayer = LayerMask.NameToLayer(playerLayerName);

        carryingHash = Animator.StringToHash(carryingBoolParam);

        isFollowing = (target != null);
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleActionRef != null)
        {
            _toggleAction = toggleActionRef.action;
            if (_toggleAction != null) _toggleAction.Enable();
        }
#endif
        if (target != null && captureOffsetOnEnable) CaptureOffsets();
        cachedTarget = target;
    }

    // Set default carrying state AFTER everything is initialized
    void Start()
    {
        if (startWithCarryingTrue)
            SetCarrying(true);
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_toggleAction != null) _toggleAction.Disable();
#endif
    }

    public void CaptureOffsets()
    {
        if (target == null) return;
        localOffset = target.InverseTransformPoint(transform.position);
        localRotationOffset = Quaternion.Inverse(target.rotation) * transform.rotation;
    }

    public void SetTarget(Transform newTarget, bool recalcOffsets = true)
    {
        target = newTarget;
        if (target != null && (recalcOffsets || recalcOffsetOnTargetChange))
            CaptureOffsets();
        cachedTarget = target;
        isFollowing = (target != null) && isFollowing;
    }

    void Update()
    {
        if (WasTogglePressedThisFrame())
        {
            if (isFollowing) PutDown();
            else TryPickUp();
        }
    }

    bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
            return true;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftShiftKey.wasPressedThisFrame ||
                Keyboard.current.rightShiftKey.wasPressedThisFrame)
                return true;
        }
#endif
        return false;
    }

    void FixedUpdate()
    {
        if (!isFollowing || target == null) return;

        if (cachedTarget != target)
        {
            if (recalcOffsetOnTargetChange) CaptureOffsets();
            cachedTarget = target;
        }

        Vector3 desiredPos = target.TransformPoint(localOffset);
        Quaternion desiredRot = target.rotation * localRotationOffset;

        Vector3 currentPos = rb.position;
        if (lockX) desiredPos.x = currentPos.x;
        if (lockY) desiredPos.y = currentPos.y;
        if (lockZ) desiredPos.z = currentPos.z;

        if (maintainRotation)
        {
            Vector3 eDesired = desiredRot.eulerAngles;
            Vector3 eCurrent = rb.rotation.eulerAngles;
            if (lockRotX) eDesired.x = eCurrent.x;
            if (lockRotY) eDesired.y = eCurrent.y;
            if (lockRotZ) eDesired.z = eCurrent.z;
            desiredRot = Quaternion.Euler(eDesired);
        }

        if (Vector3.Distance(currentPos, desiredPos) > teleportDistance)
        {
            rb.MovePosition(desiredPos);
            if (maintainRotation) rb.MoveRotation(desiredRot);
            return;
        }

        if (positionLerpSpeed > 0f)
        {
            float t = 1f - Mathf.Exp(-positionLerpSpeed * Time.fixedDeltaTime);
            desiredPos = Vector3.Lerp(currentPos, desiredPos, t);
        }
        if (maintainRotation && rotationLerpSpeed > 0f)
        {
            float t = 1f - Mathf.Exp(-rotationLerpSpeed * Time.fixedDeltaTime);
            desiredRot = Quaternion.Slerp(rb.rotation, desiredRot, t);
        }

        rb.MovePosition(desiredPos);
        if (maintainRotation) rb.MoveRotation(desiredRot);
    }

    // --- Toggle ops ---

    void PutDown()
    {
        isFollowing = false;

        var group = CollectGroupAbove(includeSelf: true);

        Vector3 newHeldPos = transform.position + Vector3.down * dropAmount;
        Quaternion newHeldRot = transform.rotation; // keep current rot
        TeleportGroupPreservingOffsets(group, newHeldPos, newHeldRot, rotateOthers: false);

        SetCarrying(false);
    }

    void TryPickUp()
    {
        if (!IsPlayerLayerWithinRange()) return;
        if (target == null) return;

        Vector3 holdPos = target.TransformPoint(localOffset);
        Quaternion holdRot = target.rotation * localRotationOffset;

        var group = CollectGroupAbove(includeSelf: true);
        TeleportGroupPreservingOffsets(group, holdPos, maintainRotation ? holdRot : transform.rotation, rotateOthers: false);

        isFollowing = true;
        SetCarrying(true);
    }

    // --- Animator helpers ---

    void SetCarrying(bool value)
    {
        if (!EnsurePlayerAnimator()) return;

        if (!animatorParamCached)
        {
            animatorHasParam = AnimatorHasBool(playerAnimator, carryingHash);
            animatorParamCached = true;
        }

        if (animatorHasParam)
            playerAnimator.SetBool(carryingBoolParam, value);
    }

    bool EnsurePlayerAnimator()
    {
        if (playerAnimator != null) return true;

        // Prefer Animator via target's hierarchy (common case)
        if (target != null)
        {
            playerAnimator = target.GetComponentInParent<Animator>();
            if (playerAnimator == null)
                playerAnimator = target.GetComponentInChildren<Animator>();
            if (playerAnimator != null) { animatorParamCached = false; return true; }
        }

        // Fallback: nearest Player-layer collider in range
        if (playerLayer >= 0)
        {
            int mask = 1 << playerLayer;
            var hits = Physics.OverlapSphere(transform.position, playerPickupRange, mask, QueryTriggerInteraction.Collide);
            float bestSqr = float.PositiveInfinity;
            Animator best = null;
            foreach (var h in hits)
            {
                if (!h) continue;
                var a = h.GetComponentInParent<Animator>();
                if (a == null) a = h.GetComponentInChildren<Animator>();
                if (a == null) continue;
                float d2 = (a.transform.position - transform.position).sqrMagnitude;
                if (d2 < bestSqr) { bestSqr = d2; best = a; }
            }
            if (best != null)
            {
                playerAnimator = best;
                animatorParamCached = false;
                return true;
            }
        }

        return false;
    }

    static bool AnimatorHasBool(Animator anim, int nameHash)
    {
        if (anim == null) return false;
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].type == AnimatorControllerParameterType.Bool && ps[i].nameHash == nameHash)
                return true;
        }
        return false;
    }

    bool IsPlayerLayerWithinRange()
    {
        if (playerLayer < 0) return false;
        int mask = 1 << playerLayer;
        var hits = Physics.OverlapSphere(transform.position, playerPickupRange, mask, QueryTriggerInteraction.Collide);
        return hits != null && hits.Length > 0;
    }

    // --- Stack collection & teleport ---

    List<Transform> CollectGroupAbove(bool includeSelf)
    {
        var set = new HashSet<Transform>();
        var list = new List<Transform>();

        if (includeSelf)
        {
            set.Add(transform);
            list.Add(transform);
        }

        if (stackLayer < 0) return list;

        Vector3 basePos = transform.position;
        Vector3 topPos = basePos + Vector3.up * Mathf.Max(0.01f, stackSearchHeight);
        float radius = Mathf.Max(0.01f, stackSearchRadius);
        int stackMask = 1 << stackLayer;

        var cols = Physics.OverlapCapsule(basePos, topPos, radius, stackMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            if (!c) continue;

            Transform t = c.attachedRigidbody ? c.attachedRigidbody.transform : c.transform;
            if (t == transform) continue;

            float alongUp = Vector3.Dot(t.position - basePos, Vector3.up);
            if (alongUp + aboveYTolerance < 0f) continue;

            if (set.Add(t)) list.Add(t);
        }

        list.Sort((a, b) => a.position.y.CompareTo(b.position.y));
        return list;
    }

    void TeleportGroupPreservingOffsets(List<Transform> group, Vector3 newHeldPos, Quaternion newHeldRot, bool rotateOthers)
    {
        Vector3 heldPos0 = transform.position;
        Quaternion heldRot0 = transform.rotation;

        var bodies = new List<Rigidbody>(group.Count);
        var wasKinematic = new List<bool>(group.Count);
        var wasDetect = new List<bool>(group.Count);
        var oldInterp = new List<RigidbodyInterpolation>(group.Count);
        var relPos = new List<Vector3>(group.Count);
        var relRot = new List<Quaternion>(group.Count);

        foreach (var t in group)
        {
            var body = t ? t.GetComponent<Rigidbody>() : null;
            bodies.Add(body);

            if (body != null)
            {
                wasKinematic.Add(body.isKinematic);
                wasDetect.Add(body.detectCollisions);
                oldInterp.Add(body.interpolation);

                body.isKinematic = true;
                body.detectCollisions = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                wasKinematic.Add(false);
                wasDetect.Add(false);
                oldInterp.Add(RigidbodyInterpolation.None);
            }

            Vector3 rp = Quaternion.Inverse(heldRot0) * (t.position - heldPos0);
            relPos.Add(rp);

            Quaternion rr = Quaternion.Inverse(heldRot0) * t.rotation;
            relRot.Add(rr);
        }

        for (int i = 0; i < group.Count; i++)
        {
            var t = group[i];
            if (!t) continue;

            Vector3 targetPos = newHeldPos + (newHeldRot * relPos[i]);
            var body = bodies[i];

            if (body != null) body.position = targetPos; else t.position = targetPos;

            if (t == transform)
            {
                if (body != null) body.rotation = newHeldRot; else t.rotation = newHeldRot;
            }
            else if (rotateOthers)
            {
                Quaternion targetRot = newHeldRot * relRot[i];
                if (body != null) body.rotation = targetRot; else t.rotation = targetRot;
            }
        }

        Physics.SyncTransforms();

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null) continue;
            body.detectCollisions = wasDetect[i];
            body.isKinematic = wasKinematic[i];
            body.interpolation = oldInterp[i];
            body.Sleep();
        }
    }


}
