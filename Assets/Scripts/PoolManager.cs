using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour, IAsyncStep
{
    [System.Serializable]
    public class Entry
    {
        public PooledBehaviour prefab;
        public int prewarm = 64;
        public int maxSize = 256;
    }

    [SerializeField] List<Entry> entries = new();
    readonly Dictionary<PooledBehaviour, ObjectPool<PooledBehaviour>> _pools = new();

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        foreach (var e in entries)
        {
            if (!e.prefab || _pools.ContainsKey(e.prefab)) continue;
            var pool = CreatePool(e.prefab, e.prewarm, e.maxSize);
            _pools.Add(e.prefab, pool);

            // Prewarm
            var temp = new List<PooledBehaviour>(e.prewarm);
            for (int i = 0; i < e.prewarm; i++) { temp.Add(pool.Get()); await Awaitable.NextFrameAsync(ct); }
            for (int i = 0; i < temp.Count; i++) { pool.Release(temp[i]); await Awaitable.NextFrameAsync(ct); }
            await Awaitable.NextFrameAsync(ct);
        }
    }

    ObjectPool<PooledBehaviour> CreatePool(PooledBehaviour prefab, int defaultCap, int maxSize)
    {
        ObjectPool<PooledBehaviour> pool = null;
        pool = new ObjectPool<PooledBehaviour>(
            createFunc: () =>
            {
                var inst = Instantiate(prefab, transform);
                inst.gameObject.SetActive(false);
                inst.SetPool(pool); // back-reference
                return inst;
            },
            actionOnGet: o =>
            {
                o.transform.SetParent(null, true);
                o.gameObject.SetActive(true);
                o.OnSpawn();
            },
            actionOnRelease: o =>
            {
                o.OnDespawn();
                o.gameObject.SetActive(false);
                o.transform.SetParent(transform, false);
            },
            actionOnDestroy: o => Destroy(o.gameObject),
            collectionCheck: false,
            defaultCapacity: defaultCap,
            maxSize: Mathf.Max(1, maxSize)
        );
        return pool;
    }

    public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : PooledBehaviour
    {
        if (!_pools.TryGetValue(prefab, out var pool))
            _pools[prefab] = pool = CreatePool(prefab, 0, 128);

        var obj = (T)pool.Get();
        obj.transform.SetPositionAndRotation(pos, rot);
        return obj;
    }
}

