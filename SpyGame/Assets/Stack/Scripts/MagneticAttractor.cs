using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MagneticAttractor : MonoBehaviour
{
    // --- Tuning ---
    [Header("Magnet Settings")]
    [Tooltip("Maximum distance at which attraction is applied.")]
    public float range = 3f;

    [Tooltip("Base strength of the attraction. Think of this as 'G' for your magnets.")]
    public float strength = 1f;

    [Tooltip("Minimum separation used for calculations to avoid huge forces at very small distances.")]
    public float minDistance = 0.15f;

    [Tooltip("Cap per-pair force magnitude (after falloff).")]
    public float maxForcePerPair = 10f;

    [Tooltip("Use linear falloff: 0 at range, 1 at contact (clamped by minDistance).")]
    public bool linearFalloff = true;

    [Tooltip("If not using linear falloff, force scales as 1 / distance^power within range.")]
    public float inversePower = 2f;

    [Header("Physics")]
    [Tooltip("Acceleration ignores mass, Force scales with mass.")]
    public ForceMode forceMode = ForceMode.Acceleration;

    [Tooltip("Optional: only attract if there is a clear line-of-sight (no obstacles in between).")]
    public bool requireLineOfSight = false;

    [Tooltip("Layers that block line-of-sight when 'Require Line Of Sight' is enabled.")]
    public LayerMask obstacleLayers = ~0;

    // --- Internals ---
    private static readonly HashSet<MagneticAttractor> All = new HashSet<MagneticAttractor>();
    private Rigidbody _rb;
    private int _id;

    private void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
        _id = GetInstanceID();
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        Vector3 myCOM = _rb.worldCenterOfMass;

        foreach (var other in All)
        {
            if (other == this) continue;

            // Ensure each pair is processed exactly once (lowest ID applies equal/opposite forces)
            if (_id > other._id) continue;

            Rigidbody otherRb = other._rb;
            if (otherRb == null) continue;

            Vector3 otherCOM = otherRb.worldCenterOfMass;
            Vector3 delta = otherCOM - myCOM;
            float dist = delta.magnitude;
            if (dist <= 1e-6f) continue;
            if (dist > range) continue;

            if (requireLineOfSight)
            {
                // If ANY obstacle in between, skip the attraction
                if (Physics.Raycast(myCOM, delta.normalized, dist, obstacleLayers, QueryTriggerInteraction.Ignore))
                    continue;
            }

            // Direction from me -> other
            Vector3 dir = delta / dist;

            float forceMag;
            if (linearFalloff)
            {
                // 0 at range, 1 near contact (clamped by minDistance)
                float d = Mathf.Max(minDistance, dist);
                float falloff = 1f - Mathf.Clamp01(dist / Mathf.Max(1e-6f, range));
                forceMag = strength * falloff;
            }
            else
            {
                // Classic-ish "gravity" style within a hard cutoff
                float d = Mathf.Max(minDistance, dist);
                forceMag = strength / Mathf.Pow(d, Mathf.Max(0.0001f, inversePower));
            }

            if (maxForcePerPair > 0f) forceMag = Mathf.Min(forceMag, maxForcePerPair);

            Vector3 force = dir * forceMag;

            // Apply equal and opposite forces once per pair
            _rb.AddForce(force, forceMode);
            if (!otherRb.isKinematic)
            {
                otherRb.AddForce(-force, forceMode);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, range);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
#endif
}
