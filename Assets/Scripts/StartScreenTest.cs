using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenTest : MonoBehaviour, IAsyncStep
{
    //this script does too much, will adjust later
    public static StartScreenTest Singleton;

    [Header("Player 1 UI")]
    [SerializeField] private GameObject[] _player1Screens = new GameObject[2];
    [SerializeField] private Transform _p1UiRoot;
    [SerializeField] private Button _p1StartButton;
    [SerializeField] private MultiplayerEventSystem _p1ES;


    [Header("Player 2 UI")]
    [SerializeField] private GameObject[] _player2Screens = new GameObject[2];
    [SerializeField] private Transform _p2UiRoot;
    [SerializeField] private Button _p2StartButton;
    [SerializeField] private MultiplayerEventSystem _p2ES;

    [Header("Testing")]
    public List<Player> players = new List<Player>();
    CinemachineTargetGroup targetGroup;
    private Initializer _initializer;
    [SerializeField] private Button _gameOverButton;

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        Singleton = this;
        _initializer = initializer;

        // Initial state: show Start screens, hide Character Select
        SetScreens(_player1Screens, 0);
        SetScreens(_player2Screens, 0);

        _p1StartButton.onClick.AddListener(() => ClassChosen(0));
        _p2StartButton.onClick.AddListener(() => ClassChosen(1));

        PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;

        if (_p1ES) _p1ES.playerRoot = _p1UiRoot ? _p1UiRoot.gameObject : null;
        if (_p2ES) _p2ES.playerRoot = _p2UiRoot ? _p2UiRoot.gameObject : null;

        // If players already exist (e.g., spawned before this enabled), pair them now
        foreach (var pi in FindObjectsOfType<PlayerInput>()) { OnPlayerJoined(pi); Debug.Log("PI found"); }
        _initializer.Ready += OnBegin;
        transform.GetComponent<TeamDownWatcher>().onTeamWipe.AddListener(OnTeamWipe);
        await Awaitable.NextFrameAsync(ct);
    }
    private void OnBegin()
    {
        targetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
    }

    void OnDisable()
    {
        PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    public void OnPlayerJoined(PlayerInput pi)
    {
        Debug.Log($"Player joined: {pi.playerIndex} ({pi.devices.Count} devices)");
        var idx = pi.playerIndex;

        // Pair THIS player's devices to THEIR UI module (CRITICAL)
        var es = idx == 0 ? _p1ES : _p2ES;
        if (es == null)
        {
            Debug.LogWarning($"No MultiplayerEventSystem assigned for player {idx + 1}");
            return;
        }
        var ui = es.GetComponent<InputSystemUIInputModule>();
        if (ui == null)
        {
            Debug.LogError($"MultiplayerEventSystem for player {idx + 1} has no InputSystemUIInputModule");
            return;
        }

        pi.uiInputModule = ui;                 // device isolation for UI
        pi.neverAutoSwitchControlSchemes = true;

        // Toggle screens
        if (idx == 0)
        {
            SetScreens(_player1Screens, 1);
            StartCoroutine(NextFrameSelect(es, _p1StartButton));
        }
        else if (idx == 1)
        {
            SetScreens(_player2Screens, 1);
            StartCoroutine(NextFrameSelect(es, _p2StartButton));
        }

        targetGroup.AddMember(pi.gameObject.transform, 1, 0.5f);
    }

    // --- Helpers ---

    private void SetScreens(GameObject[] screens, int val)
    {
        for (int i = 0; i < screens.Length; i++)
            screens[i].SetActive(i == val);
    }

    private void SetSelected(Selectable sel, MultiplayerEventSystem es)
    {
        es.SetSelectedGameObject(sel.gameObject);
    }

    private static IEnumerator NextFrameSelect(MultiplayerEventSystem es, Selectable sel)
    {
        if (!es || !sel) yield break;
        // Maximize-on-Play can clear selection; set it next frame
        yield return null;
        es.SetSelectedGameObject(sel.gameObject);
    }

    private void ClassChosen(int player)
    {
        players[player].Setup();
        SetScreens(player == 0 ? _player1Screens : _player2Screens, -1);
        _initializer?.Begin();
    }
    public void PlayerDeath(Player player)
    {
        targetGroup.RemoveMember(player.transform);
        foreach (var p in players)
            if (p && p.gameObject.activeInHierarchy)
                return; // still alive
        Gameover();
    }
    public void Gameover()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
    private void OnTeamWipe()
    {
        SetScreens(_player1Screens, _player1Screens.Length - 1);
        Time.timeScale = 0f;
        StartCoroutine(NextFrameSelect(_p1ES, _gameOverButton));
    }
}
