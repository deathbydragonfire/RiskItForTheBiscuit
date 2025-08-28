using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SackBiscuitsCounterUI : MonoBehaviour
{
    [Header("Layer to count")]
    public string stackLayerName = "Stack";

    [Header("Capsule Probe (world-Y aligned)")]
    public Transform anchorOverride;
    public Vector3 anchorOffset = Vector3.zero;
    [Tooltip("Upward extent from anchor.")]
    public float heightAbove = 2.0f;
    [Tooltip("Downward extent from anchor (\"slightly below\").")]
    public float heightBelow = 0.2f;
    [Tooltip("Capsule radius.")]
    public float radius = 0.5f;
    [Tooltip("Include Trigger colliders when counting.")]
    public bool includeTriggers = true;

    public enum DedupeMode { Rigidbody, TopmostOnLayer, GameObject }
    [Header("De-duplication")]
    [Tooltip("Rigidbody: one per RB. TopmostOnLayer (default): one per highest ancestor on the Stack layer. GameObject: one per collider GO.")]
    public DedupeMode dedupe = DedupeMode.TopmostOnLayer;

    [Header("UI (auto-created if left empty)")]
    public Canvas targetCanvas;
    public Text biscuitsText;

    [Header("UI Style")]
    public string labelPrefix = "Biscuits: ";
    public Font font;
    public int fontSize = 28;
    public Color textColor = Color.white;
    public Vector2 margin = new Vector2(12f, 12f);
    public bool forceOverlayCanvas = true;
    public bool useSafeArea = true;

    [Header("Runtime")]
    [Range(0.02f, 1f)] public float refreshInterval = 0.25f;

    [Header("Events / Exposed Value")]
    [Tooltip("Current number of biscuits overlapping the capsule.")]
    public int BiscuitsCount { get; private set; }
    [Tooltip("Invoked whenever BiscuitsCount changes.")]
    public UnityEvent<int> OnBiscuitsChanged;

    [Header("Gizmos")]
    public bool drawCapsuleGizmo = true;
    public Color gizmoColor = new Color(0.2f, 0.9f, 0.6f, 0.25f);
    public Color gizmoWire = new Color(0.2f, 0.9f, 0.6f, 1f);

    private int _stackLayer = -1;
    private RectTransform _safeAreaRoot;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;
    private float _nextRefresh;

    private const int MaxHits = 256;
    private readonly Collider[] _hits = new Collider[MaxHits];

    void Awake()
    {
        _stackLayer = LayerMask.NameToLayer(stackLayerName);
        EnsureCanvas();
        EnsureUI();
        ApplyStyle();
        SnapToTopLeft();
        ApplyNewCount(ComputeCount()); // initialize
    }

    void Update()
    {
        if (useSafeArea) ApplySafeArea(false);
        SnapToTopLeft();

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + refreshInterval;
            ApplyNewCount(ComputeCount());
        }
    }

    // ------- Count logic (NO Y filtering) -------
    int ComputeCount()
    {
        Vector3 anchor = GetAnchorWorld();
        Vector3 p0 = anchor + Vector3.down * heightBelow;
        Vector3 p1 = anchor + Vector3.up * heightAbove;

        int mask = (_stackLayer >= 0) ? (1 << _stackLayer) : ~0;
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        int n = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, _hits, mask, qti);
        if (n >= MaxHits) Debug.LogWarning("[SackBiscuitsCounterUI] Hit buffer full; consider increasing MaxHits.");

        var unique = new HashSet<Transform>();
        int count = 0;

        for (int i = 0; i < n; i++)
        {
            var c = _hits[i];
            if (!c) continue;

            // Skip the sack’s own colliders
            if (c.transform.IsChildOf(transform)) continue;

            // Safety: ensure Stack layer (mask should already filter)
            if (c.gameObject.layer != _stackLayer) continue;

            Transform id = GetIdentityTransform(c);
            if (unique.Add(id)) count++;
        }

        return count;
    }

    void ApplyNewCount(int newCount)
    {
        if (newCount != BiscuitsCount)
        {
            BiscuitsCount = newCount;
            OnBiscuitsChanged?.Invoke(BiscuitsCount);
        }

        if (biscuitsText != null)
        {
            string prefix = string.IsNullOrWhiteSpace(labelPrefix) ? "Biscuits: " : labelPrefix;
            biscuitsText.text = prefix + BiscuitsCount;
        }
    }

    public int RecountNow()
    {
        int c = ComputeCount();
        ApplyNewCount(c);
        return c;
    }

    Transform GetIdentityTransform(Collider c)
    {
        switch (dedupe)
        {
            case DedupeMode.Rigidbody:
                return c.attachedRigidbody ? c.attachedRigidbody.transform : c.transform;
            case DedupeMode.GameObject:
                return c.transform;
            case DedupeMode.TopmostOnLayer:
            default:
                Transform t = c.transform;
                Transform last = t;
                while (t != null)
                {
                    if (t.gameObject.layer == _stackLayer) last = t;
                    t = t.parent;
                }
                return last;
        }
    }

    Vector3 GetAnchorWorld()
    {
        if (anchorOverride)
            return anchorOverride.position + anchorOffset;

        float yTop = GetTopOfRenderersY(transform);
        return new Vector3(transform.position.x, yTop, transform.position.z) + anchorOffset;
    }

    static float GetTopOfRenderersY(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return root.position.y;
        float top = rends[0].bounds.max.y;
        for (int i = 1; i < rends.Length; i++)
            top = Mathf.Max(top, rends[i].bounds.max.y);
        return top;
    }

    // -------------------- UI helpers --------------------
    void EnsureCanvas()
    {
        if (targetCanvas == null)
        {
            targetCanvas = CreateOverlayCanvas("SackBiscuitsCanvas");
        }
        else if (forceOverlayCanvas && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            targetCanvas = CreateOverlayCanvas("SackBiscuitsCanvas (Overlay)");
        }

        if (useSafeArea)
        {
            var existing = targetCanvas.transform.Find("SafeAreaRoot") as RectTransform;
            _safeAreaRoot = existing ? existing :
                new GameObject("SafeAreaRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            if (_safeAreaRoot.transform.parent != targetCanvas.transform)
                _safeAreaRoot.SetParent(targetCanvas.transform, false);
            ApplySafeArea(true);
        }
    }

    Canvas CreateOverlayCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return c;
    }

    void EnsureUI()
    {
        if (biscuitsText == null)
        {
            Transform parent = useSafeArea ? (Transform)_safeAreaRoot : targetCanvas.transform;
            var textGO = new GameObject("BiscuitsText", typeof(RectTransform));
            textGO.transform.SetParent(parent, false);
            biscuitsText = textGO.AddComponent<Text>();
            biscuitsText.raycastTarget = false;
        }

        var rt = biscuitsText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
    }

    void ApplyStyle()
    {
        biscuitsText.font = font ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        biscuitsText.fontSize = Mathf.Max(1, fontSize);
        biscuitsText.color = textColor;
        biscuitsText.alignment = TextAnchor.UpperLeft;
    }

    void SnapToTopLeft()
    {
        var rt = biscuitsText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(margin.x, -margin.y);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    void ApplySafeArea(bool force)
    {
        if (!useSafeArea || _safeAreaRoot == null || targetCanvas == null) return;

        Rect safe = Screen.safeArea;
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        if (!force && safe == _lastSafeArea &&
            _lastScreen == new Vector2Int((int)screen.x, (int)screen.y)) return;

        _lastSafeArea = safe;
        _lastScreen = new Vector2Int((int)screen.x, (int)screen.y);

        var min = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
        var max = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        _safeAreaRoot.anchorMin = min;
        _safeAreaRoot.anchorMax = max;
        _safeAreaRoot.offsetMin = Vector2.zero;
        _safeAreaRoot.offsetMax = Vector2.zero;
        _safeAreaRoot.pivot = new Vector2(0f, 1f);
    }

    void OnValidate()
    {
        _stackLayer = LayerMask.NameToLayer(stackLayerName);
        if (biscuitsText != null) { ApplyStyle(); SnapToTopLeft(); }
    }

    // -------------------- Gizmos --------------------
    void OnDrawGizmosSelected()
    {
        if (!drawCapsuleGizmo) return;

        Vector3 a = GetAnchorWorld();
        Vector3 p0 = a + Vector3.down * heightBelow;
        Vector3 p1 = a + Vector3.up * heightAbove;

        Gizmos.color = gizmoColor;
        DrawSolidCapsule(p0, p1, radius);

        Gizmos.color = gizmoWire;
        DrawWireCapsule(p0, p1, radius);
    }

    static void DrawWireCapsule(Vector3 p0, Vector3 p1, float r)
    {
        Gizmos.DrawWireSphere(p0, r);
        Gizmos.DrawWireSphere(p1, r);
        Gizmos.DrawLine(p0 + Vector3.right * r, p1 + Vector3.right * r);
        Gizmos.DrawLine(p0 - Vector3.right * r, p1 - Vector3.right * r);
        Gizmos.DrawLine(p0 + Vector3.forward * r, p1 + Vector3.forward * r);
        Gizmos.DrawLine(p0 - Vector3.forward * r, p1 - Vector3.forward * r);
    }

    static void DrawSolidCapsule(Vector3 p0, Vector3 p1, float r)
    {
        Gizmos.DrawSphere(p0, r);
        Gizmos.DrawSphere(p1, r);
        Gizmos.DrawCube((p0 + p1) * 0.5f, new Vector3(r * 2f, (p1 - p0).magnitude, r * 2f));
    }
}
