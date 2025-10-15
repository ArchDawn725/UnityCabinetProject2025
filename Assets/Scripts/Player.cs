// Player.cs
using UnityEngine;

public class Player : MonoBehaviour
{
    // Hook up references that upgrades will modify
    [SerializeField] private PlayerMovement mover;     // your movement script
    [SerializeField] private ProjectileShooter shooter;           // your shooter
    [SerializeField] private Health health;                       // your generic health

    [Header("Tuning (percentages/amounts)")]
    [SerializeField] private float moveSpeedPercent = 0.20f; // +20%
    [SerializeField] private float damagePercent = 0.25f; // +25%
    [SerializeField] private float fireRatePercent = 0.20f; // +20% faster
    [SerializeField] private float maxHealthFlat = 20f;   // +20 max HP

    public void ApplyUpgrade(LevelUpUI.UpgradeChoice choice)
    {
        switch (choice)
        {
            case LevelUpUI.UpgradeChoice.MoveSpeedUp:
                if (mover)
                {
                    // assumes a public setter or method; if not, add one to your mover
                    mover.SetMoveSpeed(mover.GetMoveSpeed() * (1f + moveSpeedPercent));
                }
                break;

            case LevelUpUI.UpgradeChoice.DamageUp:
                if (shooter)
                    shooter.SetProjectileDamage(shooter.GetProjectileDamage() * (1f + damagePercent));
                break;

            case LevelUpUI.UpgradeChoice.FireRateUp:
                if (shooter)
                    shooter.SetSecondsBetweenShots(shooter.GetSecondsBetweenShots() * (1f - fireRatePercent));
                break;

            case LevelUpUI.UpgradeChoice.MaxHealthUp:
                if (health)
                {
                    // add these helpers to your Health class (see below)
                    health.AddMaxHp(maxHealthFlat);
                    health.Heal(maxHealthFlat);
                }
                break;
        }
    }

    private void OnDestroy()
    {
        StartScreenTest.Singleton.PlayerDeath(this);
    }
}
