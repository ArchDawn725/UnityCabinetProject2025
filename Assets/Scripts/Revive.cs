using System.Collections;
using UnityEngine;

public class Revive : MonoBehaviour
{
    [SerializeField] float autoReviveSeconds = 60f;

    public LifeState State { get; private set; } = LifeState.Alive;
    public event System.Action<Revive, LifeState, LifeState> OnStateChanged;

    Coroutine _autoRevive;

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
        Debug.Log($"{gameObject.name} is downed.");
        if (State == LifeState.Downed) return;
        SetState(LifeState.Downed);

        if (_autoRevive != null) StopCoroutine(_autoRevive);
        _autoRevive = StartCoroutine(AutoReviveTimer());
    }

    private void ReviveMe()
    {
        if (State == LifeState.Alive) return;
        if (_autoRevive != null) { StopCoroutine(_autoRevive); _autoRevive = null; }
        SetState(LifeState.Alive);
    }

    IEnumerator AutoReviveTimer()
    {
        yield return new WaitForSeconds(autoReviveSeconds);
        // If still downed after the timer, stand back up automatically.
        if (State == LifeState.Downed) { _autoRevive = null; ReviveMe(); }
    }

    void SetState(LifeState next)
    {
        var prev = State;
        State = next;
        OnStateChanged?.Invoke(this, prev, next);
    }
}

public enum LifeState { Alive, Downed }
