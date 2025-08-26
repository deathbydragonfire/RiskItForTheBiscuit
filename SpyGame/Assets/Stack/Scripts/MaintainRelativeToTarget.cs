using UnityEngine;

/// <summary>
/// Keeps this kinematic Rigidbody at a fixed local offset (and optional rotation)
/// relative to a target Transform, without actually parenting it.
/// Great for sticking objects to moving platforms or rigs without hierarchy side effects.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(100)] // ensure this runs after most movement scripts
public class MaintainRelativeToTarget : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object to follow relative to.")]
    public Transform target;

    [Header("Behavior")]
    [Tooltip("Capture the starting offset/rotation from the current scene pose on Awake/OnEnable.")]
    public bool captureOffsetOnEnable = true;

    [Tooltip("Keep the initial local rotation offset as well as position.")]
    public bool maintainRotation = false;

    [Tooltip("If true, re-captures the offset when the target changes at runtime.")]
    public bool recalcOffsetOnTargetChange = true;

    [Header("Smoothing")]
    [Tooltip("0 = snap to target each FixedUpdate (recommended for strict sticking). " +
             "Higher values blend toward the desired pose per second.")]
    [Range(0f, 30f)] public float positionLerpSpeed = 0f;
    [Range(0f, 30f)] public float rotationLerpSpeed = 0f;

    [Header("Axis Constraints (optional)")]
    public bool lockX, lockY, lockZ = false;
    public bool lockRotX, lockRotY, lockRotZ = false;

    [Header("Teleport Handling")]
    [Tooltip("If target jumps more than this distance in a single step, snap without smoothing.")]
    public float teleportDistance = 5f;

    // Captured offsets
    [SerializeField, Tooltip("Local position of this object relative to target at capture time.")]
    private Vector3 localOffset;
    [SerializeField, Tooltip("Local rotation of this object relative to target at capture time.")]
    private Quaternion localRotationOffset = Quaternion.identity;

    private Rigidbody rb;
    private Transform cachedTarget;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;                 // REQUIRED
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smoother visuals
    }

    void OnEnable()
    {
        if (target == null) return;
        if (captureOffsetOnEnable) CaptureOffsets();
        cachedTarget = target;
    }

    /// <summary>
    /// Recalculate local offsets from current world poses.
    /// </summary>
    public void CaptureOffsets()
    {
        if (target == null) return;

        // Compute "this" in target local space
        localOffset = target.InverseTransformPoint(transform.position);
        localRotationOffset = Quaternion.Inverse(target.rotation) * transform.rotation;
    }

    /// <summary>
    /// Change target at runtime.
    /// </summary>
    public void SetTarget(Transform newTarget, bool recalcOffsets = true)
    {
        target = newTarget;
        if (target != null && (recalcOffsets || recalcOffsetOnTargetChange))
        {
            CaptureOffsets();
        }
        cachedTarget = target;
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // If target reference changed externally
        if (cachedTarget != target)
        {
            if (recalcOffsetOnTargetChange) CaptureOffsets();
            cachedTarget = target;
        }

        // Compute desired world pose from stored offsets.
        Vector3 desiredPos = target.TransformPoint(localOffset);
        Quaternion desiredRot = target.rotation * localRotationOffset;

        // Axis position locks (apply in world space)
        Vector3 currentPos = rb.position;
        if (lockX) desiredPos.x = currentPos.x;
        if (lockY) desiredPos.y = currentPos.y;
        if (lockZ) desiredPos.z = currentPos.z;

        // Rotation axis locks (apply in world space using Euler)
        if (maintainRotation)
        {
            Vector3 eDesired = desiredRot.eulerAngles;
            Vector3 eCurrent = rb.rotation.eulerAngles;
            if (lockRotX) eDesired.x = eCurrent.x;
            if (lockRotY) eDesired.y = eCurrent.y;
            if (lockRotZ) eDesired.z = eCurrent.z;
            desiredRot = Quaternion.Euler(eDesired);
        }

        // Teleport detection: snap if target jumped far
        if (Vector3.Distance(currentPos, desiredPos) > teleportDistance)
        {
            rb.MovePosition(desiredPos);
            if (maintainRotation) rb.MoveRotation(desiredRot);
            return;
        }

        // Optional smoothing (per second rates, applied per fixed step)
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

        // Drive via Rigidbody for correct physics sync
        rb.MovePosition(desiredPos);
        if (maintainRotation) rb.MoveRotation(desiredRot);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.cyan;
        Vector3 p = Application.isPlaying ? target.TransformPoint(localOffset)
                                          : target.TransformPoint(localOffset);
        Gizmos.DrawWireSphere(p, 0.1f);
        Gizmos.DrawLine(target.position, p);
    }
#endif
}
