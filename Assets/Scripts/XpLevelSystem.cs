using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class XpLevelSystem : MonoBehaviour, IAsyncStep
{
    public static XpLevelSystem Instance { get; private set; }

    [Header("UI")]
    [SerializeField] Slider xpSlider;
    [SerializeField] bool sliderWholeNumbers = false; // set true if you want integers only

    [Header("Progression")]
    [SerializeField, Min(0f)] float startThreshold = 10f;     // first level requirement
    [SerializeField, Min(0f)] float thresholdIncrement = 10f; // added per level
    [SerializeField, Min(0f)] float baseXpPerKill = 2f;       // divided by player count

    [Header("State (read-only at runtime)")]
    [SerializeField] int level = 0;
    [SerializeField] float xp = 0f;

    public UnityEvent<int> onLevelUp; // passes new level

    float _currentThreshold;

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _currentThreshold = Mathf.Max(0.01f, startThreshold);
        if (xpSlider)
        {
            xpSlider.wholeNumbers = sliderWholeNumbers;
            xpSlider.maxValue = _currentThreshold;
            xpSlider.value = Mathf.Clamp(xp, 0f, _currentThreshold);
        }
    }

    /// <summary>Called by Enemy when it dies.</summary>
    public void AwardEnemyKill()
    {
        int players = GetAlivePlayerCount();
        //float award = (players <= 0) ? baseXpPerKill : baseXpPerKill / players;
        float award = baseXpPerKill;
        AddXp(award);
    }

    public void AddXp(float amount)
    {
        if (amount <= 0f) return;

        xp += amount;

        bool leveled = false;
        while (xp >= _currentThreshold)
        {
            xp -= _currentThreshold;    // keep overflow
            level++;
            _currentThreshold += thresholdIncrement;
            leveled = true;
        }

        if (xpSlider)
        {
            xpSlider.maxValue = _currentThreshold;
            xpSlider.value = Mathf.Clamp(xp, 0f, _currentThreshold);
        }

        if (leveled) { onLevelUp?.Invoke(level); }
    }

    int GetAlivePlayerCount()
    {
        // Simple: count Player components in scene (active only)
        // If you built a PlayerRegistry earlier, swap this for PlayerRegistry.Players.Count
        var players = FindObjectsOfType<Player>(includeInactive: false);
        return Mathf.Max(1, players.Length);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && xpSlider)
        {
            float previewThreshold = Mathf.Max(0.01f, startThreshold + thresholdIncrement * level);
            xpSlider.wholeNumbers = sliderWholeNumbers;
            xpSlider.maxValue = previewThreshold;
            xpSlider.value = Mathf.Clamp(xp, 0f, previewThreshold);
        }
    }
#endif
}
