using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Gunslinger : MonoBehaviour, IPlayer
{
    public PlayerMovement Movement { get; protected set; }
    public PlayerAttackAndTarget AttackAndTargeting { get; protected set; }
    public Health Health { get; protected set; }
    public Revive Revive { get; protected set; }

    public Transform Transform { get; protected set; }
    public GameObject GameObject { get; protected set; }

    public bool Initialized { get; protected set; }

    [HideInInspector] public string PType { get; } = "Gunslinger";
    [Header("Gunslinger Fields")]
    [SerializeField] private Projectile bullet;
    [SerializeField] private float bulletSpeed = 10.0f;
    [SerializeField] private float bulletDamage = 10.0f;
    [SerializeField] private float bulletLifetime = 5.0f;
    [SerializeField] private int bulletPiercing = 0;
    [Space]
    [SerializeField] private float spreadShotTimer = 8.0f;
    [SerializeField] private float spreadShotAngle = 10.0f; //in degrees
    [SerializeField] private float rapidFireTimer = 8.0f;
    [SerializeField] private float rapidFireInterval = 0.1f;

    private float baseAttackInterval;
    private float spreadShotTimeRemaining;
    private float rapidFireTimeRemaining;
    private PoolManager poolManager;
    public PlayerInput PInput { get; protected set; }

    protected void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        AttackAndTargeting = GetComponent<PlayerAttackAndTarget>();
        Health = GetComponent<Health>();
        Revive = GetComponent<Revive>();
        PInput = GetComponent<PlayerInput>();
        Transform = this.transform;
        GameObject = this.gameObject;
        baseAttackInterval = AttackAndTargeting.GetBasicAttackInterval();

        StartScreenTest.Singleton?.players.Add(this);
    }

    public void Setup()
    {
        if (Initialized) return;
        Initialized = true;

        poolManager = FindAnyObjectByType<PoolManager>();
        if (!poolManager)
        {
            Debug.LogError($"{name}: PoolManager not found in scene.", this);
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(true);
        }

        Revive.OnStateChanged += (reviveComp, prev, next) =>
        {
            if (next == LifeState.Alive)
            {
                Revived();
            }
            else if (next == LifeState.Downed)
            {
                Death();
            }
        };

        Movement.EnableMovementNow();
    }

    private void Update()
    {
        if (!Initialized) return;

        if (spreadShotTimeRemaining > 0.0f) spreadShotTimeRemaining -= Time.deltaTime;
        if (rapidFireTimeRemaining > 0.0f) rapidFireTimeRemaining -= Time.deltaTime;

        if (!(rapidFireTimeRemaining > 0.0f)) AttackAndTargeting.SetBasicAttackInterval(baseAttackInterval);
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

        // Face along +Z with angle around Z
        float angle = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);

        var p = poolManager.Spawn(bullet, transform.position, rot);
        p.Init(dir3, bulletSpeed, bulletDamage, bulletLifetime, "Enemy", bulletPiercing);
        if (spreadShotTimeRemaining > 0.0f)
        {
            dir3 = Quaternion.AngleAxis(spreadShotAngle, Vector3.back) * dir3;
            p = poolManager.Spawn(bullet, transform.position, rot);
            p.Init(dir3, bulletSpeed, bulletDamage, bulletLifetime, "Enemy", bulletPiercing);

            dir3 = Quaternion.AngleAxis(-2 * spreadShotAngle, Vector3.back) * dir3;
            p = poolManager.Spawn(bullet, transform.position, rot);
            p.Init(dir3, bulletSpeed, bulletDamage, bulletLifetime, "Enemy", bulletPiercing);
        }
    }

    public void ClassAbilityA()
    {
        Debug.Log($"{this.gameObject.name}: PROC CLASS A");
        spreadShotTimeRemaining = spreadShotTimer;
    }

    public void ClassAbilityB()
    {
        Debug.Log($"{this.gameObject.name} PROC CLASS B");
        baseAttackInterval = AttackAndTargeting.GetBasicAttackInterval();
        rapidFireTimeRemaining = rapidFireTimer;
        AttackAndTargeting.SetBasicAttackInterval(rapidFireInterval);
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
            case LevelUpUI.UpgradeChoice.Machinegunner:
                AttackAndTargeting.IncrementBasicAttackInterval(-0.05f);
                bulletSpeed += 5.0f; //shooter.IncreaseProjectileSpeed(5);
                break;
            case LevelUpUI.UpgradeChoice.HigherCaliber:
                bulletDamage += 5.0f; //shooter.IncreaseDamage(5);
                bulletPiercing += 1; //shooter.IncreasePiercing(1);
                break;
            case LevelUpUI.UpgradeChoice.Sniper:
                //shooter.IncreaseRange(2);
                //shooter.IncreaseProjLifetime(2.5f);
                break;
        }
    }

    // Interface methods have to be public. I ought to find a way to
    // obfuscate access to Revived and Death from outside the class...
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
