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

    [Header("State (read-only at runtime)")]
    [SerializeField] int level = 0;
    [SerializeField] int total;

    public UnityEvent<int> onLevelUp; // passes new level
    private Initializer initializer;

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        this.initializer = initializer;
        initializer.Ready += OnStartUp;
        await Awaitable.NextFrameAsync(ct);
    }
    public void AwardEnemyKill()
    {
        xpSlider.value += 1;
    }

    public void SetTotal(int newTotal)
    {
        total = newTotal;
        xpSlider.maxValue = newTotal;
    }

    private void OnStartUp()
    {
        EnemySpawner.Singleton.OnWaveCleared += LevelUp;
    }

    private void OnDestroy()
    {
        if (EnemySpawner.Singleton != null)
        {
            EnemySpawner.Singleton.OnWaveCleared -= LevelUp;
        }
        initializer.Ready -= OnStartUp;
    }

    public void LevelUp() {xpSlider.value = 0; onLevelUp?.Invoke(level); }
}
