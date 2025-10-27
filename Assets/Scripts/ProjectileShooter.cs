using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))] // kinematic recommended for trigger messages
public class ProjectileShooter : MonoBehaviour
{
    [Header("Detection (trigger)")]
    [SerializeField, Min(0.1f)] float detectionRadius = 10f;
    [SerializeField] string enemyTag = "Enemy";

    [Header("Firing")]
    [Tooltip("Seconds between shots")]
    [SerializeField, Min(0.01f)] float secondsBetweenShots = 0.4f;
    [SerializeField] bool fireImmediatelyOnEnter = true;
    [SerializeField] Transform muzzle;                       // spawn point; defaults to self
    [SerializeField] Projectile projectilePrefab;
    [SerializeField, Min(0f)] float projectileSpeed = 20f;
    [SerializeField, Min(0f)] float projectileDamage = 10f;
    [SerializeField, Min(0.01f)] float projectileLifetime = 5f;

    readonly List<Collider2D> _targets = new();
    CircleCollider2D _trigger;
    Rigidbody2D _rb2d;
    float _nextShotTime;

    const float EPS = 0.001f;
    private PoolManager poolManager;

    void Awake()
    {
        _trigger = GetComponent<CircleCollider2D>();
        _trigger.isTrigger = true;
        _trigger.radius = detectionRadius;

        _rb2d = GetComponent<Rigidbody2D>();
        _rb2d.isKinematic = true;     // this object is just a sensor/shooter
        _rb2d.gravityScale = 0f;

        if (!muzzle) muzzle = transform;
        if (!projectilePrefab)
            Debug.LogWarning($"{name}: projectilePrefab not assigned.", this);

        _nextShotTime = 0f; // allow an immediate shot
    }

    private void Start()
    {
        poolManager = FindAnyObjectByType<PoolManager>();
        if (!poolManager)
        {
            Debug.LogError($"{name}: PoolManager not found in scene.", this);
        }
    }

    void Update()
    {
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
        if (!projectilePrefab || !targetCol) return;

        Vector3 origin = muzzle ? muzzle.position : transform.position;

        // Prefer Health.AimAnchor if available
        Vector3 aimPoint = GetAimPoint(targetCol);

        // 2D direction (XY plane), z = 0
        Vector2 dir2 = (Vector2)(aimPoint - origin);
        if (dir2.sqrMagnitude < 1e-6f)
            dir2 = (Vector2)(muzzle ? muzzle.right : transform.right); // fallback in 2D: +X
        else
            dir2.Normalize();

        // Face along +Z with angle around Z
        float angle = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        //var proj = Instantiate(projectilePrefab, origin, rot);
        var p = poolManager.Spawn(projectilePrefab, muzzle.position, rot);

        // Pass a 3D vector with z=0 to keep your existing Projectile.Init signature
        Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);
        //proj.Init(dir3, projectileSpeed, projectileDamage, projectileLifetime, enemyTag);
        p.Init(dir3, projectileSpeed, projectileDamage, projectileLifetime, enemyTag);
    }

    [SerializeField, Range(0f, 1f)] private float fallbackChestHeight = 0.65f;
    Vector3 GetAimPoint(Collider2D col)
    {
        // 1) Try Health anchor on this object or its parents (handles multi-collider rigs)
        var health = col.GetComponentInParent<Health>();
        if (health && health.AimAnchor) return health.AimAnchor.position;

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
        Vector3 origin = muzzle ? muzzle.position : transform.position;

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

            if (d2 > r2 + EPS)
                _targets.RemoveAt(i);
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

    // --- Utilities / Debug ---

    public void SetDetectionRadius(float radius)
    {
        detectionRadius = Mathf.Max(0.1f, radius);
        if (_trigger) _trigger.radius = detectionRadius;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        //if (!_trigger) _trigger = GetComponent<CircleCollider2D>();
        //if (_trigger) _trigger.radius = detectionRadius;
        if (secondsBetweenShots < 0.01f) secondsBetweenShots = 0.01f;

        // Keep RB2D present/kinematic for 2D trigger callbacks
        if (!_rb2d) _rb2d = GetComponent<Rigidbody2D>();
        if (_rb2d)
        {
            _rb2d.isKinematic = true;
            _rb2d.gravityScale = 0f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 c = GetWorldCenter2D();
        float r = GetWorldRadius2D();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(c.x, c.y, transform.position.z), r);
    }
#endif

    public float GetProjectileDamage() => projectileDamage;
    public void SetProjectileDamage(float v) => projectileDamage = Mathf.Max(0f, v);
    public float GetSecondsBetweenShots() => secondsBetweenShots;
    public void SetSecondsBetweenShots(float v) => secondsBetweenShots = Mathf.Max(0.01f, v);
}
