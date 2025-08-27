using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class SpawnPickupTrigger : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Layer name that represents the Player.")]
    public string playerLayerName = "Player";

    [Header("Trigger Behavior")]
    [Tooltip("How many items to spawn when triggered.")]
    public int spawnCount = 1;

    [Tooltip("Delay between items in a burst (seconds). 0 = all at once.")]
    public float burstInterval = 0.0f;

    // Auto-found spawner (only one per level)
    private StackTopSpawner _spawner;
    private int _playerLayer = -1;
    private bool _triggered = false;

    private void Reset()
    {
        // Ensure trigger collider
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        playerLayerName = "Player";
        spawnCount = 1;
        burstInterval = 0f;
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col && !col.isTrigger) col.isTrigger = true;

        if (spawnCount < 1) spawnCount = 1;
        if (burstInterval < 0f) burstInterval = 0f;

        _playerLayer = LayerMask.NameToLayer(playerLayerName);
    }

    private void Awake()
    {
        _playerLayer = LayerMask.NameToLayer(playerLayerName);
        TryResolveSpawner();
    }

    private void Start()
    {
        if (_spawner == null) TryResolveSpawner();
    }

    private bool TryResolveSpawner(bool verbose = false)
    {
        if (_spawner != null) return true;

        _spawner = FindObjectOfType<StackTopSpawner>();
        if (_spawner != null)
        {
            if (verbose) Debug.Log($"[{nameof(SpawnPickupTrigger)}] Found StackTopSpawner: '{_spawner.name}'.");
            return true;
        }

        // Fallback: include inactive roots
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var s = root.GetComponentInChildren<StackTopSpawner>(true);
            if (s != null)
            {
                _spawner = s;
                if (verbose) Debug.Log($"[{nameof(SpawnPickupTrigger)}] Found inactive StackTopSpawner: '{_spawner.name}'.");
                return true;
            }
        }

        if (verbose) Debug.LogWarning($"[{nameof(SpawnPickupTrigger)}] No StackTopSpawner found in scene '{scene.name}'.");
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return; // already firing/dying
        if (!IsPlayer(other)) return;

        if (_spawner == null && !TryResolveSpawner()) return;

        _triggered = true;

        // Prevent re-entry while we spawn
        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        if (spawnCount == 1 || burstInterval <= 0f)
        {
            // Instant spawn(s), then die immediately
            for (int i = 0; i < spawnCount; i++) _spawner.SpawnNow();
            Destroy(gameObject);
        }
        else
        {
            // Burst over time, then die
            StartCoroutine(SpawnBurstAndDie(spawnCount, burstInterval));
        }
    }

    private IEnumerator SpawnBurstAndDie(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            if (_spawner) _spawner.SpawnNow();
            if (i < count - 1 && interval > 0f)
                yield return new WaitForSeconds(interval);
        }
        Destroy(gameObject);
    }

    private bool IsPlayer(Collider other)
    {
        if (_playerLayer < 0) return false;
        if (other.gameObject.layer == _playerLayer) return true;

        var root = other.transform.root;
        return root != null && root.gameObject.layer == _playerLayer;
    }
}
