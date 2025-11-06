using System.Collections;
using UnityEngine;

public class Revive : MonoBehaviour
{
    [SerializeField] float autoReviveSeconds = 60f;

    public LifeState State = LifeState.Alive;
    public event System.Action<Revive, LifeState, LifeState> OnStateChanged;

    Coroutine _autoRevive;
    [SerializeField] private Transform reviveBar;

    private void Start()
    {
        transform.GetComponent<Health>().Died += Down;
        FindAnyObjectByType<TeamDownWatcher>()?.Register(this);
    }
    private void OnDestroy()
    {
        var health = transform.GetComponent<Health>();
        if (health != null)
            health.Died -= Down;
    }

    private void Down()
    {
        if (State == LifeState.Downed) return;
        SetState(LifeState.Downed);

        if (_autoRevive != null) StopCoroutine(_autoRevive);
        _autoRevive = StartCoroutine(AutoReviveTimer());
        reviveBar.GetComponent<SpriteRenderer>().color = Color.green;
    }

    private void ReviveMe()
    {
        reviveBar.GetComponent<SpriteRenderer>().color = Color.red;
        if (State == LifeState.Alive) return;
        if (_autoRevive != null) { StopCoroutine(_autoRevive); _autoRevive = null; }
        SetState(LifeState.Alive);
    }

    IEnumerator AutoReviveTimer()
    {
        // Reset/show the bar
        UpdateReviveBar(0f);

        float elapsed = 0f;

        while (elapsed < autoReviveSeconds && State == LifeState.Downed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / autoReviveSeconds); // 0→1
            UpdateReviveBar(t);                                   // progress fill
            yield return null;
        }

        _autoRevive = null;

        // If still downed when the timer ends, revive and finish the bar
        if (State == LifeState.Downed)
        {
            ReviveMe();
        }
    }

    void UpdateReviveBar(float t)
    {
        if (!reviveBar) return;
        var s = reviveBar.localScale;
        reviveBar.localScale = new Vector3(t, s.y, s.z); // fill along X
    }


    void SetState(LifeState next)
    {
        var prev = State;
        State = next;
        OnStateChanged?.Invoke(this, prev, next);
    }

    public void DecreaseReviveTime(int amount)
    {
        autoReviveSeconds -= amount;
    }
}

public enum LifeState { Alive, Downed }
