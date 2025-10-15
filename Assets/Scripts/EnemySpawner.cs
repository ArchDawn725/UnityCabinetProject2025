using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class EnemySpawner : MonoBehaviour, IAsyncStep
{
    [Header("Setup")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(1)] private int amountToSpawn = 100;
    [SerializeField] private Transform container;   // optional parent for tidy hierarchy

    [Header("Activation pacing")]
    [Tooltip("Seconds between first few spawns.")]
    [SerializeField, Min(0f)] private float initialInterval = 0.75f;
    [Tooltip("Seconds between the last few spawns (faster = smaller).")]
    [SerializeField, Min(0f)] private float finalInterval = 0.10f;
    [Tooltip("Pop enemies in a random order instead of FIFO.")]
    [SerializeField] private bool randomizeActivationOrder = false;

    [Header("Spawn distribution")]
    [Tooltip("Round-robin cycles through spawn points; otherwise use random spawn point per enemy.")]
    [SerializeField] private bool roundRobinPoints = true;

    // Public so other systems can inspect the preloaded wave
    public readonly List<GameObject> wave = new();

    // Events
    public System.Action<GameObject> OnEnemyActivated;
    public System.Action OnWaveCompleted;
    private Initializer _initializer;

    Coroutine _runRoutine;

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        await StartSpawn(ct);
        _initializer = initializer;
        _initializer.Play += Begin;
    }

    // --- Phase 1: Preload the wave (deactivated) ---
    public async Task StartSpawn(CancellationToken ct)
    {
        wave.Clear();

        if (enemyPrefabs.Length <= 0)
        {
            Debug.LogError($"{name}: Enemy prefab not assigned.");
            return;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{name}: No spawn points assigned.");
            return;
        }

        // Create enemies, deactivate, store in wave
        for (int i = 0; i < amountToSpawn; i++)
        {
            if (ct.IsCancellationRequested) return;

            Transform sp = roundRobinPoints
                ? spawnPoints[i % spawnPoints.Length]
                : spawnPoints[Random.Range(0, spawnPoints.Length)];

            var enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            var go = Instantiate(enemyPrefab, sp.position, sp.rotation, container);
            go.SetActive(false); // keep dormant until Begin()

            wave.Add(go);

            // Yield occasionally to keep frame responsive during big builds
            if ((i & 7) == 0) // every 8th spawn
                await Awaitable.NextFrameAsync(ct);
        }

        // one more frame for good measure
        await Awaitable.NextFrameAsync(ct);
    }

    // --- Phase 2: Release the wave at increasing speed ---
    public void Begin()
    {
        if (_runRoutine != null) StopCoroutine(_runRoutine);
        _runRoutine = StartCoroutine(ActivateWaveRoutine());
    }

    public void Stop()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }
    }

    IEnumerator ActivateWaveRoutine()
    {
        if (wave.Count == 0)
        {
            OnWaveCompleted?.Invoke();
            yield break;
        }

        // Optionally randomize activation order (Fisher–Yates)
        if (randomizeActivationOrder)
        {
            for (int i = wave.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (wave[i], wave[j]) = (wave[j], wave[i]);
            }
        }

        int total = wave.Count;
        int spawned = 0;

        yield return new WaitForSeconds(5);

        while (wave.Count > 0)
        {
            // Pop from end (O(1))
            int last = wave.Count - 1;
            var enemy = wave[last];
            wave.RemoveAt(last);

            if (enemy)  // activate
            {
                enemy.GetComponent<Enemy>().SetPoints(spawned);
                enemy.SetActive(true);
                OnEnemyActivated?.Invoke(enemy);
            }

            spawned++;

            // Progress 0..1 → interval lerp (gets faster over time)
            float t = (total > 1) ? (spawned / (float)total) : 1f;
            float delay = Mathf.Lerp(initialInterval, finalInterval, t);
            delay *= 2;

            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null; // next frame
        }

        _runRoutine = null;
        OnWaveCompleted?.Invoke();
    }

    // Utility if you ever want to scrap a built wave
    public void ClearAndDestroyWave()
    {
        foreach (var e in wave)
            if (e) Destroy(e);
        wave.Clear();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (finalInterval > initialInterval)
            finalInterval = initialInterval; // keep "increasing speed" (non-increasing delay)
    }
#endif
}
