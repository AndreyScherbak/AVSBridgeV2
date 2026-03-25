using System.Collections.Concurrent;
using AVSBridge.Shared.Models;

namespace AVSBridge.Server.Services;

/// <summary>
/// Thread-safe in-memory manager for game rooms and player connections.
/// Each room holds a GameState and a per-room lock for serializing game actions.
/// </summary>
public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, PlayerConnection> _connections = new();

    /// <summary>Tracks which room and player a SignalR connection belongs to.</summary>
    public sealed record PlayerConnection(string RoomCode, string PlayerId);

    /// <summary>
    /// Wraps a GameState with a per-room lock and connection tracking.
    /// </summary>
    public sealed class GameRoom
    {
        public GameState State { get; }
        public SemaphoreSlim Lock { get; } = new(1, 1);

        /// <summary>PlayerId → ConnectionId mapping for this room.</summary>
        public ConcurrentDictionary<string, string> PlayerConnections { get; } = new();

        public GameRoom(GameState state) => State = state;
    }

    /// <summary>
    /// Create a new room with the first player. Returns the room and the player's ID.
    /// Does NOT modify GameState players — caller must do that under the room lock if needed.
    /// </summary>
    public (GameRoom Room, string PlayerId) CreateRoom(string connectionId, string playerName, bool extended = false)
    {
        var roomCode = GenerateRoomCode();
        var playerId = GeneratePlayerId();

        var state = new GameState { RoomCode = roomCode, IsExtendedMode = extended };
        state.Players.Add(new Player(playerId, playerName));

        var room = new GameRoom(state);
        room.PlayerConnections[playerId] = connectionId;

        _rooms[roomCode] = room;
        _connections[connectionId] = new PlayerConnection(roomCode, playerId);

        return (room, playerId);
    }

    /// <summary>
    /// Add a player to an existing room. Returns null if the room doesn't exist,
    /// is full, or the game has already started.
    /// </summary>
    public (GameRoom Room, string PlayerId)? JoinRoom(string connectionId, string roomCode, string playerName)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return null;

        if (room.State.Phase != GamePhase.WaitingForPlayers)
            return null;

        if (room.State.Players.Count >= 10)
            return null;

        var playerId = GeneratePlayerId();
        room.State.Players.Add(new Player(playerId, playerName));
        room.PlayerConnections[playerId] = connectionId;
        _connections[connectionId] = new PlayerConnection(roomCode, playerId);

        return (room, playerId);
    }

    public GameRoom? GetRoom(string roomCode)
    {
        _rooms.TryGetValue(roomCode, out var room);
        return room;
    }

    public PlayerConnection? GetConnection(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var conn);
        return conn;
    }

    public string? GetConnectionId(string roomCode, string playerId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return null;
        room.PlayerConnections.TryGetValue(playerId, out var connId);
        return connId;
    }

    /// <summary>
    /// Remove a connection. If the room becomes empty, removes the room too.
    /// Returns the PlayerConnection if found, null otherwise.
    /// </summary>
    public PlayerConnection? RemoveConnection(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var conn))
            return null;

        if (_rooms.TryGetValue(conn.RoomCode, out var room))
        {
            room.PlayerConnections.TryRemove(conn.PlayerId, out _);

            if (room.PlayerConnections.IsEmpty)
                _rooms.TryRemove(conn.RoomCode, out _);
        }

        return conn;
    }

    /// <summary>For testing: get total active room count.</summary>
    public int RoomCount => _rooms.Count;

    private static string GenerateRoomCode()
    {
        // Exclude I, O, 0, 1 to avoid visual confusion
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(6, chars, static (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = chars[Random.Shared.Next(chars.Length)];
        });
    }

    private static string GeneratePlayerId() => Guid.NewGuid().ToString("N")[..8];
}
