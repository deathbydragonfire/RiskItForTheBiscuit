using UnityEngine;

[DisallowMultipleComponent]
public class StackablePiece : MonoBehaviour
{
    // --- Auto-detected links (read-only) ---
    public StackablePiece parentPiece { get; private set; }
    public StackablePiece childPiece { get; private set; }

    // --- Anchors (still per-model because they depend on mesh/pivot) ---
    [Header("Anchors (local)")]
    public Vector3 bottomAnchorLocal = Vector3.zero;
    public Vector3 topAnchorLocal = new Vector3(0f, 1f, 0f);

    // --- Overrides (optional) ---
    [Header("Overrides (optional)")]
    [Tooltip("If enabled, this piece uses the local values below instead of StackHub globals.")]
    public bool overrideSettings = false;

    [Tooltip("Only used if 'overrideSettings' is enabled.")]
    public Vector3 overrideStackGapWorld = new Vector3(0f, 0.01f, 0f);

    [Tooltip("Only used if 'overrideSettings' is enabled.")]
    public float overridePositionSpring = 40f;

    [Tooltip("Only used if 'overrideSettings' is enabled.")]
    public float overridePositionDamping = 12f;

    [Tooltip("Only used if 'overrideSettings' is enabled.")]
    public float overrideOrientSpeed = 8f;

    [Tooltip("Only used if 'overrideSettings' is enabled (0 = off).")]
    public float overrideBreakDistance = 0f;

    Rigidbody _rb;
    Vector3 _velSm;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb) _rb.interpolation = RigidbodyInterpolation.Interpolate;
        RefreshLinks();
    }

    void OnEnable() { RefreshLinks(); }
    void OnTransformParentChanged() { RefreshLinks(); }
    void OnTransformChildrenChanged() { RefreshLinks(); }

    void FixedUpdate()
    {
        if (_rb) TickFollow(Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        if (!_rb) TickFollow(Time.deltaTime);
    }

    void TickFollow(float dt)
    {
        if (!parentPiece) return;

        // Read settings (global or override)
        GetSettings(out float spring, out float damping, out float orient, out Vector3 gap, out float breakDist);

        Vector3 parentTop = parentPiece.GetTopAnchorWorld();
        Vector3 targetPos = parentTop + gap;

        if (breakDist > 0f)
        {
            float dist = Vector3.Distance(GetBottomAnchorWorld(), parentTop);
            if (dist > breakDist)
            {
                // stop following by clearing parent reference (keeps child as-is)
                parentPiece = null;
                return;
            }
        }

        FollowPosition(targetPos, dt, spring, damping);
        FollowOrientation(parentPiece.transform.rotation, dt, orient);
    }

    void FollowPosition(Vector3 targetPos, float dt, float spring, float damping)
    {
        if (_rb)
        {
            Vector3 anchorNow = GetBottomAnchorWorld();

            Vector3 posErr = targetPos - anchorNow;
            Vector3 desiredVel = posErr * Mathf.Max(0f, spring);

            Vector3 r = anchorNow - _rb.worldCenterOfMass;
            Vector3 anchorVel = _rb.linearVelocity + Vector3.Cross(_rb.angularVelocity, r);

            Vector3 accel = (desiredVel - anchorVel) * Mathf.Max(0f, damping);
            _rb.AddForceAtPosition(accel, anchorNow, ForceMode.Acceleration);
        }
        else
        {
            Vector3 bottomNow = GetBottomAnchorWorld();
            float smoothT = 1f / Mathf.Max(0.001f, damping);
            Vector3 nextAnchor = Vector3.SmoothDamp(bottomNow, targetPos, ref _velSm, smoothT, Mathf.Infinity, dt);
            transform.position += (nextAnchor - bottomNow);
        }
    }

    void FollowOrientation(Quaternion parentRot, float dt, float orient)
    {
        if (orient <= 0f) return;

        Quaternion current = transform.rotation;

        Vector3 fwd = Vector3.ProjectOnPlane(current * Vector3.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        Quaternion upright = Quaternion.LookRotation(fwd.normalized, Vector3.up);

        Vector3 parentFwd = Vector3.ProjectOnPlane(parentRot * Vector3.forward, Vector3.up).normalized;
        if (parentFwd.sqrMagnitude < 1e-6f) parentFwd = Vector3.forward;
        Quaternion parentYaw = Quaternion.LookRotation(parentFwd, Vector3.up);

        Quaternion target = Quaternion.Slerp(upright, parentYaw, 0.5f);

        if (_rb)
        {
            Quaternion delta = target * Quaternion.Inverse(current);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;
            Vector3 needed = axis * (angleDeg * Mathf.Deg2Rad) / Mathf.Max(0.001f, dt);
            Vector3 torque = (needed - _rb.angularVelocity) * orient;
            _rb.AddTorque(torque, ForceMode.Acceleration);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(current, target, dt * orient);
        }
    }

    // --------- Settings resolver ----------
    void GetSettings(out float spring, out float damping, out float orient, out Vector3 gap, out float breakDist)
    {
        if (overrideSettings || StackHub.Active == null)
        {
            spring = overridePositionSpring;
            damping = overridePositionDamping;
            orient = overrideOrientSpeed;
            gap = overrideStackGapWorld;
            breakDist = overrideBreakDistance;
            return;
        }

        var hub = StackHub.Active;
        spring = hub.positionSpring;
        damping = hub.positionDamping;
        orient = hub.orientSpeed;
        gap = hub.stackGapWorld;
        breakDist = hub.breakDistance;
    }

    // --------- Links & anchors ----------
    public Vector3 GetBottomAnchorWorld() => transform.TransformPoint(bottomAnchorLocal);
    public Vector3 GetTopAnchorWorld() => transform.TransformPoint(topAnchorLocal);

    public void RefreshLinks()
    {
        parentPiece = StackHub.FindNearestAncestorPiece(transform);
        childPiece = StackHub.FindDirectChildPiece(transform);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(Application.isPlaying ? GetBottomAnchorWorld() : transform.TransformPoint(bottomAnchorLocal), 0.03f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(Application.isPlaying ? GetTopAnchorWorld() : transform.TransformPoint(topAnchorLocal), 0.03f);

        if (parentPiece)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(GetBottomAnchorWorld(), parentPiece.GetTopAnchorWorld());
        }
    }
#endif
}
