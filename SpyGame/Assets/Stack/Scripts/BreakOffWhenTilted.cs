using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class BreakOffWhenTilted : MonoBehaviour
{
    [Header("Break Condition")]
    [Range(5f, 85f)] public float breakAngleDeg = 28f;
    [Range(0f, 0.5f)] public float sustainSeconds = 0.08f;

    [Header("Physics Copy Settings")]
    public string fallenLayer = "FallenPiece";
    public float mass = 1.5f;
    public bool inheritParentVelocity = true;
    public Rigidbody parentVelocitySource;
    public float outwardImpulse = 0f;
    public CollisionDetectionMode collisionMode = CollisionDetectionMode.Continuous;
    public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    float _timer;
    bool _broken;
    Transform _parent;
    Vector3 _prevParentPos;
    Vector3 _parentVelSmoothed;

    void OnEnable()
    {
        _parent = transform.parent;
        if (_parent != null) _prevParentPos = _parent.position;
        _timer = 0f; _broken = false;
    }

    void Update()
    {
        if (_broken) return;
        if (_parent == null) { enabled = false; return; }

        if (inheritParentVelocity && parentVelocitySource == null)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Vector3 v = (_parent.position - _prevParentPos) / dt;
            _parentVelSmoothed = Vector3.Lerp(_parentVelSmoothed, v, 0.4f);
            _prevParentPos = _parent.position;
        }

        float tilt = Vector3.Angle(transform.up, _parent.up);
        _timer = (tilt >= breakAngleDeg) ? _timer + Time.deltaTime : 0f;

        if (_timer >= sustainSeconds)
            DoBreak();
    }

    void DoBreak()
    {
        if (_broken) return;
        _broken = true;

        // 1) Snapshot everything we need BEFORE touching/destroying the object
        var snap = new SpawnSnapshot
        {
            name = name + "_FallenCopy",
            layer = SafeLayer(fallenLayer),
            pos = transform.position,
            rot = transform.rotation,
            scale = transform.lossyScale,
            inheritVelocity = inheritParentVelocity
                ? (parentVelocitySource ? parentVelocitySource.linearVelocity : _parentVelSmoothed)
                : Vector3.zero,
            outwardImpulse = outwardImpulse,
            leanDir = SafeLeanDir(-transform.up),
            mass = Mathf.Max(0.01f, mass),
            collisionMode = collisionMode,
            interpolation = interpolation
        };

        // First MeshFilter/MeshRenderer pair
        var srcMF = GetComponentInChildren<MeshFilter>(true);
        var srcMR = srcMF ? srcMF.GetComponent<MeshRenderer>() : null;
        if (srcMF && srcMR)
        {
            snap.sharedMesh = srcMF.sharedMesh;
            snap.sharedMaterials = srcMR.sharedMaterials;
        }

        // First collider found: Box/Sphere/Capsule/Mesh
        var srcCol = GetComponentInChildren<Collider>(true);
        if (srcCol)
        {
            if (srcCol is BoxCollider b)
            {
                snap.colType = ColliderType.Box;
                snap.box_center = b.center; snap.box_size = b.size;
            }
            else if (srcCol is SphereCollider s)
            {
                snap.colType = ColliderType.Sphere;
                snap.sphere_center = s.center; snap.sphere_radius = s.radius;
            }
            else if (srcCol is CapsuleCollider c)
            {
                snap.colType = ColliderType.Capsule;
                snap.capsule_center = c.center; snap.capsule_radius = c.radius;
                snap.capsule_height = c.height; snap.capsule_direction = c.direction;
            }
            else if (srcCol is MeshCollider mc && mc.sharedMesh)
            {
                snap.colType = ColliderType.Mesh;
                snap.meshColliderMesh = mc.sharedMesh;
            }
        }

        // 2) Tell the wobble system to forget this node
        var rotator = GetComponentInParent<WobblyStackRotator>();
        if (rotator) rotator.RemoveNode(transform);

        // 3) Make this node inert immediately so nothing else can touch it this frame
        transform.SetParent(null, true);
        InertThisHierarchy();

        // 4) Schedule spawn next frame via runner that survives destruction, then destroy now
        BreakOffWhenTiltedRunner.Ensure().ScheduleSpawn(snap);
        Destroy(gameObject); // destroying this object will NOT cancel the runner's coroutine
    }

    void InertThisHierarchy()
    {
        foreach (var b in GetComponentsInChildren<Behaviour>(true))
        {
            if (b == this) continue;
            b.enabled = false;
        }
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    static int SafeLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return 0;
        int l = LayerMask.NameToLayer(layerName);
        return l >= 0 ? l : 0;
    }

    static Vector3 SafeLeanDir(Vector3 fromUp)
    {
        var d = Vector3.ProjectOnPlane(fromUp, Vector3.up);
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
    }

    // ===== Helper runner that survives after we destroy the piece =====
    private sealed class BreakOffWhenTiltedRunner : MonoBehaviour
    {
        static BreakOffWhenTiltedRunner _instance;
        public static BreakOffWhenTiltedRunner Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("_BreakOffRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BreakOffWhenTiltedRunner>();
            return _instance;
        }

        public void ScheduleSpawn(SpawnSnapshot s) => StartCoroutine(SpawnNextFrame(s));

        IEnumerator SpawnNextFrame(SpawnSnapshot s)
        {
            // Wait until the end of this frame (so original is fully gone),
            // then one more frame to be extra-safe around physics/CC depenetration.
            yield return new WaitForEndOfFrame();
            yield return null;
            SpawnCopy(s);
        }

        static void SpawnCopy(SpawnSnapshot s)
        {
            var go = new GameObject(s.name);
            go.layer = s.layer;
            go.transform.SetPositionAndRotation(s.pos, s.rot);
            go.transform.localScale = s.scale;

            if (s.sharedMesh && s.sharedMaterials != null && s.sharedMaterials.Length > 0)
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = s.sharedMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = s.sharedMaterials;
            }

            switch (s.colType)
            {
                case ColliderType.Box:
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = s.box_center; bc.size = s.box_size;
                    break;
                case ColliderType.Sphere:
                    var sc = go.AddComponent<SphereCollider>();
                    sc.center = s.sphere_center; sc.radius = s.sphere_radius;
                    break;
                case ColliderType.Capsule:
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.center = s.capsule_center; cc.radius = s.capsule_radius;
                    cc.height = s.capsule_height; cc.direction = s.capsule_direction;
                    break;
                case ColliderType.Mesh:
                    if (s.meshColliderMesh)
                    {
                        var mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = s.meshColliderMesh;
                        mc.convex = true;
                    }
                    break;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = s.mass;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = s.collisionMode;
            rb.interpolation = s.interpolation;

            if (s.inheritVelocity.sqrMagnitude > 0f)
                rb.linearVelocity = s.inheritVelocity;

            if (s.outwardImpulse > 0f && s.leanDir != Vector3.zero)
                rb.AddForce(s.leanDir * s.outwardImpulse, ForceMode.Impulse);
        }
    }

    // ===== Snapshot types =====
    enum ColliderType { None, Box, Sphere, Capsule, Mesh }

    struct SpawnSnapshot
    {
        public string name;
        public int layer;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;

        public Mesh sharedMesh;
        public Material[] sharedMaterials;

        public ColliderType colType;
        public Vector3 box_center, box_size;
        public Vector3 sphere_center; public float sphere_radius;
        public Vector3 capsule_center; public float capsule_radius, capsule_height; public int capsule_direction;
        public Mesh meshColliderMesh;

        public Vector3 inheritVelocity;
        public float outwardImpulse;
        public Vector3 leanDir;
        public float mass;
        public CollisionDetectionMode collisionMode;
        public RigidbodyInterpolation interpolation;
    }
}
