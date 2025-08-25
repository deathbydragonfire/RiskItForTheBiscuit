using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WobblyStackRotator : MonoBehaviour
{
    [Header("Drive")]
    public float driveFromVelocity = 0.12f;
    public float driveFromAccel = 0.6f;
    public bool invertLean = true; // +X move => lean -X

    [Header("Spring-Damper per level")]
    public float spring = 18f;
    public float damping = 8f;
    [Range(0f, 1f)] public float uprightBias = 0.4f; // tiny pull to baseline
    public float maxLeanDegLevel0 = 8f;

    [Header("Propagation up the chain")]
    [Tooltip(">1 means each level above leans more (e.g., 1.15 = +15% per level).")]
    public float amplifyPerLevel = 1.15f;

    [Tooltip("Seconds of extra lag per level (0–0.03).")]
    [Range(0f, 0.05f)] public float lagPerLevel = 0.01f;

    [Header("Only wobble chain pieces")]
    public bool requireStackablePiece = true;

    struct State { public Vector2 ang, angVel; public Quaternion baseLocal; }
    readonly List<Transform> _chain = new List<Transform>();
    readonly Dictionary<Transform, State> _map = new Dictionary<Transform, State>();

    Vector3 _prevPos, _vel, _acc;

    void OnEnable() { CaptureChain(); ResetKinematics(); }
    void OnTransformChildrenChanged() { CaptureChain(); }

    void ResetKinematics()
    {
        _prevPos = transform.position; _vel = Vector3.zero; _acc = Vector3.zero;
    }

    void CaptureChain()
    {
        _chain.Clear(); _map.Clear();
        Transform t = transform.childCount > 0 ? transform.GetChild(0) : null;
        while (t)
        {
            if (!requireStackablePiece || t.GetComponent<StackablePiece>())
            {
                _chain.Add(t);
                _map[t] = new State { ang = Vector2.zero, angVel = Vector2.zero, baseLocal = t.localRotation };
                t = t.childCount > 0 ? t.GetChild(0) : null;
            }
            else break;
        }
    }

    /// <summary>
    /// Explicitly remove a node from the chain (e.g., if it breaks off).
    /// Call this BEFORE destroying the node.
    /// </summary>
    public void RemoveNode(Transform node)
    {
        if (node == null) return;

        _chain.Remove(node);
        _map.Remove(node);

        // optional: recapture to be absolutely safe
        CaptureChain();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;

        // root kinematics
        var p = transform.position;
        var newVel = (p - _prevPos) / Mathf.Max(dt, 1e-5f);
        _acc = (newVel - _vel) / Mathf.Max(dt, 1e-5f);
        _vel = Vector3.Lerp(_vel, newVel, 0.5f);
        _prevPos = p;

        var inv = Quaternion.Inverse(transform.rotation);
        var v = inv * _vel; var a = inv * _acc;

        Vector2 baseDrive =
            new Vector2(v.z, -v.x) * driveFromVelocity +
            new Vector2(a.z, -a.x) * driveFromAccel;

        if (invertLean) baseDrive = -baseDrive;

        // step each level
        for (int i = 0; i < _chain.Count; i++)
        {
            var t = _chain[i];
            if (!_map.TryGetValue(t, out var s)) continue;

            float gain = Mathf.Pow(amplifyPerLevel, i);
            float maxLean = maxLeanDegLevel0 * gain;

            Vector2 target = baseDrive * gain + (-s.ang) * (uprightBias * 0.01f);
            float alpha = dt / Mathf.Max(1e-4f, lagPerLevel * i + dt);
            Vector2 lagged = Vector2.Lerp(s.ang, target, alpha);

            Vector2 err = lagged - s.ang;
            Vector2 acc = err * spring - s.angVel * damping;
            s.angVel += acc * dt;
            s.ang += s.angVel * dt;

            float mag = s.ang.magnitude;
            if (mag > maxLean) s.ang *= maxLean / mag;

            var tilt =
                Quaternion.AngleAxis(s.ang.x, Vector3.right) *
                Quaternion.AngleAxis(s.ang.y, Vector3.forward);

            t.localRotation = s.baseLocal * tilt;
            _map[t] = s;
        }
    }
}
