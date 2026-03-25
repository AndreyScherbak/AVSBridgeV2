using AVSBridge.Server.Services;
using AVSBridge.Shared.DTOs;
using AVSBridge.Shared.Engine;
using AVSBridge.Shared.Events;
using AVSBridge.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace AVSBridge.Server.Hubs;

/// <summary>
/// SignalR hub for real-time game communication.
/// All game state mutations are serialized per-room via a SemaphoreSlim.
/// After each action, every player receives a personalized GameStateDto
/// (their own hand only) plus broadcast game events.
/// </summary>
public sealed class GameHub : Hub
{
    private readonly RoomManager _roomManager;
    private readonly GameEngine _engine;

    public GameHub(RoomManager roomManager, GameEngine engine)
    {
        _roomManager = roomManager;
        _engine = engine;
    }

    // ═══════════════════════════════════════════════
    //  Client → Server
    // ═══════════════════════════════════════════════

    /// <summary>Create a new room. Returns the room code to the caller.</summary>
    public async Task<string> CreateRoom(string playerName, bool extended = false)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new HubException("Player name is required.");

        var (room, playerId) = _roomManager.CreateRoom(Context.ConnectionId, playerName, extended);
        var roomCode = room.State.RoomCode;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Caller.SendAsync("RoomJoined", roomCode, playerId);
        await BroadcastGameState(room.State);

        return roomCode;
    }

    /// <summary>Join an existing room by code.</summary>
    public async Task JoinRoom(string roomCode, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new HubException("Player name is required.");

        roomCode = roomCode.Trim().ToUpperInvariant();

        var result = _roomManager.JoinRoom(Context.ConnectionId, roomCode, playerName);
        if (result is null)
        {
            await Clients.Caller.SendAsync("Error",
                "Unable to join room. It may not exist, be full, or already in progress.");
            return;
        }

        var (room, playerId) = result.Value;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Caller.SendAsync("RoomJoined", roomCode, playerId);
        await Clients.Group(roomCode).SendAsync("PlayerJoined", playerName);
        await BroadcastGameState(room.State);
    }

    /// <summary>Host starts the game (deals cards).</summary>
    public async Task StartGame(string roomCode)
    {
        await ExecuteGameAction(roomCode, state => _engine.DealCards(state));
    }

    /// <summary>Play one or more cards from hand.</summary>
    public async Task PlayCards(string roomCode, List<IPlayableCard> cards)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.PlayCards(state, playerId, cards));
    }

    /// <summary>Draw a card from the draw pile.</summary>
    public async Task DrawCard(string roomCode)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.DrawCard(state, playerId));
    }

    /// <summary>Declare the required suit after playing a Jack.</summary>
    public async Task DeclareJackSuit(string roomCode, Suit suit)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.DeclareJackSuit(state, playerId, suit));
    }

    /// <summary>Discard a card face-down under the deck (9 effect).</summary>
    public async Task DiscardCard(string roomCode, IPlayableCard card)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.DiscardCard(state, playerId, card));
    }

    /// <summary>Accept a pending penalty (draw for 6/KoH, skip for 7/Ace).</summary>
    public async Task AcceptPenalty(string roomCode)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.AcceptPenalty(state, playerId));
    }

    /// <summary>Start a new round, preserving scores.</summary>
    public async Task StartNewRound(string roomCode)
    {
        await ExecuteGameAction(roomCode, state => _engine.StartNewRound(state));
    }

    /// <summary>Pass turn after having drawn a card that could be played.</summary>
    public async Task PassTurn(string roomCode)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.PassTurn(state, playerId));
    }

    /// <summary>Dealer declines to cover the face-up card after dealing.</summary>
    public async Task SkipDealerCover(string roomCode)
    {
        var playerId = GetPlayerId();
        await ExecuteGameAction(roomCode, state => _engine.SkipDealerCover(state, playerId));
    }

    /// <summary>Leave the room gracefully.</summary>
    public async Task LeaveRoom(string roomCode)
    {
        var conn = _roomManager.RemoveConnection(Context.ConnectionId);
        if (conn is null) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);

        var room = _roomManager.GetRoom(roomCode);
        if (room is null) return;

        await room.Lock.WaitAsync();
        try
        {
            var player = room.State.Players.Find(p => p.Id == conn.PlayerId);
            if (player is null) return;

            if (room.State.Phase == GamePhase.WaitingForPlayers)
            {
                room.State.Players.Remove(player);
            }
            else
            {
                // Mid-game: mark as eliminated so the engine skips their turns
                player.IsEliminated = true;
            }

            await Clients.Group(roomCode).SendAsync("PlayerLeft", player.Name);
            await BroadcastGameState(room.State);
        }
        finally
        {
            room.Lock.Release();
        }
    }

    // ═══════════════════════════════════════════════
    //  Connection lifecycle
    // ═══════════════════════════════════════════════

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var conn = _roomManager.RemoveConnection(Context.ConnectionId);
        if (conn is not null)
        {
            var room = _roomManager.GetRoom(conn.RoomCode);
            if (room is not null)
            {
                await room.Lock.WaitAsync();
                try
                {
                    var player = room.State.Players.Find(p => p.Id == conn.PlayerId);
                    if (player is not null)
                    {
                        if (room.State.Phase == GamePhase.WaitingForPlayers)
                            room.State.Players.Remove(player);
                        else
                            player.IsEliminated = true;

                        await Clients.Group(conn.RoomCode).SendAsync("PlayerLeft", player.Name);
                        await BroadcastGameState(room.State);
                    }
                }
                finally
                {
                    room.Lock.Release();
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ═══════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════

    /// <summary>Resolve the current caller's player ID from their connection.</summary>
    private string GetPlayerId()
    {
        var conn = _roomManager.GetConnection(Context.ConnectionId)
            ?? throw new HubException("Not connected to any room.");
        return conn.PlayerId;
    }

    /// <summary>
    /// Execute a game engine action under the per-room lock.
    /// Broadcasts events and personalized state to all players afterward.
    /// Engine exceptions (InvalidOperationException) are sent as Error messages to the caller.
    /// </summary>
    private async Task ExecuteGameAction(string roomCode, Func<GameState, List<IGameEvent>> action)
    {
        var room = _roomManager.GetRoom(roomCode);
        if (room is null)
        {
            await Clients.Caller.SendAsync("Error", "Room not found.");
            return;
        }

        await room.Lock.WaitAsync();
        try
        {
            var events = action(room.State);
            await BroadcastEvents(roomCode, events);
            await BroadcastGameState(room.State);
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
        finally
        {
            room.Lock.Release();
        }
    }

    /// <summary>
    /// Broadcast each game event to all players in the room.
    /// Uses the event type name as the SignalR method name for easy client-side routing.
    /// </summary>
    private async Task BroadcastEvents(string roomCode, List<IGameEvent> events)
    {
        foreach (var evt in events)
        {
            // Use specific method names so clients can subscribe to individual event types
            switch (evt)
            {
                case CardPlayed e:
                    await Clients.Group(roomCode).SendAsync("CardPlayed", e.PlayerId, e.Cards);
                    break;
                case CardDrawn e:
                    await Clients.Group(roomCode).SendAsync("CardDrawn", e.PlayerId, e.Count);
                    break;
                case TurnSkipped e:
                    await Clients.Group(roomCode).SendAsync("TurnSkipped", e.PlayerId);
                    break;
                case SuitDeclared e:
                    await Clients.Group(roomCode).SendAsync("SuitDeclared", e.Suit, e.IsUgolovshchina);
                    break;
                case TurnChanged e:
                    await Clients.Group(roomCode).SendAsync("TurnChanged", e.CurrentPlayerId);
                    break;
                case RoundEnded e:
                    await Clients.Group(roomCode).SendAsync("RoundEnded", e.Scores);
                    break;
                case ScoreUpdated e:
                    await Clients.Group(roomCode).SendAsync("ScoreUpdated", e.PlayerId, e.NewScore);
                    break;
                case PlayerEliminated e:
                    await Clients.Group(roomCode).SendAsync("PlayerEliminated", e.PlayerId);
                    break;
                case GameStarted e:
                    await Clients.Group(roomCode).SendAsync("GameStarted", e.RoomCode);
                    break;
                case DeckReshuffled e:
                    await Clients.Group(roomCode).SendAsync("DeckReshuffled", e.NewMultiplier);
                    break;
                case DiscardedUnderDeck e:
                    await Clients.Group(roomCode).SendAsync("DiscardedUnderDeck", e.PlayerId);
                    break;
                case ConsecutiveQueensRoundEnd:
                    await Clients.Group(roomCode).SendAsync("ConsecutiveQueensRoundEnd");
                    break;
                case JokerCancelled e:
                    await Clients.Group(roomCode).SendAsync("JokerCancelled", e.PlayerId);
                    break;
            }
        }
    }

    /// <summary>
    /// Send each player their own personalized view of the game state.
    /// Each player sees all public info + only their own hand.
    /// </summary>
    private async Task BroadcastGameState(GameState state)
    {
        var dto = ToDto(state);

        foreach (var player in state.Players)
        {
            var connectionId = _roomManager.GetConnectionId(state.RoomCode, player.Id);
            if (connectionId is null) continue;

            var hand = new HandDto(player.Hand.ToList());
            await Clients.Client(connectionId).SendAsync("GameStateUpdated", dto, hand);
        }
    }

    /// <summary>
    /// Map authoritative GameState to a client-safe DTO.
    /// No player hand contents are included — those go via HandDto per-player.
    /// </summary>
    private static GameStateDto ToDto(GameState state)
    {
        var players = state.Players.Select(p =>
            new PlayerDto(p.Id, p.Name, p.Hand.Count, p.Score, p.IsEliminated)).ToList();

        var currentPlayerId = state.Players.Count > 0 && state.CurrentPlayerIndex < state.Players.Count
            ? state.Players[state.CurrentPlayerIndex].Id
            : string.Empty;

        return new GameStateDto(
            state.RoomCode,
            state.Phase,
            players,
            state.TopTableCard,
            state.DrawPile.Count,
            currentPlayerId,
            state.DeclaredSuit,
            state.IsUgolovshchina,
            state.PendingDraws,
            state.SkipCount,
            state.DeckFlipMultiplier,
            state.IsExtendedMode,
            state.AwaitingEightCover,
            state.AwaitingNineDiscard,
            state.HasDrawnThisTurn,
            state.IsDealerCoverPhase,
            state.ExtraTurns
        );
    }
}
