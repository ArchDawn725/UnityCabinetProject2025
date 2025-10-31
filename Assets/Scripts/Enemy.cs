using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(Health))]
public class Enemy : PooledBehaviour
{
    private EnemyChaser _mover; 
    private Health _health;

    IObjectPool<Enemy> _pool;
    public void SetPool(IObjectPool<Enemy> pool) => _pool = pool;
    void Awake() => _health = GetComponent<Health>();

    void OnEnable() => _health.Died += OnDied;
    void OnDisable() => _health.Died -= OnDied;

    void OnDied()
    {
        if (XpLevelSystem.Instance) XpLevelSystem.Instance.AwardEnemyKill();
        Despawn();
    }
    public void ApplyDefinition(EnemySO so, int difficulty)
    {
        _mover = GetComponent<EnemyChaser>();
        _health = GetComponent<Health>();

        _health.SetMaxHp(so.maxHealth * (1f + difficulty * 0.25f));
        _mover.SetSpeed(so.moveSpeed * (1f + difficulty * 0.25f));
        GetComponent<SpriteRenderer>().color = so.color;

        // visuals, ai, etc.
    }
}
