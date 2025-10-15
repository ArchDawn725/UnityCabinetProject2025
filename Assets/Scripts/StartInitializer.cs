using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class StartInitializer : MonoBehaviour
{
    #region References
    [Header("Basics")]
    [SerializeField] private LoadProgress _loadProgressPrefab;
    [SerializeField] private Camera _mainCameraPrefab;
    [SerializeField] private MultiplayerEventSystem _eventSystem;

    [Header("Steps (Prefabs)")]
    [Tooltip("Each element is a prefab whose root has a component implementing IAsyncStep.")]
    [SerializeField] private List<GameObject> _stepPrefabs = new();

    // Hidden / runtime
    public static StartInitializer singleton { get; private set; }
    private CancellationTokenSource _cts;
    private LoadProgress _loadProgress;
    public event Action Ready;
    private readonly List<GameObject> _spawnedSteps = new();

    private void Awake()
    {
        if (singleton != null && singleton != this)
        {
            Debug.LogError($"{nameof(StartInitializer)}: Duplicate instance detected; destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        singleton = this;
    }
    #endregion

    #region Construction
    private async void Start()
    {
        ReplaceCts();

        try
        {
            // First frame to let other Awake/OnEnable run
            await Awaitable.NextFrameAsync(_cts.Token);

            await InitializeBasics(_cts.Token);
            await InitializeMain(_cts.Token);
            await CleanUp(_cts.Token);
        }
        catch (OperationCanceledException) { /* normal on teardown */ }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void OnDestroy()
    {
        if (singleton == this) singleton = null;
        CancelAndDispose(ref _cts);
    }

    private async Task InitializeBasics(CancellationToken ct)
    {
        // 1) Progress UI
        if (_loadProgressPrefab)
        {
            _loadProgress = Instantiate(_loadProgressPrefab);
        }
        else
        {
            Debug.LogWarning("LoadProgress prefab not assigned.");
        }

        // 2) Main Camera (only if none exists)
        if (_mainCameraPrefab)
        {
            var existingCam = Camera.main;
            if (existingCam == null)
            {
                Instantiate(_mainCameraPrefab);
            }
        }
        else
        {
            Debug.LogWarning("Main Camera prefab not assigned. Skipping camera spawn.");
        }

        // 3) EventSystem (only if none exists)
        if (EventSystem.current == null)
        {
            if (_eventSystem) Instantiate(_eventSystem);
            else Debug.LogWarning("EventSystem prefab not assigned.");
        }

        await Awaitable.NextFrameAsync(ct);
    }

    private async Task InitializeMain(CancellationToken ct)
    {
        int count = _stepPrefabs?.Count ?? 0;
        if (count == 0)
        {
            _loadProgress?.SetProgress(1f);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // Visual progress (start-of-step)
            _loadProgress?.SetProgress((float)i / count);

            var prefab = _stepPrefabs[i];
            if (!prefab)
            {
                Debug.LogWarning($"Step {i} prefab is null. Skipping.");
                continue;
            }

            // Spawn the step prefab
            var instance = Instantiate(prefab);
            _spawnedSteps.Add(instance);

            // Require IAsyncStep on the root (per tooltip)
            if (!instance.TryGetComponent<IAsyncStep>(out var step))
            {
                Debug.LogWarning($"Spawned step '{instance.name}' has no component implementing IAsyncStep.");
                continue;
            }

            try
            {
                // NOTE: If you rename the interface method to SetupAsync, update this call.
                await step.SetupAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw; // bubble up so outer flow cancels cleanly
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // continue to next step instead of aborting whole sequence
            }

            // One-frame buffer so anything started inside the step can schedule
            await Awaitable.NextFrameAsync(ct);
        }

        // 100%
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
    }
    #endregion

    #region Deconstruction
    public async void StartGame()
    {
        ReplaceCts();

        try
        {
            await Awaitable.NextFrameAsync(_cts.Token);
            await Deconstruction(_cts.Token);

            var gameInitializer = GetComponent<GameInitializer>();
            if (gameInitializer != null)
            {
                // If StartGame is async Task, you can await it here.
                gameInitializer.StartGame(_cts.Token);
            }
            else
            {
                Debug.LogWarning($"{nameof(StartInitializer)}: {nameof(GameInitializer)} not found on this GameObject.", this);
            }
        }
        catch (OperationCanceledException) { /* normal on teardown */ }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task Deconstruction(CancellationToken ct)
    {
        int count = _spawnedSteps?.Count ?? 0;
        if (count == 0) return;

        for (int i = count - 1; i >= 0; i--)
        {
            var obj = _spawnedSteps[i];
            if (!obj)
            {
                Debug.LogWarning($"Step {i} instance is null. Skipping.");
                continue;
            }

            Destroy(obj);
            await Awaitable.NextFrameAsync(ct);
        }

        _spawnedSteps.Clear();
        await Awaitable.NextFrameAsync(ct);
    }
    #endregion

    #region Helpers
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
    #endregion
}
