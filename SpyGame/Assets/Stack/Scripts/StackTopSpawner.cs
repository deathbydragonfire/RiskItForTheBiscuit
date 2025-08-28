using UnityEngine;

[DisallowMultipleComponent]
public class StackTopSpawner : MonoBehaviour
{
    [Header("Prefab")]
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
        stackLayers = LayerMask.GetMask("Stack");
        spawnOffsetY = 0.20f;
        maxScanDistance = 100f;
    }

    [ContextMenu("Spawn Now")]
    public void SpawnNow()
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(StackTopSpawner)}] No prefab assigned on '{name}'.");
            return;
        }

        Vector3 origin = transform.position;

        // No XZ offset — probe straight up from the spawner's XZ
        float topY = GetTopYAlongUp(origin);
        Vector3 spawnPos = new Vector3(origin.x, topY + spawnOffsetY, origin.z);

        Instantiate(prefab, spawnPos, prefab.transform.rotation, parentForSpawned);
    }

    /// <summary>
    /// Casts straight up from 'origin' and returns the highest bounds.max.y of any hit
    /// collider on stackLayers. If none, returns origin.y.
    /// </summary>
    private float GetTopYAlongUp(Vector3 origin)
    {
        Ray ray = new Ray(origin, Vector3.up);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxScanDistance, stackLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return origin.y;

        float highestTop = origin.y;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (!col) continue;

            // Ignore ourselves/children
            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            float top = col.bounds.max.y;
            if (top > highestTop) highestTop = top;
        }

        return highestTop;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 o = transform.position;
        Gizmos.DrawLine(o, o + Vector3.up * maxScanDistance);
    }
}
