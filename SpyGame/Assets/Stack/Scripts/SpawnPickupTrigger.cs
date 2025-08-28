using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class SpawnPickupTrigger : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Layer name that represents the Player.")]
    public string playerLayerName = "Player";

    [Header("Spawn Settings")]
    [Tooltip("How many items to spawn when triggered (all in the same frame).")]
    public int spawnCount = 1;

    private StackTopSpawner _spawner; // auto-found (only one per level)
    private int _playerLayer = -1;
    private bool _triggered = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        playerLayerName = "Player";
        spawnCount = 1;
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col && !col.isTrigger) col.isTrigger = true;

        if (spawnCount < 1) spawnCount = 1;

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

        // Active objects first
        _spawner = FindObjectOfType<StackTopSpawner>();
        if (_spawner != null)
        {
            if (verbose) Debug.Log($"[{nameof(SpawnPickupTrigger)}] Found StackTopSpawner: '{_spawner.name}'.");
            return true;
        }

        // Include inactive in active scene roots
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
        if (_triggered) return;
        if (!IsPlayer(other)) return;

        if (_spawner == null && !TryResolveSpawner(true)) return;

        _triggered = true;
        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        // Spawn all in the same frame
        for (int i = 0; i < spawnCount; i++)
            _spawner.SpawnNow();

        // Then destroy this trigger object
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
