using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileExplosive : PooledBehaviour
{
    float _targetSpeed;
    float _currSpeed;
    float _damage;
    float _radius;
    float _lifeRemaining;
    string _enemyTag;

    Rigidbody2D _rb;
    Vector2 _dir; // XY direction

    public void Init(Vector3 direction, float speed, float damage, float radius, float lifetime, string enemyTag)
    {
        _dir = new Vector2(direction.x, direction.y).normalized;
        _targetSpeed = Mathf.Max(0f, speed);
        _currSpeed = 0.5f;
        _damage = Mathf.Max(0f, damage);
        _radius = Mathf.Max(0f, radius);
        _lifeRemaining = Mathf.Max(0.01f, lifetime);
        _enemyTag = enemyTag;

        if (!_rb) _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void Init(Vector2 direction, float speed, float damage, float radius, float lifetime, string enemyTag) =>
        Init(new Vector3(direction.x, direction.y, 0f), speed, damage, radius, lifetime, enemyTag);

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
    }

    void FixedUpdate()
    {
        if (!gameObject.activeInHierarchy) return;

        _currSpeed = Mathf.MoveTowards(_currSpeed, _targetSpeed, 0.5f);

        // move forward (XY only)
        _rb.MovePosition(_rb.position + _dir * _currSpeed * Time.fixedDeltaTime);

        // lifetime
        _lifeRemaining -= Time.fixedDeltaTime;
        if (_lifeRemaining <= 0f) Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || !_MatchesEnemy(other)) return;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(this.transform.position, _radius);
        foreach(Collider2D enemy in enemies)
        {
            if (!_MatchesEnemy(enemy)) continue;

            var target = enemy.GetComponentInParent<Health>();
            if (target == null) continue;

            target.Hit(_damage);
        }
        Despawn();
    }

    bool _MatchesEnemy(Collider2D other) =>
        string.IsNullOrEmpty(_enemyTag) || other.CompareTag(_enemyTag);
}
