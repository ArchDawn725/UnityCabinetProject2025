using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LoadProgress : MonoBehaviour, IAsyncStep
{
    [Header("References")]
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TextMeshProUGUI _percentLabel; 
    [SerializeField] private CanvasGroup _canvasGroup;    

    [Header("Animation")]
    [Tooltip("Seconds to animate progress from 0 → 1. Scales by delta size.")]
    [SerializeField, Min(0f)] private float _secondsPerUnit = 0.35f;
    [SerializeField] private AnimationCurve _ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool _useUnscaledTime = true;
    [Tooltip("If true, progress never moves backwards.")]
    [SerializeField] private bool _onlyIncrease = true;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

    private Coroutine _progressCo;
    private Coroutine _fadeCo;
    private float _target;

    /// <summary>Current slider value (0..1), or 0 if missing.</summary>
    public float Value => _progressBar ? _progressBar.value : 0f;

    /// <summary>Last requested progress (0..1).</summary>
    public float Target => _target;

    private void Reset()
    {
        _progressBar = GetComponentInChildren<Slider>(true);
        _canvasGroup = GetComponent<CanvasGroup>();
        _percentLabel = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        EnsureRefs();

        if (!_progressBar)
        {
            Debug.LogError($"{nameof(LoadProgress)} requires a {nameof(Slider)} somewhere in children. Disabling.", this);
            enabled = false;
            return;
        }

        _progressBar.value = 0f;
        UpdateLabel(0f);

        Show(animated: true);
        await Awaitable.NextFrameAsync(ct);
    }

    private void EnsureRefs()
    {
        if (!_progressBar) _progressBar = GetComponentInChildren<Slider>(true);
        if (!_canvasGroup) _canvasGroup = GetComponent<CanvasGroup>();
        if (!_percentLabel) _percentLabel = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>Convenience wrapper to toggle visibility.</summary>
    public void SetVisible(bool visible, bool animated = true)
    {
        if (visible) Show(animated);
        else Hide(animated);
    }

    public void Show(bool animated = true)
    {
        gameObject.SetActive(true);
        if (_canvasGroup)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeTo(1f, animated ? _fadeDuration : 0f));
        }
    }

    public void Hide(bool animated = true)
    {
        if (_canvasGroup)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeTo(0f, animated ? _fadeDuration : 0f, () =>
            {
                gameObject.SetActive(false); // deactivate after fade
            }));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Sets progress [0..1]. If animated, smoothly tweens the slider value.
    /// Respects <see cref="_onlyIncrease"/>.
    /// </summary>
    public void SetProgress(float value, bool animated = true)
    {
        EnsureRefs();
        if (!_progressBar) return;

        value = Mathf.Clamp01(value);
        if (_onlyIncrease) value = Mathf.Max(value, _target);
        _target = value;

        if (!animated || _secondsPerUnit <= 0f)
        {
            TryStopProgress();
            _progressBar.value = _target;
            UpdateLabel(_target);
            return;
        }

        TryStopProgress();
        _progressCo = StartCoroutine(AnimateProgress(_progressBar.value, _target));
    }

    private IEnumerator AnimateProgress(float from, float to)
    {
        float duration = Mathf.Max(0.0001f, Mathf.Abs(to - from) * _secondsPerUnit);
        float t = 0f;

        while (t < duration)
        {
            t += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = _ease.Evaluate(u);
            float v = Mathf.Lerp(from, to, eased); // clamped (avoid overshoot if curve > 1)
            _progressBar.value = v;
            UpdateLabel(v);
            yield return null;
        }

        _progressBar.value = to;
        UpdateLabel(to);
        _progressCo = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, System.Action onComplete = null)
    {
        if (!_canvasGroup) yield break;

        float start = _canvasGroup.alpha;
        float t = 0f;

        // Only force-activate if we are fading IN (so the fade is visible).
        if (targetAlpha > start && !gameObject.activeSelf)
            gameObject.SetActive(true);

        while (t < duration)
        {
            t += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            _canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, _ease.Evaluate(u));
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        onComplete?.Invoke();
        _fadeCo = null;
    }

    private void UpdateLabel(float normalized)
    {
        if (_percentLabel)
        {
            int pct = Mathf.RoundToInt(normalized * 100f);
            _percentLabel.text = pct + "%";
        }
    }

    private void OnDisable()
    {
        TryStopProgress();
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
    }

    private void TryStopProgress()
    {
        if (_progressCo != null) { StopCoroutine(_progressCo); _progressCo = null; }
    }
}
