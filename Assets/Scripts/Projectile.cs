using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Straight projectile (2D). Moves along XY, calls Hit(damage) on enemies,
/// supports piercing N additional targets before despawning.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : PooledBehaviour
{
    float _speed;
    float _damage;
    float _lifeRemaining;
    string _enemyTag;

    int _pierceCharges = 0; // 0 = stop after first hit, 1 = can hit one more, etc.

    Rigidbody2D _rb;
    Vector2 _dir; // XY direction

    // Track already-hit Healths so we don't hit the same target twice (multi-collider rigs).
    readonly HashSet<int> _hitTargets = new HashSet<int>(8);

    public void Init(Vector3 direction, float speed, float damage, float lifetime, string enemyTag, int pierceCharges)
    {
        _dir = new Vector2(direction.x, direction.y).normalized;
        _speed = Mathf.Max(0f, speed);
        _damage = Mathf.Max(0f, damage);
        _lifeRemaining = Mathf.Max(0.01f, lifetime);
        _enemyTag = enemyTag;
        _pierceCharges = Mathf.Max(0, pierceCharges);

        if (!_rb) _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void Init(Vector2 direction, float speed, float damage, float lifetime, string enemyTag, int pierceCharges) =>
        Init(new Vector3(direction.x, direction.y, 0f), speed, damage, lifetime, enemyTag, pierceCharges);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    public override void OnSpawn()
    {
        if (_rb) { _rb.linearVelocity = Vector2.zero; _rb.angularVelocity = 0; }
        _hitTargets.Clear(); // reset pierce bookkeeping for pooled reuse
    }

    void FixedUpdate()
    {
        if (!gameObject.activeInHierarchy) return;

        // move forward (XY only)
        _rb.MovePosition(_rb.position + _dir * _speed * Time.fixedDeltaTime);

        // lifetime
        _lifeRemaining -= Time.fixedDeltaTime;
        if (_lifeRemaining <= 0f) Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || !_MatchesEnemy(other)) return;

        // Find a Health to hit (collider, its rigidbody, or any parent).
        var target = other.GetComponentInParent<Health>();
        if (target == null) return;

        // Prevent double-hits on the same Health (multi-collider rigs).
        int id = target.GetInstanceID();
        if (!_hitTargets.Add(id)) return;

        // Apply damage
        target.Hit(_damage);

        // Despawn if out of pierce; otherwise consume one charge and keep flying.
        if (_pierceCharges <= 0)
        {
            Despawn();
        }
        else
        {
            _pierceCharges--;
            // keep going; since we're a trigger, we'll naturally pass through and hit the next target
        }
    }

    bool _MatchesEnemy(Collider2D other) =>
        string.IsNullOrEmpty(_enemyTag) || other.CompareTag(_enemyTag);
}
