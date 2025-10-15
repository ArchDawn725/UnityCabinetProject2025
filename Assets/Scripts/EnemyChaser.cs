using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField, Min(0f)] float detectionRadius = 50f;
    [SerializeField, Min(0f)] float retargetInterval = 0.25f;

    [Header("Attack")]
    [SerializeField, Min(0.5f)] float attackRange = 1.5f;
    [SerializeField, Min(0.05f)] float attackCooldown = 1.0f;
    [SerializeField, Min(0f)] float damage = 10f;

    [Header("Agent Tuning")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float angularSpeed = 720f;
    [SerializeField] float acceleration = 8f;

    NavMeshAgent _agent;

    struct Target
    {
        public Transform transform;
        public Health hp;
        public bool IsValid => transform && transform.gameObject.activeInHierarchy;
    }

    readonly List<Target> _targets = new();
    Target? _current;
    float _nextRetargetTime;
    float _nextAttackTime;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = angularSpeed;
        _agent.acceleration = acceleration;
        _agent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.85f);
        _agent.autoBraking = true;
    }

    void OnEnable()
    {
        // Seed with any already-present players
        foreach (var pi in PlayerRegistry.Players)
            TryAddTarget(pi.gameObject);

        // Listen for future players regardless of spawn order
        PlayerRegistry.Added += OnPlayerAdded;
        PlayerRegistry.Removed += OnPlayerRemoved;
    }

    void OnDisable()
    {
        PlayerRegistry.Added -= OnPlayerAdded;
        PlayerRegistry.Removed -= OnPlayerRemoved;
        _targets.Clear();
        _current = null;
    }

    void Update()
    {
        PruneTargets();

        if (Time.time >= _nextRetargetTime)
        {
            _current = GetClosestTargetInRange();
            _nextRetargetTime = Time.time + retargetInterval;
        }

        if (_current.HasValue && _current.Value.IsValid)
        {
            var t = _current.Value.transform;
            float dist = Vector3.Distance(transform.position, t.position);

            if (dist > attackRange)
            {
                if (_agent.enabled && _agent.isOnNavMesh)
                    _agent.SetDestination(t.position);
            }
            else
            {
                FaceTowards(t.position);

                if (Time.time >= _nextAttackTime)
                {
                    _current.Value.hp?.Hit(damage);
                    _nextAttackTime = Time.time + attackCooldown;
                }
            }
        }
    }

    // --- Registry event handlers ---
    void OnPlayerAdded(PlayerInput pi) => TryAddTarget(pi.gameObject);
    void OnPlayerRemoved(PlayerInput pi) => TryRemoveTarget(pi.gameObject);

    // --- Target management ---
    void TryAddTarget(GameObject go)
    {
        if (!go) return;
        var t = go.transform;
        if (_targets.Any(x => x.transform == t)) return;

        var hp = go.GetComponentInParent<Health>() ?? go.GetComponent<Health>();
        _targets.Add(new Target { transform = t, hp = hp });
    }

    void TryRemoveTarget(GameObject go)
    {
        if (!go) return;
        var t = go.transform;
        for (int i = _targets.Count - 1; i >= 0; i--)
            if (_targets[i].transform == t) _targets.RemoveAt(i);

        if (_current.HasValue && _current.Value.transform == t) _current = null;
    }

    void PruneTargets()
    {
        for (int i = _targets.Count - 1; i >= 0; i--)
            if (!_targets[i].IsValid) _targets.RemoveAt(i);

        if (_current.HasValue && !_current.Value.IsValid) _current = null;
    }

    Target? GetClosestTargetInRange()
    {
        if (_targets.Count == 0) return null;

        float bestSqr = float.PositiveInfinity;
        Target? best = null;
        Vector3 p = transform.position;
        float r2 = detectionRadius <= 0f ? float.PositiveInfinity : detectionRadius * detectionRadius;

        foreach (var t in _targets)
        {
            if (!t.IsValid) continue;
            float d2 = (t.transform.position - p).sqrMagnitude;
            if (d2 <= r2 && d2 < bestSqr) { bestSqr = d2; best = t; }
        }
        return best;
    }

    void FaceTowards(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-6f) return;
        var targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, angularSpeed * Time.deltaTime);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (detectionRadius > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
