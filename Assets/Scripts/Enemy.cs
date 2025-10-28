using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

[RequireComponent(typeof(Health))]
public class Enemy : PooledBehaviour
{
    // Hook up references that upgrades will modify
    private EnemyChaser _mover;     // your movement script
    private Health _health;                       // your generic health

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
    public override void OnSpawn()
    {
        //if (rb) { rb.velocity = Vector2.zero; rb.angularVelocity = 0f; }
        //if (hp) hp.ResetHP();
        //if (ai) ai.enabled = true;
    }
    public override void OnDespawn()
    {
        //if (ai) ai.enabled = false;
        // stop coroutines, clear status effects, ai, etc.
    }
    public void ApplyDefinition(EnemySO so, int difficulty)
    {
        _mover = GetComponent<EnemyChaser>();
        _health = GetComponent<Health>();

        _health.SetMaxHp(so.maxHealth * (1f + difficulty * 0.1f));
        _mover.SetSpeed(so.moveSpeed * (1f + difficulty * 0.1f));

        // visuals, ai, etc.
    }
}
