using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    // Hook up references that upgrades will modify
    private NavMeshAgent _mover;     // your movement script
    private Health _health;                       // your generic health

    void Awake() => _health = GetComponent<Health>();

    void OnEnable() => _health.Died += OnDied;
    void OnDisable() => _health.Died -= OnDied;

    void OnDied()
    {
        if (XpLevelSystem.Instance) XpLevelSystem.Instance.AwardEnemyKill();
        Destroy(gameObject);
        // other death logic (loot, VFX)...
    }
    public void SetPoints(float points)
    {
        _mover = GetComponent<NavMeshAgent>();
        _health = GetComponent<Health>();

        _mover.speed *= 1f + points * 0.01f;
        _health.AddMaxHp(_health.Max * (1f + points * 0.01f));
    }
}
