using UnityEngine;
using UnityEngine.Pool;

public abstract class PooledBehaviour : MonoBehaviour
{
    IObjectPool<PooledBehaviour> _pool;
    internal void SetPool(IObjectPool<PooledBehaviour> pool) => _pool = pool;

    public virtual void OnSpawn() { }
    public virtual void OnDespawn() { }

    public void Despawn()
    {
        if (!gameObject.activeInHierarchy) return; // guard double-release
        _pool?.Release(this);
    }
}
