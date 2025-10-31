using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class TeamDownWatcher : MonoBehaviour
{
    [Header("Hook this to your Game Over flow/UI")]
    [SerializeField] public UnityEvent onTeamWipe;

    [Header("Setup")]
    [Tooltip("If empty, players are auto-discovered on Awake.")]
    [SerializeField] List<Revive> players = new List<Revive>();

    [Header("Race-condition guard")]
    [Tooltip("Confirm on the next frame so multiple downs in one frame register correctly.")]
    [SerializeField] bool confirmNextFrame = true;

    int _aliveCount;
    Coroutine _confirmRoutine;

    void Awake()
    {
        if (players == null || players.Count == 0)
        {
            players = Object.FindObjectsByType<Revive>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();
        }

        foreach (var p in players)
            if (p != null) p.OnStateChanged += HandleStateChanged;

        _aliveCount = players.Count(p => p != null && p.State == LifeState.Alive);
    }

    void OnDestroy()
    {
        foreach (var p in players)
            if (p != null) p.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(Revive p, LifeState oldState, LifeState newState)
    {
        if (oldState == LifeState.Alive && newState == LifeState.Downed) _aliveCount--;
        if (oldState == LifeState.Downed && newState == LifeState.Alive) _aliveCount++;

        if (_aliveCount <= 0) StartConfirm();
        else CancelConfirm();
    }

    void StartConfirm()
    {
        if (_confirmRoutine == null) _confirmRoutine = StartCoroutine(ConfirmTeamWipe());
    }

    void CancelConfirm()
    {
        if (_confirmRoutine != null) { StopCoroutine(_confirmRoutine); _confirmRoutine = null; }
    }

    IEnumerator ConfirmTeamWipe()
    {
        if (confirmNextFrame) yield return null; // ensures simultaneous downs are counted

        if (players.All(p => p == null || p.State == LifeState.Downed))
            onTeamWipe?.Invoke();

        _confirmRoutine = null;
    }

    public void Register(Revive p)
    {
        if (players.Contains(p)) return;
        players.Add(p);
        p.OnStateChanged += HandleStateChanged;
        if (p.State == LifeState.Alive) _aliveCount++;
    }
    public void Unregister(Revive p)
    {
        if (!players.Remove(p)) return;
        p.OnStateChanged -= HandleStateChanged;
        if (p.State == LifeState.Alive) _aliveCount--;
        if (_aliveCount <= 0) StartConfirm();
    }
}
