using AVSBridge.Shared.Models;

namespace AVSBridge.Shared.Events;

/// <summary>
/// Base interface for all game events produced by the engine.
/// </summary>
public interface IGameEvent;

public sealed record CardPlayed(string PlayerId, List<IPlayableCard> Cards) : IGameEvent;

public sealed record CardDrawn(string PlayerId, int Count) : IGameEvent;

public sealed record TurnSkipped(string PlayerId) : IGameEvent;

public sealed record SuitDeclared(string PlayerId, Suit Suit, bool IsUgolovshchina) : IGameEvent;

public sealed record TurnChanged(string CurrentPlayerId) : IGameEvent;

public sealed record RoundEnded(Dictionary<string, int> Scores) : IGameEvent;

public sealed record ScoreUpdated(string PlayerId, int NewScore) : IGameEvent;

public sealed record PlayerEliminated(string PlayerId) : IGameEvent;

public sealed record RoomCreated(string RoomCode) : IGameEvent;

public sealed record PlayerJoined(string PlayerId, string PlayerName) : IGameEvent;

public sealed record PlayerLeft(string PlayerId, string PlayerName) : IGameEvent;

public sealed record GameStarted(string RoomCode) : IGameEvent;

public sealed record DeckReshuffled(int NewMultiplier) : IGameEvent;

public sealed record DiscardedUnderDeck(string PlayerId) : IGameEvent;

public sealed record ConsecutiveQueensRoundEnd() : IGameEvent;

public sealed record JokerCancelled(string PlayerId) : IGameEvent;
