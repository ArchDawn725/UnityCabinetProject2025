using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileShooter : MonoBehaviour
{
    [Header("Detection (trigger)")]
    [SerializeField, Min(0.1f)] float detectionRadius = 10f;
    [SerializeField] string enemyTag = "Enemy";

    [Header("Firing")]
    [Tooltip("Seconds between shots")]
    [SerializeField, Min(0.01f)] float secondsBetweenShots = 0.4f;
    [SerializeField] bool fireImmediatelyOnEnter = true;
    [SerializeField] Projectile projectilePrefab;
    [SerializeField, Min(0f)] float projectileSpeed = 20f;
    [SerializeField, Min(0f)] float projectileDamage = 10f;
    [SerializeField, Min(0.01f)] float projectileLifetime = 5f;
    [SerializeField, Min(0)] int piercing = 0;

    [SerializeField] private List<Collider2D> _targets = new();
    CircleCollider2D _trigger;
    float _nextShotTime;

    const float EPS = 1.1f;
    private PoolManager poolManager;
    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
        _trigger = GetComponent<CircleCollider2D>();
        _trigger.isTrigger = true;
        _trigger.radius = detectionRadius;

        if (!projectilePrefab)
            Debug.LogWarning($"{name}: projectilePrefab not assigned.", this);

        _nextShotTime = 0f;
    }

    public void Setup()
    {
        poolManager = FindAnyObjectByType<PoolManager>();
        if (!poolManager)
        {
            Debug.LogError($"{name}: PoolManager not found in scene.", this);
        }
    }

    void Update()
    {
        if (!_player._initialized) return;
        PruneTargets();

        if (Time.time >= _nextShotTime && _targets.Count > 0)
        {
            var target = GetClosestTarget();
            if (target) FireAt(target);
            _nextShotTime = Time.time + secondsBetweenShots;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || !other.gameObject.CompareTag(enemyTag)) return;

        if (!_targets.Contains(other))
        {
            _targets.Add(other);

            if (fireImmediatelyOnEnter && Time.time >= _nextShotTime)
            {
                FireAt(other);
                _nextShotTime = Time.time + secondsBetweenShots;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other || !other.gameObject.CompareTag(enemyTag)) return;
        _targets.Remove(other);
    }

    // --- Core ---

    void FireAt(Collider2D targetCol)
    {
        if (!projectilePrefab || !targetCol || !_player._initialized) return;

        Vector3 origin = transform.position;

        // Prefer Health.AimAnchor if available
        Vector3 aimPoint = GetAimPoint(targetCol);

        // 2D direction (XY plane), z = 0
        Vector2 dir2 = (Vector2)(aimPoint - origin);
        if (dir2.sqrMagnitude < 1e-6f)
            dir2 = (Vector2)(transform.right); // fallback in 2D: +X
        else
            dir2.Normalize();

        // Face along +Z with angle around Z
        float angle = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        var p = poolManager.Spawn(projectilePrefab, transform.position, rot);

        // Pass a 3D vector with z=0 to keep your existing Projectile.Init signature
        Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);
        p.Init(dir3, projectileSpeed, projectileDamage, projectileLifetime, enemyTag, piercing);
    }

    [SerializeField, Range(0f, 1f)] private float fallbackChestHeight = 0.65f;
    Vector3 GetAimPoint(Collider2D col)
    {
        // 1) Try Health anchor on this object or its parents (handles multi-collider rigs)
        var health = col.GetComponentInParent<Health>();

        // 2) Fallback: “chest” from combined bounds across all colliders on the same rigidbody2D
        Bounds b = col.bounds;
        var rb = col.attachedRigidbody;
        if (rb)
        {
            var cols = rb.GetComponentsInChildren<Collider2D>();
            if (cols.Length > 0)
            {
                b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            }
        }

        float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(fallbackChestHeight));
        return new Vector3(b.center.x, y, transform.position.z); // z stays on shooter’s plane
    }

    Collider2D GetClosestTarget()
    {
        Collider2D best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = transform.position;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var col = _targets[i];
            if (!IsValid(col)) { _targets.RemoveAt(i); continue; }

            float d2 = ((Vector2)col.bounds.center - (Vector2)origin).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = col; }
        }
        return best;
    }

    void PruneTargets()
    {
        Vector2 c = GetWorldCenter2D();
        float r = GetWorldRadius2D();
        float r2 = r * r;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var col = _targets[i];
            if (!IsValid(col)) { _targets.RemoveAt(i); continue; }

            // Use ClosestPoint so large enemies near the edge aren’t culled early
            Vector2 p = col.ClosestPoint(c);
            float d2 = (p - c).sqrMagnitude;

            if (d2 > r2 + (detectionRadius * EPS))
            {
                _targets.RemoveAt(i);
            }

        }
    }

    bool IsValid(Collider2D col) =>
        col && col.gameObject.activeInHierarchy;

    Vector2 GetWorldCenter2D()
    {
        // CircleCollider2D.offset is local; convert to world
        return _trigger
            ? (Vector2)_trigger.transform.TransformPoint((Vector3)_trigger.offset)
            : (Vector2)transform.position;
    }

    float GetWorldRadius2D()
    {
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        float r = _trigger ? _trigger.radius : detectionRadius;
        return r * scale;
    }

    public void DecreaseSecondsBetweenShots(float amount) => secondsBetweenShots -= amount;
    public void IncreaseProjectileSpeed(int amount) => projectileSpeed += amount;
    public void IncreasePiercing(int amount) => piercing += amount;
    public void IncreaseDamage(int amount) => projectileDamage += amount;
    public void IncreaseProjLifetime(float amount) => projectileLifetime += amount;
    public void IncreaseRange(float amount) { detectionRadius += amount; _trigger.radius = detectionRadius; }
}
