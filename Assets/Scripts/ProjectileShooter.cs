using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class ProjectileShooter : MonoBehaviour
{
    [Header("Detection (trigger)")]
    [SerializeField, Min(0.1f)] float detectionRadius = 10f;
    [SerializeField] string enemyTag = "enemy";

    [Header("Firing")]
    [Tooltip("Seconds between shots")]
    [SerializeField, Min(0.01f)] float secondsBetweenShots = 0.4f;
    [SerializeField] bool fireImmediatelyOnEnter = true;
    [SerializeField] Transform muzzle;                       // spawn point; defaults to self
    [SerializeField] Projectile projectilePrefab;
    [SerializeField, Min(0f)] float projectileSpeed = 20f;
    [SerializeField, Min(0f)] float projectileDamage = 10f;
    [SerializeField, Min(0.01f)] float projectileLifetime = 5f;

    readonly List<Collider> _targets = new();
    SphereCollider _trigger;
    float _nextShotTime;

    const float EPS = 0.001f;

    void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius = detectionRadius;

        if (!muzzle) muzzle = transform;
        if (!projectilePrefab)
            Debug.LogWarning($"{name}: projectilePrefab not assigned.", this);

        _nextShotTime = 0f; // allow an immediate shot
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

    void OnTriggerEnter(Collider other)
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

    void OnTriggerExit(Collider other)
    {
        if (!other || !other.gameObject.CompareTag(enemyTag)) return;
        _targets.Remove(other);
    }

    // --- Core ---

    void FireAt(Collider targetCol)
    {
        if (!projectilePrefab || !targetCol) return;

        Vector3 origin = muzzle ? muzzle.position : transform.position;

        // Prefer Health.AimAnchor if available
        Vector3 aimPoint = GetAimPoint(targetCol);

        Vector3 dir = aimPoint - origin;
        if (dir.sqrMagnitude < 1e-6f)
            dir = (muzzle ? muzzle.forward : transform.forward);
        else
            dir.Normalize();

        var proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
        proj.Init(dir, projectileSpeed, projectileDamage, projectileLifetime, enemyTag);
    }

    // Add near your other serialized fields (optional tweak)
    [SerializeField, Range(0f, 1f)] private float fallbackChestHeight = 0.65f;
    Vector3 GetAimPoint(Collider col)
    {
        // 1) Try Health anchor on this object or its parents (handles multi-collider rigs)
        var health = col.GetComponentInParent<Health>();
        if (health && health.AimAnchor) return health.AimAnchor.position;

        // 2) Fallback: chest-ish from combined bounds on the rigidbody’s colliders
        Bounds b = col.bounds;
        var rb = col.attachedRigidbody;
        if (rb)
        {
            var cols = rb.GetComponentsInChildren<Collider>();
            if (cols.Length > 0)
            {
                b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            }
        }

        float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(fallbackChestHeight));
        return new Vector3(b.center.x, y, b.center.z);
    }

    Collider GetClosestTarget()
    {
        Collider best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = muzzle ? muzzle.position : transform.position;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var col = _targets[i];
            if (!IsValid(col)) { _targets.RemoveAt(i); continue; }

            // Distance from muzzle to collider’s center
            float d2 = (col.bounds.center - origin).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = col; }
        }
        return best;
    }

    void PruneTargets()
    {
        Vector3 c = GetWorldCenter();
        float r = GetWorldRadius();
        float r2 = r * r;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var col = _targets[i];
            if (!IsValid(col)) { _targets.RemoveAt(i); continue; }

            // Use ClosestPoint so large enemies near the edge aren’t culled early
            Vector3 p = col.ClosestPoint(c);
            float d2 = (p - c).sqrMagnitude;

            if (d2 > r2 + EPS)
                _targets.RemoveAt(i);
        }
    }

    bool IsValid(Collider col) =>
        col && col.gameObject.activeInHierarchy;

    Vector3 GetWorldCenter()
    {
        // SphereCollider.center is local-space; convert to world
        return _trigger
            ? _trigger.transform.TransformPoint(_trigger.center)
            : transform.position;
    }

    float GetWorldRadius()
    {
        float scale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));
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
        if (!_trigger) _trigger = GetComponent<SphereCollider>();
        if (_trigger) _trigger.radius = detectionRadius;
        if (secondsBetweenShots < 0.01f) secondsBetweenShots = 0.01f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetWorldCenter(), GetWorldRadius());
    }
#endif

    public float GetProjectileDamage() => projectileDamage;
    public void SetProjectileDamage(float v) => projectileDamage = Mathf.Max(0f, v);
    public float GetSecondsBetweenShots() => secondsBetweenShots;
    public void SetSecondsBetweenShots(float v) => secondsBetweenShots = Mathf.Max(0.01f, v);
}
