using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Spawn Pattern (camera-facing circle)")]
    public bool autoFromTriggerBounds = true;
    public float radiusPadding = 0.25f;
    public float heightPadding = 0.20f;
    public float spawnRadius = 1.0f;            // used if autoFromTriggerBounds = false
    public float spawnHeightOffset = 1.0f;      // used if autoFromTriggerBounds = false (along camera up)
    public float startAngleDeg = 90f;
    public bool useFullCircle = false;
    public float arcDegrees = 120f;

    [Header("Camera-Facing Ring")]
    public Camera targetCamera;
    public bool starsFaceCamera = true;

    [Header("Level Progression")]
    [Tooltip("Advance to the next scene after stars spawn, if possible.")]
    public bool autoAdvanceNextLevel = true;
    [Tooltip("Delay (seconds, real time) before advancing.")]
    public float nextLevelDelay = 3f;
    [Tooltip("Optional explicit scene name to load if there is no 'next' build index.")]
    public string fallbackSceneName = "";

    [Header("Debug")]
    public bool debugLog = true;
    public string debugPrefix = "[GoalAreaScorer]";

    // --- runtime ---
    private int _bagLayer;
    private bool _scored;
    private int _bagInsideCount;
    private Coroutine _waitCo;
    private Collider _trigger;

    void Awake()
    {
        _bagLayer = LayerMask.NameToLayer(bagLayerName);
        _trigger = GetComponent<Collider>();
        if (!_trigger) { Debug.LogError($"{debugPrefix} No collider found."); enabled = false; return; }
        if (!_trigger.isTrigger) Debug.LogWarning($"{debugPrefix} Collider should be set to 'Is Trigger'.");

        // normalize thresholds
        if (starThresholds != null && starThresholds.Length > 1)
            for (int i = 1; i < starThresholds.Length; i++)
                if (starThresholds[i] < starThresholds[i - 1])
                    starThresholds[i] = starThresholds[i - 1];
    }

    void OnDisable()
    {
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
    }

    void OnTriggerEnter(Collider other)
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

    void OnTriggerExit(Collider other)
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

    IEnumerator WaitAndScore(Collider bagCol)
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

    void DoScore(Collider bagCol)
    {
        if (_scored) return;

        var counter = bagCol.GetComponentInParent<SackBiscuitsCounterUI>()
                   ?? bagCol.GetComponentInChildren<SackBiscuitsCounterUI>();

        int score = counter ? counter.BiscuitsCount : 0;
        if (!counter) Debug.LogWarning($"{debugPrefix} No SackBiscuitsCounterUI found on bag; score = 0.");

        int stars = CalculateStars(score);
        SpawnStarsCameraCircle(stars);

        if (debugLog)
        {
            string bagName = bagCol.transform.root ? bagCol.transform.root.name : bagCol.name;
            Debug.Log($"{debugPrefix} GOAL TRIGGERED on '{name}' by bag '{bagName}'. Score={score}, Stars={stars}.");
        }

        _scored = true;                 // prevent re-scoring
        if (_trigger) _trigger.enabled = false; // stop further overlaps while we wait

        if (autoAdvanceNextLevel)
            StartCoroutine(AdvanceAfterDelayRealtime(nextLevelDelay));
    }

    int CalculateStars(int score)
    {
        if (starThresholds == null || starThresholds.Length == 0) return 0;
        int stars = 0;
        for (int i = 0; i < starThresholds.Length; i++)
            if (score >= starThresholds[i]) stars = i + 1;
        return stars;
    }

    // -------- camera-facing ring spawn --------
    void SpawnStarsCameraCircle(int count)
    {
        if (count <= 0 || starPrefab == null) return;

        Camera cam = targetCamera ? targetCamera : Camera.main;
        if (!cam)
        {
            if (debugLog) Debug.LogWarning($"{debugPrefix} No camera found; cannot place camera-facing ring.");
            return;
        }

        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;

        Vector3 center;
        float radius;

        if (autoFromTriggerBounds && _trigger)
        {
            Bounds b = _trigger.bounds;
            Vector3 absRight = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            Vector3 absUp = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            float rRight = Vector3.Dot(absRight, b.extents);
            float rUp = Vector3.Dot(absUp, b.extents);
            radius = Mathf.Max(rRight, rUp) + radiusPadding;
            float lift = Vector3.Dot(absUp, b.extents) + heightPadding;
            center = b.center + up * lift;
        }
        else
        {
            radius = spawnRadius;
            center = transform.position + up * spawnHeightOffset;
        }

        float step = useFullCircle ? 360f / count : (count == 1 ? 0f : arcDegrees / (count - 1));
        float start = useFullCircle ? startAngleDeg : startAngleDeg - arcDegrees * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float ang = start + step * i;
            float rad = ang * Mathf.Deg2Rad;
            Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * radius;
            Quaternion rot = starsFaceCamera ? Quaternion.LookRotation((cam.transform.position - pos).normalized, up)
                                             : Quaternion.identity;
            Instantiate(starPrefab, pos, rot); // unparented
        }
    }

    // -------- level advance --------
    IEnumerator AdvanceAfterDelayRealtime(float delaySec)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySec));
        TryAdvanceScene();
    }

    void TryAdvanceScene()
    {
        var current = SceneManager.GetActiveScene();
        int thisIdx = current.buildIndex;
        int total = SceneManager.sceneCountInBuildSettings;

        // Next by build index
        if (thisIdx >= 0 && thisIdx + 1 < total)
        {
            if (debugLog) Debug.Log($"{debugPrefix} Loading next scene by index: {thisIdx + 1}/{total - 1}");
            SceneManager.LoadSceneAsync(thisIdx + 1, LoadSceneMode.Single);
            return;
        }

        // Optional fallback by name
        if (!string.IsNullOrWhiteSpace(fallbackSceneName) &&
            Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            if (debugLog) Debug.Log($"{debugPrefix} Loading fallback scene by name: {fallbackSceneName}");
            SceneManager.LoadSceneAsync(fallbackSceneName, LoadSceneMode.Single);
            return;
        }

        if (debugLog)
            Debug.Log($"{debugPrefix} No next scene found in Build Settings and no valid fallback; staying on '{current.name}'.");
    }
}
