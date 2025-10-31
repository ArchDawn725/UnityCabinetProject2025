using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class Initializer : MonoBehaviour
{
    #region References
    [Header("Basics")]
    [SerializeField] private LoadProgress _loadProgressPrefab;

    [Header("Steps (Prefabs)")]
    [Tooltip("Each element is a prefab whose root has a component implementing IAsyncStep.")]
    [SerializeField] private List<GameObject> _stepPrefabs = new();

    // Hidden
    private CancellationTokenSource _cts;
    private LoadProgress _loadProgress;
    public event Action Ready;
    private bool _initialized;
    public event Action Play;

    public static Initializer singleton;

    private void Awake() { singleton = this; }

    #endregion

    #region Construction

    private async void Start()
    {
        ReplaceCts();

        try
        {
            // First frame to let other Awake/OnEnable run
            await Awaitable.NextFrameAsync(_cts.Token);

            await InitializeProgressBar(_cts.Token);
            await InitializeSteps(_cts.Token);
            await CleanUp(_cts.Token);
        }
        catch (OperationCanceledException) { /* normal on teardown */ }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task InitializeProgressBar(CancellationToken ct)
    {
        if (_loadProgressPrefab) 
        { 
            _loadProgress = Instantiate(_loadProgressPrefab);
            await _loadProgress.SetupAsync(ct, this);
        }
        else { Debug.LogWarning("LoadProgress prefab not assigned."); }

        await Awaitable.NextFrameAsync(_cts.Token);
    }

    private async Task InitializeSteps(CancellationToken ct)
    {
        int total = await GetStepCount();
        if (total <= 0)
        {
            _loadProgress?.SetProgress(1f);
            await Awaitable.NextFrameAsync(ct);
            return;
        }

        int done = 0;
        _loadProgress?.SetProgress(0f);
        await Awaitable.NextFrameAsync(ct);

        for (int i = 0; i < _stepPrefabs.Count; i++)
        {
            var prefab = _stepPrefabs[i];
            if (!prefab)
            {
                Debug.LogWarning($"Step prefab at index {i} is null. Counting as one step.");
                done++;
                _loadProgress?.SetProgress(done / (float)total);
                await Awaitable.NextFrameAsync(ct);
                continue;
            }

            // Spawn the prefab once
            var instance = Instantiate(prefab);

            // Collect ALL IAsyncStep components (root + children)
            var steps = instance.GetComponentsInChildren<IAsyncStep>(true);

            if (steps == null || steps.Length == 0)
            {
                // No step components: still counts as one step
                done++;
                _loadProgress?.SetProgress(done / (float)total);
                await Awaitable.NextFrameAsync(ct);
                continue;
            }

            // Run each step sequentially, updating progress after each
            for (int s = 0; s < steps.Length; s++)
            {
                try
                {
                    await steps[s].SetupAsync(ct, this);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    // continue with the remaining steps
                }

                done++;
                _loadProgress?.SetProgress(done / (float)total);
                await Awaitable.NextFrameAsync(ct);
            }
        }

        // Ensure 100% at the end
        _loadProgress?.SetProgress(1f);
        await Awaitable.NextFrameAsync(ct);
    }

    private async Task CleanUp(CancellationToken ct)
    {
        if (_loadProgress)
        {
            _loadProgress.Hide();
            Destroy(_loadProgress.gameObject);
            _loadProgress = null;
        }

        await Awaitable.NextFrameAsync(ct);

        // Signal that the start flow finished and UI can appear
        Ready?.Invoke();
        PlayerInputManager.instance.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
    }


    #endregion

    #region Helpers
    private async Task<int> GetStepCount()
    {
        int count = 0;
        if (_stepPrefabs == null || _stepPrefabs.Count == 0) return count;

        for (int i = 0; i < _stepPrefabs.Count; i++)
        {
            var go = _stepPrefabs[i];
            if (!go) continue;

            // 1) Count IAsyncStep components (root + children)
            int stepComponents = go.GetComponentsInChildren<IAsyncStep>(true).Length;
            count += (stepComponents > 0) ? stepComponents : 1; // no setup task still counts as one

            // keep the UI responsive if this runs during async init
            if ((i & 5) == 0) await Awaitable.NextFrameAsync();
        }

        return count + 1;
    }

    private void ReplaceCts()
    {
        CancelAndDispose(ref _cts);
        _cts = new CancellationTokenSource();
    }

    private static void CancelAndDispose(ref CancellationTokenSource cts)
    {
        if (cts == null) return;
        try { cts.Cancel(); }
        catch { /* ignore */ }
        cts.Dispose();
        cts = null;
    }

    private void OnDestroy() { CancelAndDispose(ref _cts); }

    public void Begin() { if (!_initialized) { _initialized = true; Play?.Invoke(); } }
    #endregion
}
