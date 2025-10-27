using UnityEngine;

/// <summary>
/// Simple straight projectile (2D). Moves along a fixed direction on XY,
/// calls Hit(damage) on enemies it triggers with, then destroys itself.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : PooledBehaviour
{
    float _speed;
    float _damage;
    float _lifeRemaining;
    string _enemyTag;

    Rigidbody2D _rb;
    Vector2 _dir; // XY direction

    // Keep the original API; Z is ignored in 2D.
    public void Init(Vector3 direction, float speed, float damage, float lifetime, string enemyTag)
    {
        _dir = new Vector2(direction.x, direction.y).normalized;
        _speed = Mathf.Max(0f, speed);
        _damage = Mathf.Max(0f, damage);
        _lifeRemaining = Mathf.Max(0.01f, lifetime);
        _enemyTag = enemyTag;

        if (!_rb) _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic; // MovePosition for trigger collisions
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    // Optional convenience overload for pure 2D callers.
    public void Init(Vector2 direction, float speed, float damage, float lifetime, string enemyTag) =>
        Init(new Vector3(direction.x, direction.y, 0f), speed, damage, lifetime, enemyTag);

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

        // move forward (XY only; Z is unchanged)
        _rb.MovePosition(_rb.position + _dir * _speed * Time.fixedDeltaTime);

        // lifetime
        _lifeRemaining -= Time.fixedDeltaTime;
        if (_lifeRemaining <= 0f) Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || !_MatchesEnemy(other)) return;

        // Try to find a Hit(damage) receiver on the collider, its rigidbody, or parent
        if (TryHit(other.gameObject)) { Despawn(); return; }

        if (other.attachedRigidbody && TryHit(other.attachedRigidbody.gameObject))
        { Despawn(); return; }

        if (other.transform.parent && TryHit(other.transform.parent.gameObject))
        { Despawn(); return; }
    }

    bool _MatchesEnemy(Collider2D other) =>
        string.IsNullOrEmpty(_enemyTag) || other.CompareTag(_enemyTag);

    bool TryHit(GameObject go)
    {
        var enemy = go.GetComponent<Health>();
        if (enemy != null) { enemy.Hit(_damage); return true; }
        return false;
    }
}
