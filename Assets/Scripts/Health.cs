using System;
using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public event Action<float, float> Changed; // (current, max)
    public event Action Died;

    [Header("Health")]
    [SerializeField, Min(1f)] float maxHp = 100f;
    [SerializeField] float hp;
    [SerializeField] private Transform healthBar;

    [Header("Regen")]
    [SerializeField] bool regenEnabled = true;
    [SerializeField, Min(0f)] float regenPerSecond = 0f;     // HP per second
    [SerializeField, Min(0f)] float regenDelay = 1f;         // seconds after last damage

    float _lastDamageTime;        // time of last Hit()
    Coroutine _regenRoutine;

    void Awake()
    {
        if (hp <= 0f) hp = maxHp;
    }

    void OnEnable()
    {
        UpdateUI();
        StartRegen();
    }

    void OnDisable()
    {
        StopRegen();
    }

    public void Hit(float damage)
    {
        if (damage <= 0f || hp <= 0f) return;

        hp = Mathf.Max(0f, hp - damage);
        _lastDamageTime = Time.time;                 // block regen until delay passes
        UpdateUI();

        if (hp <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || hp <= 0f) return;
        hp = Mathf.Min(maxHp, hp + amount);
        UpdateUI();
    }

    public void FullHeal()
    {
        hp = maxHp;
        UpdateUI();
    }

    public void AddMaxHp(float delta)
    {
        if (Mathf.Abs(delta) < Mathf.Epsilon) return;
        maxHp = Mathf.Max(1f, maxHp + delta);
        FullHeal();
    }
    public void AddHealthRegen(int amount)
    {
        regenPerSecond += amount;
    }

    public void SetMaxHp(float val)
    {
        maxHp = Mathf.Max(1f, val);
        FullHeal();
    }

    public float Current => hp;
    public float Max => maxHp;

    void Die()
    {
        StopRegen();          // stop regen on death
        Died?.Invoke();
    }

    // ---------- Regen loop ----------
    void StartRegen()
    {
        if (!regenEnabled || _regenRoutine != null) return;
        _regenRoutine = StartCoroutine(RegenLoop());
    }

    void StopRegen()
    {
        if (_regenRoutine != null) { StopCoroutine(_regenRoutine); _regenRoutine = null; }
    }

    IEnumerator RegenLoop()
    {
        while (enabled)
        {
            // Freeze regen (and the delay) while paused
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                yield return null;
                continue;
            }

            if (regenEnabled && hp > 0f && hp < maxHp)
            {
                // Wait until delay has passed since last damage
                if (Time.time - _lastDamageTime >= regenDelay)
                {
                    float dt = Time.deltaTime; // per-frame increment
                    if (dt > 0f)
                    {
                        float newHp = Mathf.Min(maxHp, hp + regenPerSecond * dt);
                        if (!Mathf.Approximately(newHp, hp))
                        {
                            hp = newHp;
                            UpdateUI();
                        }
                    }
                }
            }

            yield return null;
        }

        _regenRoutine = null;
    }

    // ---------- Helpers ----------
    void UpdateUI()
    {
        Changed?.Invoke(hp, maxHp);
        if (healthBar)
        {
            var s = healthBar.localScale;
            float x = maxHp > 0f ? (hp / maxHp) : 0f;
            healthBar.localScale = new Vector3(x, s.y, s.z);
        }
    }
}
