using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class EnemySpawner : MonoBehaviour, IAsyncStep
{
    #region Settings
    public static EnemySpawner Singleton { get; private set; }

    [Header("Pool + Enemy")]
    [SerializeField] private PoolManager pool; 
    [SerializeField] private Enemy enemyPrefab; 

    [Header("Pacing")]
    [Tooltip("Seconds between the first few spawns.")]
    [SerializeField, Min(0f)] private float initialInterval = 2f;
    [Tooltip("Seconds between the last few spawns (faster = smaller).")]
    [SerializeField, Min(0f)] private float finalInterval = 0.5f;
    [Tooltip("Randomize the overall order of spawn entries.")]
    [SerializeField] private bool randomizeOrder = false;

    // Events
    public event Action<Enemy> OnEnemySpawned;
    public event Action OnWaveStarted;
    public event Action OnWaveCleared;

    // Runtime state
    private Coroutine _spawnRoutine;
    private int _alive;                         
    private List<Transform> _activePoints = new();

#endregion
    #region Externals

    // ---------------- IAsyncStep ----------------
    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        if (Singleton && Singleton != this) { Destroy(gameObject); return; }
        Singleton = this;

        await Task.CompletedTask;
    }

    // ---------------- Room-driven API ----------------

    /// <summary>
    /// Start spawning for a room. Uses roomRoot to discover spawn points; falls back to serialized points.
    /// </summary>
    public void StartSpawn(RoomSO room, int difficulty, Transform roomRoot)
    {
        Stop(); // stop any previous wave
        if (room == null)
        {
            Debug.LogWarning($"{nameof(EnemySpawner)}: StartSpawn called with null RoomSO.");
            return;
        }
        if (!enemyPrefab || !pool)
        {
            Debug.LogError($"{nameof(EnemySpawner)}: PoolManager or Enemy prefab not assigned.");
            return;
        }

        var plan = BuildPlan(room);

        _activePoints = FindRoomSpawnPoints(roomRoot);

        if (randomizeOrder && plan.Count > 1)
            FisherYates(plan);
        _alive = 0;
        _spawnRoutine = StartCoroutine(SpawnRoutine(plan, difficulty));
    }

    public void Stop()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }
#endregion
    // ---------------- Internals ----------------
    #region Internals

    private List<EnemySO> BuildPlan(RoomSO room)
    {
        var list = new List<EnemySO>();
        if (room.enemies != null)
        {
            foreach (var e in room.enemies)
            {
                var count = Mathf.Max(0, e.count);
                for (int i = 0; i < count; i++) list.Add(e.enemy);
            }
        }
        return list;
    }

    private IEnumerator SpawnRoutine(List<EnemySO> plan, int difficulty)
    {
        if (plan.Count == 0)
        {
            // No enemies in this room, clear immediately
            OnWaveStarted?.Invoke();
            OnWaveCleared?.Invoke();
            yield break;
        }

        OnWaveStarted?.Invoke();

        int total = plan.Count;
        int spawned = 0;
        XpLevelSystem.Instance.SetTotal(total);

        yield return new WaitForSeconds(5); //gives time for second player to join

        while (spawned < total)
        {
            var def = plan[spawned];
            var point = PickSpawnPoint();

            // Spawn via pool + configure
            var enemy = pool.Spawn(enemyPrefab, point.position, point.rotation);
            WireHealthDeath(enemy);             // track alive/clear
            enemy.ApplyDefinition(def, difficulty * GetAlivePlayerCount());
            enemy.SendMessage("SetDifficulty", difficulty, SendMessageOptions.DontRequireReceiver);

            _alive++;
            OnEnemySpawned?.Invoke(enemy);

            spawned++;

            // Pace: Lerp interval from initial -> final across the wave
            float t = (total > 1) ? (spawned / (float)total) : 1f;
            float delay = Mathf.Lerp(initialInterval, finalInterval, t);
            delay /= Mathf.Max(1, GetAlivePlayerCount());
            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null;
        }

        // All spawned—now wait until all are dead
        while (_alive > 0) yield return null;

        _spawnRoutine = null;
        OnWaveCleared?.Invoke();
    }

    private Transform PickSpawnPoint()
    {
        if (_activePoints == null || _activePoints.Count == 0)
            return transform; // absolute fallback

        int i = UnityEngine.Random.Range(0, _activePoints.Count);
        return _activePoints[i];
    }

    private void WireHealthDeath(Enemy enemy)
    {
        if (!enemy) return;
        // Expect a Health component with an OnDied event
        var hp = enemy.GetComponent<Health>() ?? enemy.GetComponentInChildren<Health>();
        if (!hp)
        {
            // If no Health, treat as instantly dead (unlikely)
            _alive = Mathf.Max(0, _alive - 1);
            return;
        }

        void OnDiedHandler()
        {
            hp.Died -= OnDiedHandler;
            _alive = Mathf.Max(0, _alive - 1);
        }
        hp.Died += OnDiedHandler;
    }

    private static void FisherYates<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private List<Transform> FindRoomSpawnPoints(Transform roomRoot)
    {
        var points = new List<Transform>();
        if (!roomRoot) return points;

        // 1) Any children tagged "EnemySpawn"
        var tagged = roomRoot.GetComponentsInChildren<Transform>(true)
                             .Where(t => t && t.CompareTag("EnemySpawn"));
        points.AddRange(tagged);

        // 2) Any children named "EnemySpawn" (or under a parent named "EnemySpawns")
        foreach (var t in roomRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!t) continue;
            if (t.name == "EnemySpawn" || t.parent && t.parent.name == "EnemySpawns")
                points.Add(t);
        }

        // Deduplicate nulls and repeats
        points = points.Where(p => p != null).Distinct().ToList();
        return points;
    }

    private int GetAlivePlayerCount()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        int alive = players?.Length ?? 0;
        return Mathf.Max(1, alive);
    }
    #endregion
}
