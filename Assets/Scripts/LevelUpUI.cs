using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour, IAsyncStep
{
    // ---- Choices ----
    public enum UpgradeChoice { 
        Survivor, Speedster, 
        Machinegunner, HigherCaliber, Sniper,
        Swordmaster, SweepingEdge, SharperBlade,
        BiggerBlast, FasterBolts, StrongerBolts
    }

    [Serializable]
    public struct PlayerPanel
    {
        [Header("Panel & Focus")]
        public GameObject root;                 // Player's Level-Up panel (already in scene; default inactive)
        public Selectable firstSelectable;      // First button to focus
        public MultiplayerEventSystem eventSystem; // Optional: that player's ES for isolated control

        [Header("Options (same length arrays)")]
        public Button[] optionButtons;
        public TextMeshProUGUI[] optionTitles;       
        public TextMeshProUGUI[] optionDescriptions;

        [Header("Target Player")]
        public IPlayer player;

        [Header("Upgrade Pool")]
        public UpgradeChoice[] pool;
    }

    [Header("Panels (index 0 = P1, 1 = P2)")]
    [SerializeField] private PlayerPanel[] panels;

    [Header("Choices")]
    [SerializeField, Min(1)] private int choicesPerPlayer = 3;

    [Header("Input Maps (optional)")]
    [SerializeField] private string gameplayMap = "Gameplay";
    [SerializeField] private string uiMap = "UI";

    [Header("Gate")]
    [SerializeField] private bool waitForAllPanelsWithPlayers = false; // wait until all panel slots have a player
    Dictionary<PlayerInput, string> _previousMaps = new();

    bool _open;
    float _savedTimeScale;
    int _awaiting;
    int _pendingLevelUps;
    int _lastLevel;

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        if (XpLevelSystem.Instance)
            XpLevelSystem.Instance.onLevelUp.AddListener(HandleLevelUp);

        if (PlayerInputManager.instance)
            PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
        await Awaitable.NextFrameAsync(ct);
    }

    void OnDisable()
    {
        if (XpLevelSystem.Instance)
            XpLevelSystem.Instance.onLevelUp.RemoveListener(HandleLevelUp);

        if (PlayerInputManager.instance)
            PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput pi)
    {
        var p = pi.GetComponent<IPlayer>();
        if (p == null) return;

        int idx = pi.playerIndex;
        if (idx >= 0 && idx < panels.Length)
        {
            var pp = panels[idx];
            pp.player = p;
            switch (p.PType)
            {
                case "Gunslinger":
                    pp.pool = new UpgradeChoice[5];
                    pp.pool[0] = UpgradeChoice.Survivor;
                    pp.pool[1] = UpgradeChoice.Speedster;
                    pp.pool[2] = UpgradeChoice.Machinegunner;
                    pp.pool[3] = UpgradeChoice.HigherCaliber;
                    pp.pool[4] = UpgradeChoice.Sniper;
                    break;
                case "Samurai":
                    pp.pool = new UpgradeChoice[5];
                    pp.pool[0] = UpgradeChoice.Survivor;
                    pp.pool[1] = UpgradeChoice.Speedster;
                    pp.pool[2] = UpgradeChoice.Swordmaster;
                    pp.pool[3] = UpgradeChoice.SweepingEdge;
                    pp.pool[4] = UpgradeChoice.SharperBlade;
                    break;
                case "Wizard":
                    pp.pool = new UpgradeChoice[5];
                    pp.pool[0] = UpgradeChoice.Survivor;
                    pp.pool[1] = UpgradeChoice.Speedster;
                    pp.pool[2] = UpgradeChoice.BiggerBlast;
                    pp.pool[3] = UpgradeChoice.FasterBolts;
                    pp.pool[4] = UpgradeChoice.StrongerBolts;
                    break;
            }
            panels[idx] = pp;
        }

        // If a level-up was queued, try to open now
        if (!_open && _pendingLevelUps > 0)
        {
            if (!waitForAllPanelsWithPlayers || PanelsWithPlayersCount() == TargetPanelsCount())
            {
                _pendingLevelUps--;
                HandleLevelUp(_lastLevel);
            }
        }
    }

    public void HandleLevelUp(int newLevel)
    {
        Debug.Log($"[LevelUpUI] Level up to {newLevel}!");
        if (_open) return; 
        Debug.Log("[LevelUpUI] Opening level-up UI.");

        EnsurePanelPlayers(); // map any already-spawned players
        Debug.Log($"[LevelUpUI] Panels with players: {PanelsWithPlayersCount()}/{TargetPanelsCount()}");

        int present = PanelsWithPlayersCount();
        int target = TargetPanelsCount();

        if (present == 0)
        {
            _pendingLevelUps++;
            _lastLevel = newLevel;
            Debug.Log("[LevelUpUI] No players present yet; queuing level-up.");
            return;
        }

        if (waitForAllPanelsWithPlayers && present < target)
        {
            _pendingLevelUps++;
            _lastLevel = newLevel;
            Debug.Log($"[LevelUpUI] Waiting for all players. Present={present}/{target}. Queued.");
            return;
        }

        PauseGame();

        _awaiting = 0;
        for (int i = 0; i < panels.Length; i++)
        {
            Debug.Log($"[LevelUpUI] Processing panel {i}...");
            int panelIndex = i; // <-- capture index to avoid closure bug
            var pp = panels[panelIndex];
            if (pp.player == null || pp.root == null) continue;

            _awaiting++;

            var choices = RollChoices(panelIndex);
            WirePanel(panelIndex, pp, choices);

            pp.root.SetActive(true);
            if (pp.eventSystem && pp.firstSelectable)
                pp.eventSystem.SetSelectedGameObject(pp.firstSelectable.gameObject);

            panels[panelIndex] = pp; // write back (struct)
        }

        _open = true;
    }

    void OnPlayerChose(int panelIndex, UpgradeChoice choice)
    {
        if (panelIndex < 0 || panelIndex >= panels.Length) { Debug.LogError($"[LevelUpUI] panelIndex {panelIndex} OOR."); return; }
        var pp = panels[panelIndex];

        Debug.Log($"[LevelUpUI] Player in panel {panelIndex} chose {choice}");

        if (pp.player != null)
        {
            try { pp.player.ApplyUpgrade(choice); }
            catch (Exception e) { Debug.LogException(e, pp.player.GameObject); }
        }

        CleanupPanel(pp);
        if (pp.root) pp.root.SetActive(false);
        panels[panelIndex] = pp;

        _awaiting--;
        if (_awaiting <= 0) ResumeGame();
    }

    // -------- Wiring & helpers --------

    void WirePanel(int panelIndex, PlayerPanel pp, UpgradeChoice[] choices)
    {
        for (int i = 0; i < pp.optionButtons.Length; i++)
        {
            var btn = pp.optionButtons[i];
            if (!btn) continue;

            bool active = i < choices.Length;
            btn.gameObject.SetActive(active);
            btn.onClick.RemoveAllListeners();

            if (!active) continue;

            if (i < pp.optionTitles.Length && pp.optionTitles[i])
                pp.optionTitles[i].text = Title(choices[i]);
            if (i < pp.optionDescriptions.Length && pp.optionDescriptions[i])
                pp.optionDescriptions[i].text = Description(choices[i]);

            var choice = choices[i]; // capture per-button
            btn.onClick.AddListener(() => OnPlayerChose(panelIndex, choice));
        }
    }

    void CleanupPanel(PlayerPanel pp)
    {
        foreach (var b in pp.optionButtons)
            if (b) b.onClick.RemoveAllListeners();
    }

    void EnsurePanelPlayers()
    {
        //var players = FindObjectsOfType<IPlayer>(includeInactive: false);
        var players = FindObjectsOfType<MonoBehaviour>(true).OfType<IPlayer>().ToArray();
        foreach (var p in players)
        {
            var pi = p.PInput;
            if (!pi) continue;
            int idx = pi.playerIndex;
            if (idx >= 0 && idx < panels.Length && panels[idx].player == null)
            {
                var pp = panels[idx];
                pp.player = p;
                panels[idx] = pp;
            }
        }
    }

    int PanelsWithPlayersCount() => panels.Count(x => x.player != null);
    int TargetPanelsCount() => panels.Length;

    UpgradeChoice[] RollChoices(int idx)
    {
        var list = panels[idx].pool.ToArray();
        for (int i = 0; i < list.Length; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Length);
            (list[i], list[j]) = (list[j], list[i]);
        }
        int take = Mathf.Clamp(choicesPerPlayer, 1, list.Length);
        return list.Take(take).ToArray();
    }

    void PauseGame()
    {
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        _previousMaps.Clear();

        foreach (var pi in FindObjectsOfType<PlayerInput>())
        {
            // remember exact map name (could be "Gameplay", "InGame", etc.)
            var current = pi.currentActionMap != null ? pi.currentActionMap.name : null;
            _previousMaps[pi] = current;

            // switch to UI if it exists
            SafeSwitchMap(pi, uiMap);
        }
    }

    void ResumeGame()
    {
        Time.timeScale = _savedTimeScale;

        // hide panels & clear listeners
        foreach (var pp in panels)
        {
            if (pp.root) pp.root.SetActive(false);
            CleanupPanel(pp);
            if (pp.eventSystem) pp.eventSystem.SetSelectedGameObject(null);
        }

        // restore each player's map exactly to what they had
        foreach (var kvp in _previousMaps)
        {
            var pi = kvp.Key;
            var prev = kvp.Value;

            if (!string.IsNullOrEmpty(prev) && pi.actions?.FindActionMap(prev) != null)
            {
                pi.SwitchCurrentActionMap(prev);
            }
            else
            {
                // fallback: try your configured gameplay map
                SafeSwitchMap(pi, gameplayMap);
            }
        }
        _previousMaps.Clear();

        _open = false;
    }

    void SafeSwitchMap(PlayerInput pi, string mapName)
    {
        Debug.Log($"[LevelUpUI] Switching PlayerInput (index {pi.playerIndex}) to map '{mapName}'");
        if (string.IsNullOrEmpty(mapName)) return;
        var map = pi.actions?.FindActionMap(mapName);
        if (map != null) pi.SwitchCurrentActionMap(map.name);
    }

    string Title(UpgradeChoice c) => c switch
    {
        UpgradeChoice.Survivor => "Survivor",
        UpgradeChoice.Speedster => "Speedster",
        UpgradeChoice.Machinegunner => "Machine Gunner",
        UpgradeChoice.HigherCaliber => "Higher Caliber",
        UpgradeChoice.Sniper => "Sniper",
        UpgradeChoice.Swordmaster => "Swordmaster",
        UpgradeChoice.SweepingEdge => "Sweeping Edge",
        UpgradeChoice.SharperBlade => "Sharper Blade",
        UpgradeChoice.BiggerBlast => "Bigger Blast",
        UpgradeChoice.FasterBolts => "Faster Bolts",
        UpgradeChoice.StrongerBolts => "Stronger Bolts",
        _ => c.ToString()
    };

    string Description(UpgradeChoice c) => c switch
    {
        UpgradeChoice.Survivor => "Increases health and health regen",
        UpgradeChoice.Speedster => "Increases movement and revival speed",
        UpgradeChoice.Machinegunner => "Increases fire rate and bullet speed",
        UpgradeChoice.HigherCaliber => "Increases damage and pierce",
        UpgradeChoice.Sniper => "Increases range and bullet life",
        UpgradeChoice.Swordmaster => "Increases sword speed",
        UpgradeChoice.SweepingEdge => "Increases sword slashing area",
        UpgradeChoice.SharperBlade => "Increases sword damage",
        UpgradeChoice.BiggerBlast => "Increases fireball blast radius and damage",
        UpgradeChoice.FasterBolts => "Increase magic bolt speed",
        UpgradeChoice.StrongerBolts => "Increases magic bolt damage",
        _ => ""
    };
}
