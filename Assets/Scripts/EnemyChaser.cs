using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Retargeting")]
    [SerializeField, Min(0f)] float retargetInterval = 0.25f;

    [Header("Attack")]
    [SerializeField, Min(0.5f)] float attackRange = 1.5f;
    [SerializeField, Min(0.05f)] float attackCooldown = 1.0f;
    [SerializeField, Min(0f)] float damage = 10f;

    [Header("Movement (2D)")]
    [SerializeField] float moveSpeed = 3.5f; 
    [SerializeField] float angularSpeed = 720f; 
    [SerializeField] float acceleration = 8f; 

    Rigidbody2D _rb;

    struct Target
    {
        public Transform transform;
        public Health hp;
        public bool IsValid => transform && transform.gameObject.activeInHierarchy;
        public bool IsReady => transform.gameObject.GetComponent<Player>()._initialized;
    }

    readonly List<Target> _targets = new();
    Target? _current;
    float _nextRetargetTime;
    float _nextAttackTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.bodyType = RigidbodyType2D.Dynamic;
    }

    void OnEnable()
    {
        foreach (var pi in PlayerRegistry.Players)
            TryAddTarget(pi.gameObject);

        PlayerRegistry.Added += OnPlayerAdded;
        PlayerRegistry.Removed += OnPlayerRemoved;
    }

    void OnDisable()
    {
        PlayerRegistry.Added -= OnPlayerAdded;
        PlayerRegistry.Removed -= OnPlayerRemoved;
        _targets.Clear();
        _current = null;
        if (_rb) _rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        // remove any dead/disabled targets
        for (int i = _targets.Count - 1; i >= 0; i--)
            if (!_targets[i].IsValid) _targets.RemoveAt(i);
        if (_current.HasValue && !_current.Value.IsValid) _current = null;

        // pick closest (no radius limit)
        if (Time.time >= _nextRetargetTime)
        {
            _current = GetClosestTarget();
            _nextRetargetTime = Time.time + retargetInterval;
        }
    }

    void FixedUpdate()
    {
        if (!_current.HasValue || !_current.Value.IsValid)
        {
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
            return;
        }

        var t = _current.Value.transform;
        Vector2 myPos = _rb.position;
        Vector2 targetPos = (Vector2)t.position;
        Vector2 to = targetPos - myPos;
        float dist = to.magnitude;

        if (dist > attackRange)
        {
            // accelerate toward target
            Vector2 dir = dist > 1e-6f ? to / dist : Vector2.zero;
            Vector2 desiredVel = dir * moveSpeed;
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, desiredVel, acceleration * Time.fixedDeltaTime);

            // face movement direction
            if (_rb.linearVelocity.sqrMagnitude > 1e-6f)
            {
                float targetAngle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg;
                float newAngle = Mathf.MoveTowardsAngle(_rb.rotation, targetAngle, angularSpeed * Time.fixedDeltaTime);
                _rb.MoveRotation(newAngle);
            }
        }
        else
        {
            // stop + attack on cooldown
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);

            if (Time.time >= _nextAttackTime)
            {
                _current.Value.hp?.Hit(damage);
                _nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    // --- PlayerRegistry hooks ---
    void OnPlayerAdded(PlayerInput pi) => TryAddTarget(pi.gameObject);
    void OnPlayerRemoved(PlayerInput pi) => TryRemoveTarget(pi.gameObject);

    // --- Targets ---
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

    Target? GetClosestTarget()
    {
        if (_targets.Count == 0) return null;

        float bestSqr = float.PositiveInfinity;
        Target? best = null;
        Vector2 p = _rb ? _rb.position : (Vector2)transform.position;

        foreach (var t in _targets)
        {
            if (!t.IsValid) continue;
            if (!t.IsReady) continue;
            float d2 = ((Vector2)t.transform.position - p).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = t; }
        }
        return best;
    }

    public void SetSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
}
