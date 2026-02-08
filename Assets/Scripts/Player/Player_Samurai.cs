using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;

public class Player_Samurai : MonoBehaviour, IPlayer
{
    public PlayerMovement Movement { get; protected set; }
    public PlayerAttackAndTarget AttackAndTargeting { get; protected set; }
    public Health Health { get; protected set; }
    public Revive Revive { get; protected set; }

    public Transform Transform { get { return this.transform; } }
    public GameObject GameObject { get { return this.gameObject; } }

    public bool Initialized { get; protected set; }

    [HideInInspector] public string PType { get; } = "Samurai";
    [Header("Samurai Fields")]
    [SerializeField] private float swordDamage = 8.0f;
    [SerializeField] private float HitboxWidth = 3.0f;
    [SerializeField] private float HitboxRange = 1.0f;

    private float ClassAHit2Time = -1.0f;
    private float ClassAHit3Time = -1.0f;
    private float ClassBHitInterval = 0.25f;

    public PlayerInput PInput { get; protected set; }

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        AttackAndTargeting = GetComponent<PlayerAttackAndTarget>();
        Health = GetComponent<Health>();
        Revive = GetComponent<Revive>();
        PInput = GetComponent<PlayerInput>();

        StartScreenTest.Singleton?.players.Add(this);
    }

    public void Setup()
    {
        if (Initialized) return;
        Initialized = true;

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(true);
        }

        Revive.OnStateChanged += (reviveComp, prev, next) =>
        {
            if (next == LifeState.Alive) Revived();
            else if (next == LifeState.Downed) Death();
        };

        Movement.EnableMovementNow();
    }

    private void Update()
    {
        if (!Initialized) return;
        
        if(ClassAHit2Time >= 0.0f && Time.time >= ClassAHit2Time)
        {
            AttackInRadius(5.5f);
            ClassAHit2Time = -1.0f;
        }

        if (ClassAHit3Time >= 0.0f && Time.time >= ClassAHit3Time)
        {
            AttackInRadius(5.5f);
            ClassAHit3Time = -1.0f;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawCube(
            center: transform.position + transform.up * HitboxRange / 2,
            size: new Vector3(HitboxWidth, HitboxRange, 0.5f));
    }

    public void BasicAttack()
    {
        Debug.Log($"{this.gameObject.name}: PROC BASIC ATTACK");
        Collider2D targetCol = AttackAndTargeting.GetClosestTarget();
        if (targetCol == null) targetCol = GetComponent<Collider2D>();

        Vector3 origin = transform.position;
        Vector3 aimPoint = AttackAndTargeting.GetAimPoint(targetCol);
        Vector2 dir2 = (Vector2)(aimPoint - origin);
        if (dir2.sqrMagnitude < 1e-6f)
            dir2 = (Vector2)(transform.right);
        else
            dir2.Normalize();
        float angle = Mathf.Atan2(dir2.x, dir2.y) * Mathf.Rad2Deg;

        List<Collider2D> enemies;

        Collider2D[] colliders;
        colliders = Physics2D.OverlapBoxAll(
            point: (Vector2)transform.position + dir2 * HitboxRange / 2,
            size: new Vector2(HitboxWidth, HitboxRange),
            angle: angle);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = (Vector2)transform.position + dir2 * HitboxRange / 2;
        cube.transform.eulerAngles = new Vector3 (0f, 0f, angle);

        enemies = colliders.Where(col => col.gameObject.tag == "Enemy").ToList();
        foreach (Collider2D col in enemies)
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null) continue;
            target.Hit(swordDamage);
        }
    }

    public void ClassAbilityA()
    {
        Debug.Log($"{this.gameObject.name}: PROC CLASS A PSYCHE");
        Movement.Dash(Movement.GetInputDir() * 2);
        ClassAHit2Time = Time.time + 0.25f;
        ClassAHit3Time = Time.time + 0.50f;
        AttackInRadius(5.5f);
    }

    private void AttackInRadius(float rad)
    {
        List<Collider2D> enemies;
        Collider2D[] colliders;
        colliders = Physics2D.OverlapCircleAll(
            point: (Vector2)transform.position,
            radius: rad);

        enemies = colliders.Where(col => col.gameObject.tag == "Enemy").ToList();
        foreach (Collider2D col in enemies)
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null) continue;
            target.Hit(swordDamage);
        }
    }

    public void ClassAbilityB()
    {
        StartCoroutine(ClassBHitCoroutine());
    }

    private IEnumerator ClassBHitCoroutine()
    {
        Health.SetInvincible(true);
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(ClassBHitInterval);
            AttackInRadius(7.0f);
        }
        Health.SetInvincible(false);
    }

    public void ApplyUpgrade(LevelUpUI.UpgradeChoice choice)
    {
        switch (choice)
        {
            case LevelUpUI.UpgradeChoice.Survivor:
                Health.AddMaxHp(20);
                Health.AddHealthRegen(1);
                break;
            case LevelUpUI.UpgradeChoice.Speedster:
                Movement.IncreaseMoveSpeed(2.5f);
                Revive.DecreaseReviveTime(10);
                break;
            case LevelUpUI.UpgradeChoice.Swordmaster:
                AttackAndTargeting.IncrementBasicAttackInterval(-0.05f);
                //bulletSpeed += 5.0f; //shooter.IncreaseProjectileSpeed(5);
                break;
            case LevelUpUI.UpgradeChoice.SweepingEdge:
                //bulletDamage += 5.0f; //shooter.IncreaseDamage(5);
                //bulletPiercing += 1; //shooter.IncreasePiercing(1);
                break;
            case LevelUpUI.UpgradeChoice.SharperBlade:
                //shooter.IncreaseRange(2);
                //shooter.IncreaseProjLifetime(2.5f);
                break;
        }
    }

    public void Revived()
    {
        Debug.Log($"{this.gameObject.name} has been revived!");
        Health.FullHeal();
        Movement.EnableMovementNow();
        Initialized = true;
    }

    public void Death()
    {
        Initialized = false;
        Movement.DisableMovementNow();
    }

    private void OnDestroy()
    {
        StartScreenTest.Singleton.PlayerDeath(this);
    }
}
