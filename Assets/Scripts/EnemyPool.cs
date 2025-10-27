using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyPool : MonoBehaviour, IAsyncStep
{
    [SerializeField] private Enemy enemyPrefab;
    int prewarm = 64;
    int maxSize = 256;

    ObjectPool<Enemy> _pool;

    [ContextMenu("Force Setup")]//for testing in editor only
    private void ForceSetup() { var _ = SetupAsync(CancellationToken.None, null); }
    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        _pool = new ObjectPool<Enemy>(
            createFunc: () =>
            {
                var p = Instantiate(enemyPrefab, transform);
                p.gameObject.SetActive(false);
                p.SetPool(_pool);                  // give the projectile a way to return itself
                return p;
            },
            actionOnGet: p => 
            {
                p.transform.SetParent(null, true); // leave pool root when active
                p.gameObject.SetActive(true);
                //p.Activate();
            },
            actionOnRelease: p =>
            {
                //p.Deactivate();
                p.gameObject.SetActive(false);
                p.transform.SetParent(transform, false);
            },
            actionOnDestroy: p => Destroy(p.gameObject),
            collectionCheck: false,
            defaultCapacity: prewarm,
            maxSize: maxSize
        );

        // Prewarm (spread across frames, on main thread)
        var temp = new Enemy[prewarm];
        for (int i = 0; i < prewarm; i++)
        {
            temp[i] = _pool.Get();                 // OnGet runs (sync)
            // optional: do not run heavy logic in OnSpawn; it's for quick resets only
            await Awaitable.NextFrameAsync(ct);
        }
        for (int i = 0; i < prewarm; i++)
        {
            _pool.Release(temp[i]);                // OnRelease runs (sync)
            await Awaitable.NextFrameAsync(ct);
        }
    }

    public async Task<Enemy> SpawnAsync(EnemySO so, Vector3 pos, Quaternion rot, CancellationToken ct)
    {
        var p = _pool.Get();
        p.transform.SetPositionAndRotation(pos, rot);
        //await p.SetUp(so, ct);
        return p;
    }
}
