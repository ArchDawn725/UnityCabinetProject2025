using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInitializer : MonoBehaviour
{
    #region References
    [Header("Basics")]
    [SerializeField] private LoadProgress _loadProgressPrefab;
    [SerializeField] private GameObject _directionalLightPrefab;
    [SerializeField] private GameObject _playerPrefab;

    [Header("Steps (Prefabs)")]
    [Tooltip("Each element is a prefab whose root has a component implementing IAsyncStep.")]
    [SerializeField] private List<GameObject> _stepPrefabs = new();

    // Hidden
    public static GameInitializer singleton { get; private set; }
    private CancellationTokenSource _cts;
    private LoadProgress _loadProgress;
    public event Action Ready;

    private void Awake()
    {
        if (singleton != null && singleton != this)
        {
            Debug.LogError($"{nameof(GameInitializer)}: Duplicate instance detected; destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        singleton = this;
    }
    #endregion

    #region Construction

    private void Start()
    {
        // If no StartInitializer is present, we’re responsible for kicking off the flow.
        if (!TryGetComponent<StartInitializer>(out _))
        {
            ReplaceCts();
            _ = RunGameFlowAsync(_cts.Token); // fire-and-forget; errors are caught inside
        }
    }

    /// <summary>
    /// Entry point when called by StartInitializer. Uses the parent-provided token.
    /// </summary>
    public async void StartGame(CancellationToken ct)
    {
        try { await RunGameFlowAsync(ct); }
        catch (OperationCanceledException) { /* normal on teardown */ }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    private async Task RunGameFlowAsync(CancellationToken ct)
    {
        // Let other Awake/OnEnable finish
        await Awaitable.NextFrameAsync(ct);

        await InitializeBasics(ct);
        await InitializeMain(ct);
        await CleanUp(ct);
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
            Debug.LogWarning($"{nameof(GameInitializer)}: LoadProgress prefab not assigned.", this);
        }

        // 2) Main Directional Light (only if none exists)
        if (_directionalLightPrefab)
        {
            var light = FindAnyObjectByType<Light>(FindObjectsInactive.Include);
            bool hasDirectional = light != null && light.type == LightType.Directional;
            if (!hasDirectional)
            {
                Instantiate(_directionalLightPrefab);
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(GameInitializer)}: Directional Light prefab not assigned. Skipping light spawn.", this);
        }

        await Awaitable.NextFrameAsync(ct);
    }

    private async Task InitializeMain(CancellationToken ct)
    {
        int count = _stepPrefabs?.Count ?? 0;

        for (int i = 0; i < count; i++)
        {
            // Visual progress (start-of-step)
            _loadProgress?.SetProgress((float)i / Math.Max(1, count));

            var prefab = _stepPrefabs[i];
            if (!prefab)
            {
                Debug.LogWarning($"Step {i} prefab is null. Skipping.", this);
                continue;
            }

            var instance = Instantiate(prefab);

            if (!instance.TryGetComponent<IAsyncStep>(out var step))
            {
                Debug.LogWarning($"Spawned step '{instance.name}' has no component implementing IAsyncStep.", instance);
                continue;
            }

            try
            {
                // If you rename to SetupAsync, update call site here.
                await step.SetupAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Debug.LogException(ex, instance); }

            await Awaitable.NextFrameAsync(ct);
        }

        // Players are part of “main” init too
        await SpawnPlayers(ct);

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
        Ready?.Invoke();
    }

    private async Task SpawnPlayers(CancellationToken ct)
    {
        if (_playerPrefab == null)
        {
            Debug.LogWarning($"{nameof(GameInitializer)}: Player prefab not assigned; skipping player spawn.", this);
            return;
        }

        // Spawn up to two players based on available gamepads.
        int padCount = Gamepad.all.Count;
        if (padCount == 0)
        {
            Debug.LogWarning("No gamepads connected. Skipping player spawns. (Consider keyboard setup or PlayerInputManager for dynamic join.)", this);
            return;
        }

        // P1
        try
        {
            var p1 = PlayerInput.Instantiate(_playerPrefab, controlScheme: "Gamepad", pairWithDevice: Gamepad.all[0]);
            if (p1.TryGetComponent<IAsyncStep>(out var step1))
            {
                await step1.SetupAsync(ct);
            }
            await Awaitable.NextFrameAsync(ct);
        }
        catch (Exception ex) { Debug.LogException(ex, this); }

        // P2 (only if second pad exists)
        if (padCount >= 2)
        {
            try
            {
                var p2 = PlayerInput.Instantiate(_playerPrefab, controlScheme: "Gamepad", pairWithDevice: Gamepad.all[1]);
                if (p2.TryGetComponent<IAsyncStep>(out var step2))
                {
                    await step2.SetupAsync(ct);
                }
                await Awaitable.NextFrameAsync(ct);
            }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
        else
        {
            Debug.Log("Only one gamepad detected; spawning single player.", this);
        }
    }

    // Optional: hook if you use PlayerInputManager elsewhere
    private static void HandlePlayerJoined(PlayerInput pi)
    {
        var devices = string.Join(", ", pi.devices.Select(d => d.displayName));
        Debug.Log($"Joined: {pi.name} index={pi.playerIndex} devices=[{devices}] map={pi.currentActionMap?.name}");
        pi.neverAutoSwitchControlSchemes = true; // prevent controllers from swapping later
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
        try { cts.Cancel(); } catch { /* ignore */ }
        cts.Dispose();
        cts = null;
    }
    #endregion
}
