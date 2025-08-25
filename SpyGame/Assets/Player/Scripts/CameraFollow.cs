using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0, 10, -10);
    public float smoothSpeed = 5f;   // Higher = snappier follow

    [Header("Shake (runtime)")]
    [Tooltip("Noise samples per second for shake motion.")]
    public float shakeFrequency = 25f;

    // Internal shake state (no coroutines)
    float shakeTimeRemaining = 0f;
    float shakeTotalDuration = 0f;
    float shakeMagnitude = 0f;
    Vector2 noiseSeed;

    void Awake()
    {
        // Randomize per-instance noise seed
        noiseSeed = new Vector2(Random.value * 1000f, Random.value * 1000f);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Smooth follow (position only; keep editor-set rotation)
        Vector3 desired = target.position + offset;
        Vector3 followPos = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);

        // Compute shake offset (XY only so orthographic Z stays stable)
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimeRemaining > 0f)
        {
            // Progress + damping (ease-out)
            float t = 1f - (shakeTimeRemaining / Mathf.Max(0.0001f, shakeTotalDuration));
            float damping = 1f - t;                 // linear ease-out
            damping = damping * damping;            // quadratic for smoother fade

            float time = Time.time * shakeFrequency;
            float nx = (Mathf.PerlinNoise(noiseSeed.x, time) * 2f) - 1f;
            float ny = (Mathf.PerlinNoise(noiseSeed.y, time) * 2f) - 1f;

            shakeOffset = new Vector3(nx, ny, 0f) * (shakeMagnitude * damping);

            shakeTimeRemaining -= Time.deltaTime;
            if (shakeTimeRemaining <= 0f)
            {
                shakeTimeRemaining = 0f;
                shakeTotalDuration = 0f;
                shakeMagnitude = 0f;
            }
        }

        // Apply final position (follow + shake)
        transform.position = followPos + shakeOffset;
        // Do NOT touch rotation (kept from editor)
    }

    /// <summary>
    /// Public, call from other scripts to shake the camera.
    /// If multiple calls overlap, we keep the stronger magnitude
    /// and extend duration if longer.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        // Extend/boost instead of starting a competing coroutine
        shakeTotalDuration = Mathf.Max(shakeTotalDuration, duration);
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
    }
}
