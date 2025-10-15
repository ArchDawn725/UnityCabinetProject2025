using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public event Action<float, float> Changed; // (current, max)
    public event Action Died;

    [SerializeField, Min(1f)] float maxHp = 100f;
    [SerializeField] float hp;
    [SerializeField] private Transform healthBar;

    [Header("Aiming (optional)")]
    [Tooltip("Drag a head/chest transform here. Shooter will aim at this point.")]
    [SerializeField] private Transform aimAnchor;
    public Transform AimAnchor => aimAnchor;

    void Awake()
    {
        if (hp <= 0f) hp = maxHp;
    }
    void OnEnable()
    {
        Changed?.Invoke(hp, maxHp); // initialize UI on spawn/enable
    }

    public void Hit(float damage)
    {
        if (damage <= 0f) return;
        hp = Mathf.Max(0f, hp - damage);
        Changed?.Invoke(hp, maxHp);
        healthBar.localScale = new Vector3(hp / maxHp, healthBar.localScale.y, healthBar.localScale.z);
        if (hp <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Min(maxHp, hp + amount);
        Changed?.Invoke(hp, maxHp);
        healthBar.localScale = new Vector3(hp / maxHp, healthBar.localScale.y, healthBar.localScale.z);
    }

    public void AddMaxHp(float delta)
    {
        if (Mathf.Abs(delta) < Mathf.Epsilon) return;
        maxHp = Mathf.Max(1f, maxHp + delta);
        hp = maxHp;
        hp = Mathf.Min(hp, maxHp);
        Changed?.Invoke(hp, maxHp); // <-- updates bar on level-up
        healthBar.localScale = new Vector3(hp / maxHp, healthBar.localScale.y, healthBar.localScale.z);
    }

    public float Current => hp;
    public float Max => maxHp;

    void Die()
    {
        Died?.Invoke();
        Destroy(gameObject); // if you destroy on death
    }
}
