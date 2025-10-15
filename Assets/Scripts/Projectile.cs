using UnityEngine;

/// <summary>
/// Simple straight projectile. Moves along a fixed direction, 
/// calls Hit(damage) on enemies it triggers with, then destroys itself.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    float _speed;
    float _damage;
    float _lifeRemaining;
    string _enemyTag;
    Rigidbody _rb;
    Vector3 _dir;

    public void Init(Vector3 direction, float speed, float damage, float lifetime, string enemyTag)
    {
        _dir = direction.normalized;
        _speed = Mathf.Max(0f, speed);
        _damage = Mathf.Max(0f, damage);
        _lifeRemaining = Mathf.Max(0.01f, lifetime);
        _enemyTag = enemyTag;

        if (!_rb) _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true; // moving via MovePosition (trigger collisions)
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void FixedUpdate()
    {
        // move forward
        _rb.MovePosition(_rb.position + _dir * _speed * Time.fixedDeltaTime);

        // lifetime
        _lifeRemaining -= Time.fixedDeltaTime;
        if (_lifeRemaining <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other || !_MatchesEnemy(other)) return;

        // Try to find a Hit(damage) receiver on the collider, its rigidbody, or parent
        if (TryHit(other.gameObject)) { Destroy(gameObject); return; }
        if (other.attachedRigidbody && TryHit(other.attachedRigidbody.gameObject)) { Destroy(gameObject); return; }
        if (other.transform.parent && TryHit(other.transform.parent.gameObject)) { Destroy(gameObject); return; }
    }

    bool _MatchesEnemy(Collider other) =>
        string.IsNullOrEmpty(_enemyTag) || other.CompareTag(_enemyTag);

    bool TryHit(GameObject go)
    {
        // Fast-path: common pattern is an EnemyHealth with Hit(float)
        var enemy = go.GetComponent<Health>();
        if (enemy != null) { enemy.Hit(_damage); return true; }

        return false;
    }
}
