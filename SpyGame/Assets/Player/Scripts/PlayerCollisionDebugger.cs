using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class PlayerCollisionDebugger : MonoBehaviour
{
    // ───────────── Public values (same API) ─────────────
    public Collider StandingOnCollider { get; private set; }
    public List<Collider> SideContacts { get; } = new List<Collider>();
    public List<Collider> PushingContacts { get; } = new List<Collider>();

    public bool IsStanding => StandingOnCollider != null;
    public bool IsTouchingSide => SideContacts.Count > 0;
    public bool IsPushing => PushingContacts.Count > 0;

    // ───────────── Inspector ─────────────
    [Header("Setup (auto-detected if present)")]
    public CharacterController characterController;
    public Rigidbody rb;

    [Header("Layers")]
    public LayerMask surfaceMask = ~0;

    [Header("Ground Probe")]
    public float groundProbeDistance = 0.3f;
    public float groundProbeRadius = 0.25f;
    public float groundProbeStartOffset = 0.1f;
    [Tooltip("Keep last valid ground for this many seconds if it momentarily disappears.")]
    public float standingHoldSeconds = 0.15f;

    [Header("Side Detection (Overlap Capsule)")]
    public float sideProbeInflation = 0.05f;
    [Tooltip("|dot(up, dirToContact)| must be below this to count as 'side' (lower = stricter).")]
    [Range(0f, 1f)] public float sideVerticalDotThreshold = 0.65f;
    [Tooltip("Keep a side contact visible for this many seconds after last detection.")]
    public float sideHoldSeconds = 0.20f;

    [Header("Pushing Detection")]
    [Tooltip("Minimum other Rigidbody speed to consider it pushing.")]
    public float minPushingSpeed = 0.05f;
    [Tooltip("Other's velocity points this much toward player (1 = exactly toward).")]
    [Range(0f, 1f)] public float pushingDotThreshold = 0.45f;
    [Tooltip("Fallback: our velocity pointing into contact this much counts as pushing.")]
    [Range(0f, 1f)] public float selfOpposeDotThreshold = 0.6f;
    [Tooltip("Keep a pusher visible for this many seconds after last detection.")]
    public float pushingHoldSeconds = 0.20f;

    [Header("Overlay")]
    public bool showOverlay = true;

    [Header("Gizmo Display")]
    public bool showProbeGizmo = true;
    public Color probeNoHitColor = new Color(0.2f, 1f, 0.2f, 0.35f);
    public Color probeHitColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    [Header("Velocity (optional)")]
    [Tooltip("If non-zero, overrides our own velocity estimate (used for fallback pushing check).")]
    public Vector3 externalVelocityOverride;

    // ───────────── Internal ─────────────
    private Vector3 lastPos;
    private Vector3 estimatedVelocity;

    private float _lastStandingTime = -999f;
    private Collider _lastStanding;

    private readonly Dictionary<Collider, float> _sideLastSeen = new Dictionary<Collider, float>();
    private readonly Dictionary<Collider, float> _pushLastSeen = new Dictionary<Collider, float>();

    // Non-alloc buffers
    private readonly Collider[] _overlapBuf = new Collider[32];

    void Reset()
    {
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (!characterController) characterController = GetComponent<CharacterController>();
        if (!rb) rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
    }

    // Run after your controller moves in Update()
    void LateUpdate()
    {
        // Estimate our velocity (for fallback pushing only)
        if (externalVelocityOverride != Vector3.zero) estimatedVelocity = externalVelocityOverride;
        else if (rb) estimatedVelocity = rb.linearVelocity;
        else
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-6f);
            estimatedVelocity = (transform.position - lastPos) / dt;
        }
        lastPos = transform.position;

        RefreshStanding();
        RefreshSideAndPushing();
        BuildPublicLists();
    }

    // ---------- Ground (with hold/hysteresis) ----------
    private Vector3 FeetOrigin()
    {
        if (characterController)
        {
            Vector3 centerW = transform.TransformPoint(characterController.center);
            float feetDown = characterController.height * 0.5f - characterController.skinWidth;
            Vector3 feet = centerW + Vector3.down * feetDown;
            return feet + Vector3.up * groundProbeStartOffset;
        }
        return transform.position + Vector3.up * groundProbeStartOffset;
    }

    private void RefreshStanding()
    {
        Collider hitCol = null;

        Vector3 origin = FeetOrigin();
        if (Physics.SphereCast(origin, groundProbeRadius, Vector3.down, out RaycastHit hit,
                               groundProbeDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.3f)
            {
                hitCol = hit.collider;
            }
        }

        if (hitCol != null)
        {
            StandingOnCollider = hitCol;
            _lastStanding = hitCol;
            _lastStandingTime = Time.time;
        }
        else
        {
            // Hysteresis: keep last ground briefly to avoid flicker on upward-moving platforms
            if (_lastStanding && (Time.time - _lastStandingTime) <= standingHoldSeconds)
            {
                StandingOnCollider = _lastStanding;
            }
            else
            {
                StandingOnCollider = null;
            }
        }
    }

    // ---------- Sides & Pushing (overlap-based + hold) ----------
    private void RefreshSideAndPushing()
    {
        if (!characterController)
        {
            DecayHoldsOnly();
            return;
        }

        // CharacterController capsule in world space
        Vector3 cCenter = transform.TransformPoint(characterController.center);
        float hh = Mathf.Max(0f, characterController.height * 0.5f - characterController.radius);
        Vector3 top = cCenter + Vector3.up * hh;
        Vector3 bottom = cCenter - Vector3.up * hh;
        float radius = characterController.radius + sideProbeInflation;

        // Overlap
        int n = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, _overlapBuf, surfaceMask, QueryTriggerInteraction.Ignore);
        float now = Time.time;

        // Mark seen this frame
        var seenThisFrame = HashSetPool<Collider>.Get();
        for (int i = 0; i < n; i++)
        {
            var col = _overlapBuf[i];
            if (!col) continue;
            if (col.transform.root == transform.root) continue; // ignore self

            // Classify as side: direction to closest point not too vertical
            Vector3 closest = col.ClosestPoint(cCenter);
            Vector3 dir = (closest - cCenter);
            if (dir.sqrMagnitude < 1e-6f) dir = (col.bounds.center - cCenter); // fallback
            float upDot = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up));
            if (upDot >= sideVerticalDotThreshold) continue;

            // Side contact seen
            _sideLastSeen[col] = now;
            seenThisFrame.Add(col);

            // Pushing test
            bool pushing = false;
            var otherRb = col.attachedRigidbody;
            if (otherRb && otherRb.linearVelocity.sqrMagnitude > (minPushingSpeed * minPushingSpeed))
            {
                Vector3 toPlayer = (cCenter - (otherRb ? otherRb.worldCenterOfMass : col.bounds.center)).normalized;
                float toward = Vector3.Dot(otherRb.linearVelocity.normalized, toPlayer);
                if (toward > pushingDotThreshold)
                    pushing = true;
            }

            // Fallback: we are moving into that contact
            if (!pushing && estimatedVelocity.sqrMagnitude > 1e-4f)
            {
                Vector3 toOther = (closest - cCenter).normalized;
                float oppose = Vector3.Dot(estimatedVelocity.normalized, toOther);
                if (oppose > selfOpposeDotThreshold)
                    pushing = true;
            }

            if (pushing) _pushLastSeen[col] = now;
        }
        HashSetPool<Collider>.Release(seenThisFrame);

        // Decay holds (remove items not refreshed within hold window)
        DecayHoldsOnly();
    }

    private void DecayHoldsOnly()
    {
        float now = Time.time;

        // Side
        var rmSide = ListPool<Collider>.Get();
        foreach (var kv in _sideLastSeen)
            if (now - kv.Value > sideHoldSeconds) rmSide.Add(kv.Key);
        foreach (var c in rmSide) _sideLastSeen.Remove(c);
        ListPool<Collider>.Release(rmSide);

        // Pushing
        var rmPush = ListPool<Collider>.Get();
        foreach (var kv in _pushLastSeen)
            if (now - kv.Value > pushingHoldSeconds) rmPush.Add(kv.Key);
        foreach (var c in rmPush) _pushLastSeen.Remove(c);
        ListPool<Collider>.Release(rmPush);
    }

    private void BuildPublicLists()
    {
        SideContacts.Clear();
        foreach (var c in _sideLastSeen.Keys)
            if (c) SideContacts.Add(c);

        PushingContacts.Clear();
        foreach (var c in _pushLastSeen.Keys)
            if (c) PushingContacts.Add(c);
    }

    // ---------- Overlay ----------
    void OnGUI()
    {
        if (!showOverlay) return;

        var style = new GUIStyle { fontSize = 16, alignment = TextAnchor.UpperRight };
        style.normal.textColor = Color.red;

        float width = 640f, lineH = 22f;
        float x = Screen.width - width - 10f, y = 10f;

        string standingName = StandingOnCollider ? SafeName(StandingOnCollider) : "None";
        GUI.Label(new Rect(x, y, width, lineH), $"Standing on: {standingName}", style); y += lineH;

        string sideNames = SideContacts.Count > 0 ? string.Join(", ", SideContacts.Select(SafeName)) : "None";
        GUI.Label(new Rect(x, y, width, lineH), $"Sides touching: {sideNames}", style); y += lineH;

        string pushNames = PushingContacts.Count > 0 ? string.Join(", ", PushingContacts.Select(SafeName)) : "None";
        GUI.Label(new Rect(x, y, width, lineH), $"Pushing me: {pushNames}", style);
    }

    private string SafeName(Collider c)
    {
        if (!c) return "None";
        var root = c.transform.root;
        return root ? root.name : c.name;
    }

    // ---------- Gizmo ----------
    void OnDrawGizmos()
    {
        if (!showProbeGizmo) return;

        Vector3 origin = FeetOrigin();
        Vector3 end = origin + Vector3.down * groundProbeDistance;

        Gizmos.color = (Application.isPlaying && StandingOnCollider != null) ? probeHitColor : probeNoHitColor;
        Gizmos.DrawSphere(end, groundProbeRadius);
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(end, groundProbeRadius);
        Gizmos.DrawLine(origin, end);
    }

    // --- tiny pooled helpers to avoid GC ---
    static class HashSetPool<T>
    {
        static readonly Stack<HashSet<T>> pool = new Stack<HashSet<T>>();
        public static HashSet<T> Get() => pool.Count > 0 ? pool.Pop() : new HashSet<T>();
        public static void Release(HashSet<T> s) { s.Clear(); pool.Push(s); }
    }
    static class ListPool<T>
    {
        static readonly Stack<List<T>> pool = new Stack<List<T>>();
        public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>();
        public static void Release(List<T> l) { l.Clear(); pool.Push(l); }
    }
}
