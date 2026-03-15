using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Mage : MonoBehaviour, IPlayer
{
    public PlayerMovement Movement { get; protected set; }
    public PlayerAttackAndTarget AttackAndTargeting { get; protected set; }
    public Health Health { get; protected set; }
    public Revive Revive { get; protected set; }

    public Transform Transform { get { return this.transform; } }
    public GameObject GameObject { get { return this.gameObject; } }

    public bool Initialized { get; protected set; }

    [HideInInspector] public string PType { get; } = "Mage";
    [Header("Mage Fields")]
    [SerializeField] private Projectile magicMissile;
    [SerializeField] private int numberOfMissiles = 1;
    [SerializeField] private float missileSpeed = 7.5f;
    [SerializeField] private float missileDamage = 20.0f;
    [SerializeField] private float missileLifetime = 5.0f;
    [Space]
    [SerializeField] private ProjectileExplosive fireball;
    [SerializeField] private float fireballSpeed = 15.0f;
    [SerializeField] private float fireballDamage = 25.0f;
    [SerializeField] private float fireballRadius = 5.0f;

    private PoolManager poolManager;
    public PlayerInput PInput { get; protected set; }

    protected void Awake()
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

        poolManager = FindAnyObjectByType<PoolManager>();
        if(!poolManager)
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
        // TODO: uhhhhh
    }

    public void BasicAttack()
    {
        Debug.Log($"{this.gameObject.name}: PROC BASIC ATTACK");
        Collider2D[] targetCols = AttackAndTargeting.GetClosestTargets(numberOfMissiles);
        if (targetCols == null) return; // TODO: fuckin uhhhh no targets in range effect lmao

        Vector3 origin = transform.position;
        foreach (var col in targetCols)
        {
            Vector3 aimPoint = AttackAndTargeting.GetAimPoint(col);

            Vector2 dir2 = (Vector2)(aimPoint - origin);
            if (dir2.sqrMagnitude < 1e-6f)
                dir2 = (Vector2)(transform.right);
            else
                dir2.Normalize();

            // Face along +Z with angle around Z
            float angle = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);

            var p = poolManager.Spawn(magicMissile, transform.position, rot);
            p.Init(dir3, missileSpeed, missileDamage, 5.0f, "Enemy", 0);
        }
    }

    public void ClassAbilityA()
    {
        // TODO: INTERLOPE
    }

    public void ClassAbilityB()
    {
        Debug.Log($"{this.gameObject.name}: PROC CLASS B");
        Collider2D targetCol = AttackAndTargeting.GetClosestTarget();
        if (targetCol == null) targetCol = GetComponent<Collider2D>();

        Vector3 origin = transform.position;
        Vector3 aimPoint = AttackAndTargeting.GetAimPoint(targetCol);

        Vector2 dir2 = (Vector2)(aimPoint - origin);
        if (dir2.sqrMagnitude < 1e-6f)
            dir2 = (Vector2)(transform.right);
        else
            dir2.Normalize();

        float angle = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);

        var p = poolManager.Spawn(fireball, transform.position, rot);
        p.Init(dir3, fireballSpeed, fireballDamage, fireballRadius, 5.0f, "Enemy");
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
            case LevelUpUI.UpgradeChoice.BiggerBlast:
                fireballRadius += 0.5f;
                fireballDamage += 5.0f;
                break;
            case LevelUpUI.UpgradeChoice.MoreMissiles:
                numberOfMissiles += 1;
                break;
            case LevelUpUI.UpgradeChoice.BetterMissiles:
                missileSpeed += 2.5f;
                missileDamage += 3.0f;
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
