using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IPlayer
{
    PlayerMovement Movement { get; }
    PlayerAttackAndTarget AttackAndTargetting { get; }
    Health Health { get; }
    Revive Revive { get; }

    Transform Transform { get; }
    GameObject GameObject { get; }

    bool Initialized { get; }

    PlayerInput PInput { get; }

    string PType { get; }

    void Setup();

    void BasicAttack();

    void ClassAbilityA();

    void ClassAbilityB();

    void ApplyUpgrade(LevelUpUI.UpgradeChoice choice);

    void Revived();

    void Death();
}
