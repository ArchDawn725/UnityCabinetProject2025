using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RoomManager : MonoBehaviour, IAsyncStep
{
    #region settings
    [Header("Assets & Prefabs")]
    [SerializeField] private RoomSO[] _roomSOArray;

    [Header("Runtime / Setup")]
    [SerializeField, Min(1)] private int _roomCount = 10;
    [SerializeField] private float _coopSpawnSeparation = 1.5f; // P2 offset if only one spawn

    [Header("Debug / Inspect")]
    [SerializeField] private List<GameObject> _rooms = new(); // instances (disabled until entered)
    [SerializeField] private int _numOfRoomsEntered = -1;
    [SerializeField] private int _enteredFrom;

    // Prepared entries (SO + instance)
    class RoomEntry { public RoomSO so; public GameObject go; public bool used; }
    readonly List<RoomEntry> _deck = new();   // all prebuilt
    readonly List<RoomEntry> _unused = new(); // not yet entered
    RoomEntry _active;                        // current active room

    readonly List<Transform> _players = new(2);

    private EnemySpawner _spawner;
    private Initializer _initializer;

    private readonly List<RoomSO> _lastOptions = new();

    #endregion
    #region Public

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        _initializer = initializer;

        // Build a deck of RoomSOs matching quotas
        var deckSOs = BuildDeck(_roomSOArray, _roomCount);
        _deck.Clear(); _unused.Clear(); _rooms.Clear();

        // Instantiate disabled (pre-spawn), parented to this manager
        for (int i = 0; i < deckSOs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var so = deckSOs[i];
            var prefab = so.roomPrefab;

            var go = Instantiate(prefab, transform);
            go.name = $"{so.name}_#{i}";
            go.SetActive(false);

            var entry = new RoomEntry { so = so, go = go, used = false };
            _deck.Add(entry);
            _unused.Add(entry);
            _rooms.Add(go);

            // spread work over frames
            await Awaitable.NextFrameAsync(ct);
        }

        _spawner = FindAnyObjectByType<EnemySpawner>();
        _initializer.Play += OnPlay;
        _spawner.OnWaveCleared += OnRoundComplete;
    }

    private void OnDestroy()
    {
        if (_initializer != null)
            _initializer.Play -= OnPlay;

        if (_spawner != null)
            _spawner.OnWaveCleared -= OnRoundComplete;
    }

    // Invoked by Initializer.Play event
    private void OnPlay()
    {
        _ = BeginAsync(CancellationToken.None);
    }

    private async Task BeginAsync(CancellationToken ct)
    {
        GatherPlayers();

        // Auto-enter the Starter
        var starter = _unused.FirstOrDefault(e => e.so.type == RoomType.Starter);
        if (starter != null) await EnterNewRoomInternal(starter, ct);
        else if (_unused.Count > 0) await EnterNewRoomInternal(_unused[0], ct);
    }

    public void OnRoundComplete()
    {
        _lastOptions.Clear();
        var options = _unused
            .Where(e => e != _active)
            .OrderBy(_ => Random.value)
            .Take(3)
            .Select(e => e.so);

        _lastOptions.AddRange(options);

        if (_lastOptions.Count == 0)
        {
            Debug.Log("You win!!!");
        }

        List<RoomTransitioner> transitioners =
            new List<RoomTransitioner>(
                Object.FindObjectsByType<RoomTransitioner>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                )
            );

        int value = 0;
        foreach (var door in transitioners)
        {
            if (door.roomDir == _enteredFrom) continue;
            if (options.Count() < value + 1) break;
            door.SetRoomNumber(value);
            value++;
        }
    }

    public async void EnterNewRoom(int id, int enteredFromDir)
    {
        _enteredFrom = GetNeRoomEnteredDir(enteredFromDir);
        RoomSO newRoom = _lastOptions[id];
        using var cts = new CancellationTokenSource();
        try { await EnterNewRoomAsync(newRoom, cts.Token); }
        catch (System.OperationCanceledException) { }
    }

    public async Task EnterNewRoomAsync(RoomSO newRoom, CancellationToken ct)
    {
        var pick = _unused.FirstOrDefault(e => e.so == newRoom);
        if (pick == null)
        {
            Debug.LogWarning($"RoomManager: Requested room {newRoom?.name} not available.", this);
            return;
        }
        await EnterNewRoomInternal(pick, ct);
    }

    private int GetNeRoomEnteredDir(int oldValue)
    {
        switch (oldValue)
        {
            default:
            case 0:
                return 2;
            case 1:
                return 3;
            case 2:
                return 0;
            case 3:
                return 1;
        }
    }

    #endregion
    #region Internal

    private async Task EnterNewRoomInternal(RoomEntry entry, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_active != null && _active.go)
        {
            _active.used = true;
            _unused.Remove(_active);
            _rooms.Remove(_active.go);
            Destroy(_active.go);
            _active.go = null;
        }

        _active = entry;
        if (_active.go) _active.go.SetActive(true);

        // Make sure we still have up to two player references (handles join/leave mid-run)
        GatherPlayers();

        // Move P1/P2 to spawn points (or fallback)
        MovePlayersToSpawns(_active.go);

        // Difficulty scales with rooms entered
        _numOfRoomsEntered++;

        // Start spawning enemies for this room
        StartSpawningForRoom(_active, _numOfRoomsEntered, _active.go.transform);

        // Give one frame for physics/AI to settle
        await Awaitable.NextFrameAsync(ct);
    }

    private void StartSpawningForRoom(RoomEntry e, int difficulty, Transform room)
    {
        _spawner?.StartSpawn(e.so, difficulty, room);
    }
    #endregion
    #region Players
    private void GatherPlayers()
    {
        _players.Clear();

        foreach (var pi in PlayerRegistry.Players)
        {
            if (pi && pi.transform) _players.Add(pi.transform);
            if (_players.Count == 2) break;
        }
    }

    private void MovePlayersToSpawns(GameObject roomGO)
    {
        if (!roomGO) return;

        // Try dedicated spawns first
        var s1 = FindChild(roomGO.transform, "Player1Spawn") ?? FindChild(roomGO.transform, "PlayerSpawn1");
        var s2 = FindChild(roomGO.transform, "Player2Spawn") ?? FindChild(roomGO.transform, "PlayerSpawn2");

        // Single generic spawn
        var any = FindChild(roomGO.transform, "PlayerSpawn");

        // P1
        if (_players.Count >= 1 && _players[0])
        {
            if (s1) _players[0].position = s1.position;
            else if (any) _players[0].position = any.position;
            else _players[0].position = roomGO.transform.position;
        }

        // P2
        if (_players.Count >= 2 && _players[1])
        {
            if (s2) _players[1].position = s2.position;
            else if (any)
                _players[1].position = any.position + Vector3.right * _coopSpawnSeparation;
            else
                _players[1].position = roomGO.transform.position + Vector3.right * _coopSpawnSeparation;
        }
    }

    private static Transform FindChild(Transform parent, string name) => parent ? parent.Find(name) : null;
    #endregion
    #region Deck building 

    // Build a list of RoomSOs: 1 Starter, 1 Shop, 1 Boss, 2 Elite, rest Standard
    static List<RoomSO> BuildDeck(RoomSO[] all, int count)
    {
        var starters = all.Where(s => s.type == RoomType.Starter).ToList();
        var shops = all.Where(s => s.type == RoomType.Shop).ToList();
        var bosses = all.Where(s => s.type == RoomType.Boss).ToList();
        var elites = all.Where(s => s.type == RoomType.Elite).ToList();
        var standards = all.Where(s => s.type == RoomType.Standard).ToList();

        var deck = new List<RoomSO>(count);

        void PickOne(List<RoomSO> src)
        {
            if (src.Count > 0) deck.Add(src[Random.Range(0, src.Count)]);
        }

        if (deck.Count < count) PickOne(starters);
        if (deck.Count < count) PickOne(shops);
        if (deck.Count < count) PickOne(bosses);

        for (int i = 0; i < 2 && elites.Count > 0 && deck.Count < count; i++)
            deck.Add(elites[Random.Range(0, elites.Count)]);

        while (deck.Count < count)
        {
            if (standards.Count > 0) deck.Add(standards[Random.Range(0, standards.Count)]);
            else if (elites.Count > 0) deck.Add(elites[Random.Range(0, elites.Count)]);
            else deck.Add(all[Random.Range(0, all.Length)]);
        }

        // Shuffle
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck.Take(count).ToList();
    }
    #endregion
}
