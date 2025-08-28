using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class GoalAreaScorer : MonoBehaviour
{
    [Header("Detection (uses THIS object's Trigger Collider)")]
    public string bagLayerName = "Bag";
    public float requiredOverlapTime = 1f;

    [Header("Stars Awarding")]
    [Tooltip("Min score needed for 1, 2, 3, ... stars (ascending). Example: [1,3,6]")]
    public int[] starThresholds = new int[] { 1, 3, 6 };
    public GameObject starPrefab;

    [Header("Spawn Pattern")]
    [Tooltip("Derive ring center/size from this trigger's bounds.")]
    public bool autoFromTriggerBounds = true;
    [Tooltip("Extra ring radius added beyond bounds.")]
    public float radiusPadding = 0.25f;
    [Tooltip("Lift the ring above the trigger along the camera's Up.")]
    public float heightPadding = 0.20f;

    [Tooltip("Used only if autoFromTriggerBounds = OFF.")]
    public float spawnRadius = 1.0f;
    [Tooltip("Used only if autoFromTriggerBounds = OFF (applied along camera up).")]
    public float spawnHeightOffset = 1.0f;

    [Tooltip("Starting angle (deg) around the camera-facing circle (0 = camera right).")]
    public float startAngleDeg = 90f;
    public bool useFullCircle = false;
    public float arcDegrees = 120f;

    [Header("Camera-Facing Ring")]
    [Tooltip("Camera used to orient the circle and billboard stars. If empty, uses Camera.main.")]
    public Camera targetCamera;
    [Tooltip("If ON, stars will look at the camera.")]
    public bool starsFaceCamera = true;

    [Header("Debug")]
    public bool debugLog = true;
    public string debugPrefix = "[GoalAreaScorer]";

    // --- runtime ---
    private int _bagLayer;
    private bool _scored;
    private int _bagInsideCount;
    private Coroutine _waitCo;
    private Collider _trigger;

    private void Awake()
    {
        _bagLayer = LayerMask.NameToLayer(bagLayerName);

        _trigger = GetComponent<Collider>();
        if (_trigger == null) { Debug.LogError($"{debugPrefix} No collider found."); enabled = false; return; }
        if (!_trigger.isTrigger) Debug.LogWarning($"{debugPrefix} Collider should be set to 'Is Trigger'.");

        if (starThresholds != null && starThresholds.Length > 1)
            for (int i = 1; i < starThresholds.Length; i++)
                if (starThresholds[i] < starThresholds[i - 1])
                    starThresholds[i] = starThresholds[i - 1];
    }

    private void OnDisable()
    {
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_scored) return;
        if (other.gameObject.layer != _bagLayer) return;

        _bagInsideCount++;
        if (_bagInsideCount == 1)
        {
            if (_waitCo != null) StopCoroutine(_waitCo);
            _waitCo = StartCoroutine(WaitAndScore(other));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_scored) return;
        if (other.gameObject.layer != _bagLayer) return;

        _bagInsideCount = Mathf.Max(0, _bagInsideCount - 1);
        if (_bagInsideCount == 0 && _waitCo != null)
        {
            StopCoroutine(_waitCo);
            _waitCo = null;
        }
    }

    private IEnumerator WaitAndScore(Collider bagCol)
    {
        float t = 0f;
        while (t < requiredOverlapTime)
        {
            if (_bagInsideCount == 0) yield break;
            t += Time.deltaTime;
            yield return null;
        }
        DoScore(bagCol);
    }

    private void DoScore(Collider bagCol)
    {
        if (_scored) return;

        var counter = bagCol.GetComponentInParent<SackBiscuitsCounterUI>();
        if (counter == null) counter = bagCol.GetComponentInChildren<SackBiscuitsCounterUI>();

        int score = counter ? counter.BiscuitsCount : 0;
        if (!counter) Debug.LogWarning($"{debugPrefix} No SackBiscuitsCounterUI found on bag; score = 0.");

        int stars = CalculateStars(score);
        SpawnStarsCameraCircle(stars);

        if (debugLog)
        {
            string bagName = bagCol.transform.root ? bagCol.transform.root.name : bagCol.name;
            Debug.Log($"{debugPrefix} GOAL TRIGGERED on '{name}' by bag '{bagName}'. Score={score}, Stars={stars}. Disabling scorer.");
        }

        _scored = true;
        enabled = false; // one-shot
    }

    private int CalculateStars(int score)
    {
        if (starThresholds == null || starThresholds.Length == 0) return 0;
        int stars = 0;
        for (int i = 0; i < starThresholds.Length; i++)
            if (score >= starThresholds[i]) stars = i + 1;
        return stars;
    }

    // ---------------- Camera-facing ring spawn ----------------
    private void SpawnStarsCameraCircle(int count)
    {
        if (count <= 0 || starPrefab == null) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning($"{debugPrefix} No camera found. Falling back to world-XZ circle.");
            SpawnStarsWorldXZ(count);
            return;
        }

        // Circle basis in the camera plane
        Vector3 right = cam.transform.right; // 0° direction
        Vector3 up = cam.transform.up;    // 90° direction

        // Center & radius
        Vector3 center;
        float radius;

        if (autoFromTriggerBounds && _trigger != null)
        {
            Bounds b = _trigger.bounds;

            // Project AABB extents onto camera right/up to estimate a good radius in the camera plane
            Vector3 absRight = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            Vector3 absUp = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            float rRight = Vector3.Dot(absRight, b.extents);
            float rUp = Vector3.Dot(absUp, b.extents);
            radius = Mathf.Max(rRight, rUp) + radiusPadding;

            // Lift above top along camera up
            float lift = Vector3.Dot(absUp, b.extents) + heightPadding;
            center = b.center + up * lift;
        }
        else
        {
            radius = spawnRadius;
            center = transform.position + up * spawnHeightOffset;
        }

        // Spawn unparented; optionally billboard toward camera
        if (useFullCircle)
        {
            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float ang = startAngleDeg + step * i;
                Vector3 pos = center + (right * Mathf.Cos(ang * Mathf.Deg2Rad) + up * Mathf.Sin(ang * Mathf.Deg2Rad)) * radius;
                Quaternion rot = starsFaceCamera ? Quaternion.LookRotation((cam.transform.position - pos).normalized, up)
                                                 : Quaternion.identity;
                Instantiate(starPrefab, pos, rot);
            }
        }
        else
        {
            float step = (count == 1) ? 0f : (arcDegrees / (count - 1));
            float start = startAngleDeg - arcDegrees * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float ang = start + step * i;
                Vector3 pos = center + (right * Mathf.Cos(ang * Mathf.Deg2Rad) + up * Mathf.Sin(ang * Mathf.Deg2Rad)) * radius;
                Quaternion rot = starsFaceCamera ? Quaternion.LookRotation((cam.transform.position - pos).normalized, up)
                                                 : Quaternion.identity;
                Instantiate(starPrefab, pos, rot);
            }
        }
    }

    // Fallback: old world-XZ ring
    private void SpawnStarsWorldXZ(int count)
    {
        Bounds b = _trigger.bounds;
        float radius = Mathf.Max(b.extents.x, b.extents.z) + radiusPadding;
        float y = b.max.y + heightPadding;
        Vector3 center = new Vector3(b.center.x, y, b.center.z);

        float step = useFullCircle ? 360f / count : (count == 1 ? 0f : arcDegrees / (count - 1));
        float start = useFullCircle ? startAngleDeg : startAngleDeg - arcDegrees * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float ang = start + step * i;
            float rad = ang * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
            Instantiate(starPrefab, pos, Quaternion.identity);
        }
    }
}
