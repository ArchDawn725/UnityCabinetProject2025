using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ArchDawnTest : MonoBehaviour, IAsyncStep
{
    [SerializeField] private PoolManager poolManager;
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private EnemySO enemySO;
    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        // Do your async work here: loading, addressables, auth, etc.

        await Awaitable.WaitForSecondsAsync(2, ct);//test
    }

    [ContextMenu("Force Spawn")]
    private void SpawnEnemy()
    {
        Vector3 vector3 = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
        var enemy = poolManager.Spawn(enemyPrefab, vector3, Quaternion.identity);
        enemy.ApplyDefinition(enemySO, 0);
    }
}
