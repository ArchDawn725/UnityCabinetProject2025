using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StartSceneManager : MonoBehaviour, IAsyncStep
{
    [Header("Buttons")]
    [SerializeField] private Button _beginButton;
    [SerializeField] private Button _readyButton; // TODO: wire up 2P flow later

    [Header("Screens")]
    [SerializeField] private GameObject _startScreen;
    [SerializeField] private GameObject _characterSelectScreen;

    private bool _initialized;
    private bool _listenersAttached;

    /// <summary>
    /// Preferred async setup entry point (matches updated IAsyncStep).
    /// Hides the UI until StartInitializer signals Ready, then shows it.
    /// </summary>
    public async Task SetupAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        _initialized = true;

        Hide(); // stay hidden until Ready is raised

        // Subscribe to Ready (if available). We’ll detach after it fires or on destroy.
        if (StartInitializer.singleton != null)
        {
            StartInitializer.singleton.Ready += HandleReady;
        }
        else
        {
            Debug.LogWarning($"{nameof(StartSceneManager)}: StartInitializer.singleton is null; UI may never be shown.", this);
        }

        // Button listeners (guarded, with warnings if missing)
        if (_beginButton != null) _beginButton.onClick.AddListener(OnBegin);
        else Debug.LogWarning($"{nameof(StartSceneManager)}: Begin button not assigned.", this);

        if (_readyButton != null) _readyButton.onClick.AddListener(OnReady);
        else Debug.LogWarning($"{nameof(StartSceneManager)}: Ready button not assigned.", this);

        _listenersAttached = true;

        // Give other systems a frame to finish their own setup.
        if (!cancellationToken.IsCancellationRequested)
        {
            await Awaitable.NextFrameAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Back-compat with the original interface signature. Prefer <see cref="SetupAsync"/>.
    /// </summary>
    [System.Obsolete("Use SetupAsync(CancellationToken) instead.")]
    public Task SetUp(CancellationToken ct) => SetupAsync(ct);

    public void Hide() => gameObject.SetActive(false);
    private void Show() => gameObject.SetActive(true);

    private void HandleReady()
    {
        Show();

        // One-shot: detach after the first Ready event.
        if (StartInitializer.singleton != null)
        {
            StartInitializer.singleton.Ready -= HandleReady;
        }
    }

    private void OnBegin()
    {
        if (_startScreen) _startScreen.SetActive(false);
        if (_characterSelectScreen) _characterSelectScreen.SetActive(true);
    }

    private void OnReady()
    {
        // TODO: when adding 2-player support, gate StartGame() behind both players ready.
        if (StartInitializer.singleton != null)
        {
            StartInitializer.singleton.StartGame();
        }
        else
        {
            Debug.LogWarning($"{nameof(StartSceneManager)}: StartInitializer.singleton is null; cannot start game.", this);
        }
    }

    private void OnDestroy()
    {
        // Remove button listeners
        if (_listenersAttached)
        {
            if (_beginButton != null) _beginButton.onClick.RemoveListener(OnBegin);
            if (_readyButton != null) _readyButton.onClick.RemoveListener(OnReady);
            _listenersAttached = false;
        }

        // Ensure we’re not left subscribed to Ready
        if (StartInitializer.singleton != null)
        {
            StartInitializer.singleton.Ready -= HandleReady;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Non-destructive best-effort wiring for convenience in the editor.
        if (_beginButton == null || _readyButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (_beginButton == null && buttons.Length > 0) _beginButton = buttons[0];
            if (_readyButton == null && buttons.Length > 1) _readyButton = buttons[1];
        }
    }
#endif
}
