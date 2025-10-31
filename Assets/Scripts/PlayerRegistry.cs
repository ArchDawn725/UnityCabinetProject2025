using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public static class PlayerRegistry
{
    public static event Action<PlayerInput> Added;
    public static event Action<PlayerInput> Removed;

    static readonly List<PlayerInput> _players = new();

    public static IReadOnlyList<PlayerInput> Players => _players;

    public static void Add(PlayerInput pi)
    {
        if (pi && !_players.Contains(pi))
        {
            _players.Add(pi);
            Added?.Invoke(pi);
        }
    }

    public static void Remove(PlayerInput pi)
    {
        if (pi && _players.Remove(pi))
            Removed?.Invoke(pi);
    }
}
