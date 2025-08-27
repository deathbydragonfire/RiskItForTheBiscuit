using UnityEngine;

[DisallowMultipleComponent]
public class StackTopSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("What to spawn on top of the Stack.")]
    public GameObject prefab;

    [Header("Placement")]
    [Tooltip("How far above the detected top surface to spawn.")]
    public float spawnOffsetY = 0.20f;

    [Tooltip("How far upward to scan for Stack objects.")]
    public float maxScanDistance = 100f;

    [Tooltip("Layers considered part of the stack (defaults to the 'Stack' layer).")]
    public LayerMask stackLayers;

    [Header("Optional")]
    [Tooltip("Optional parent to keep the hierarchy tidy.")]
    public Transform parentForSpawned;

    private void Reset()
    {
        // By default, look only at the 'Stack' layer
        stackLayers = LayerMask.GetMask("Stack");
    }

    /// <summary>
    /// Call this from UI, events, or via the Context Menu to spawn now.
    /// </summary>
    [ContextMenu("Spawn Now")]
    public void SpawnNow()
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(StackTopSpawner)}] No prefab assigned on '{name}'.");
            return;
        }

        Vector3 origin = transform.position;

        // Find the highest top surface of any 'Stack' collider intersected by the upward ray.
        float topY = GetTopYAlongUp(origin);
        float targetY = topY + spawnOffsetY;

        Vector3 spawnPos = new Vector3(origin.x, targetY, origin.z);

        // Spawn with prefab's default rotation; change if you prefer aligning to spawner.
        GameObject spawned = Instantiate(prefab, spawnPos, prefab.transform.rotation, parentForSpawned);
        // Example alternative: align Y-rotation with this spawner
        // spawned.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    /// <summary>
    /// Casts straight up from 'origin' and returns the highest bounds.max.y of any hit
    /// collider on stackLayers. If none, returns origin.y.
    /// </summary>
    private float GetTopYAlongUp(Vector3 origin)
    {
        Ray ray = new Ray(origin, Vector3.up);

        // Collect all entries into colliders along the vertical line
        RaycastHit[] hits = Physics.RaycastAll(ray, maxScanDistance, stackLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            // Nothing above—treat current Y as the open space baseline.
            return origin.y;
        }

        float highestTop = origin.y;

        for (int i = 0; i < hits.Length; i++)
        {
            // Ignore if the hit collider is this object or a child of it
            if (hits[i].collider && (hits[i].collider.transform == transform || hits[i].collider.transform.IsChildOf(transform)))
                continue;

            float colliderTopY = hits[i].collider.bounds.max.y;
            if (colliderTopY > highestTop)
                highestTop = colliderTopY;
        }

        return highestTop;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the upward scan
        Gizmos.color = Color.cyan;
        Vector3 o = transform.position;
        Gizmos.DrawLine(o, o + Vector3.up * maxScanDistance);
    }
}
