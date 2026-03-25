using AVSBridge.Shared.Models;

namespace AVSBridge.Shared.DTOs;

/// <summary>
/// Client-facing view of the game state.
/// Contains only the requesting player's hand + public information.
/// </summary>
public sealed record GameStateDto(
    string RoomCode,
    GamePhase Phase,
    List<PlayerDto> Players,
    IPlayableCard? TopTableCard,
    int DrawPileCount,
    string CurrentPlayerId,
    Suit? DeclaredSuit,
    bool IsUgolovshchina,
    int PendingDraws,
    int SkipCount,
    int DeckFlipMultiplier,
    bool IsExtendedMode
);

/// <summary>
/// Public info about another player (card count, not card contents).
/// The requesting player's own hand is sent separately.
/// </summary>
public sealed record PlayerDto(
    string Id,
    string Name,
    int CardCount,
    int Score,
    bool IsEliminated
);

/// <summary>
/// Sent only to the owning player: their full hand.
/// </summary>
public sealed record HandDto(
    List<IPlayableCard> Cards
);
