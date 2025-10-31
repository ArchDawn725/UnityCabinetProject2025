using UnityEngine;

public class Player : MonoBehaviour
{
    // References
    [SerializeField] private PlayerMovement mover; 
    [SerializeField] private ProjectileShooter shooter;  
    [SerializeField] private Health health; 
    [SerializeField] private Revive revive;  

    [Header("States")]
    public bool _initialized;

    private void Awake()
    {
        StartScreenTest.Singleton?.players.Add(this);
    }

    public void Setup()
    {
        if (_initialized) return;
        _initialized = true;

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(true);
        }

        revive.OnStateChanged += (reviveComp, prev, next) =>
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

        mover.EnableMovementNow();
        shooter.Setup();
    }

    public void ApplyUpgrade(LevelUpUI.UpgradeChoice choice)
    {
        switch (choice)
        {
            case LevelUpUI.UpgradeChoice.Survivor:
                health.AddMaxHp(20);
                health.AddHealthRegen(1);
                break;
            case LevelUpUI.UpgradeChoice.Speedster:
                mover.IncreaseMoveSpeed(2.5f);
                revive.DecreaseReviveTime(10);
                break;
            case LevelUpUI.UpgradeChoice.Machinegunner:
                shooter.DecreaseSecondsBetweenShots(0.1f);
                shooter.IncreaseProjectileSpeed(5);
                break;
            case LevelUpUI.UpgradeChoice.HigherCaliber:
                shooter.IncreaseDamage(5);
                shooter.IncreasePiercing(1);
                break;
            case LevelUpUI.UpgradeChoice.Sniper:
                shooter.IncreaseRange(2);
                shooter.IncreaseProjLifetime(2.5f);
                break;
        }
    }
    private void Revived()
    {
        Debug.Log($"{gameObject.name} has been revived!");
        health.FullHeal();
        mover.EnableMovementNow();
        _initialized = true;
    }

    private void Death()
    {
        _initialized = false;
        mover.DisableMovementNow();
    }

    private void OnDestroy()
    {
        StartScreenTest.Singleton.PlayerDeath(this);
    }
}
