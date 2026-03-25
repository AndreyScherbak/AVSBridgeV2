namespace AVSBridge.Shared.Models;

/// <summary>
/// Full authoritative game state, lives on the server only.
/// </summary>
public sealed class GameState
{
    public string RoomCode { get; init; } = string.Empty;
    public List<Player> Players { get; init; } = [];
    public List<IPlayableCard> DrawPile { get; init; } = [];
    public List<IPlayableCard> TablePile { get; init; } = [];
    public int CurrentPlayerIndex { get; set; }
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;
    public bool IsExtendedMode { get; init; }

    // --- Pending effects ---

    /// <summary>Suit declared after playing a Jack. Null when no declaration is active.</summary>
    public Suit? DeclaredSuit { get; set; }

    /// <summary>True if the Jack declaration matched its own suit ("по уголовщині").</summary>
    public bool IsUgolovshchina { get; set; }

    /// <summary>Accumulated draw penalty (from 6s, King of Hearts, Black Joker).</summary>
    public int PendingDraws { get; set; }

    /// <summary>Number of players to skip (from 7s and Aces).</summary>
    public int SkipCount { get; set; }

    /// <summary>How many extra turns the current player has (Red Joker gives 3 total).</summary>
    public int ExtraTurns { get; set; }

    /// <summary>Consecutive Queens played count (4 = round ends).</summary>
    public int ConsecutiveQueenCount { get; set; }

    /// <summary>Suits of consecutive Queens played (must be 4 different suits to end round).</summary>
    public HashSet<Suit> ConsecutiveQueenSuits { get; init; } = [];

    /// <summary>Starts at 1; incremented each time the draw pile is reshuffled from table cards.</summary>
    public int DeckFlipMultiplier { get; set; } = 1;

    /// <summary>Index of the player who is the dealer this round.</summary>
    public int DealerIndex { get; set; }

    /// <summary>True when PendingDraws includes +6 from King of Hearts (cancellable by any King).</summary>
    public bool KingOfHeartsActive { get; set; }

    /// <summary>True when pending skips come from 7s (draw 1), false when from Aces (no draw).</summary>
    public bool SkipRequiresDraw { get; set; }

    /// <summary>True when the current player must cover an 8 they just played.</summary>
    public bool AwaitingEightCover { get; set; }

    /// <summary>True when the current player must discard a card (9 effect).</summary>
    public bool AwaitingNineDiscard { get; set; }

    /// <summary>True when the current player has drawn a card this turn and may play it or pass.</summary>
    public bool HasDrawnThisTurn { get; set; }

    /// <summary>True during dealer's optional cover phase after dealing.</summary>
    public bool IsDealerCoverPhase { get; set; }

    // --- Helpers ---

    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public IPlayableCard? TopTableCard => TablePile.Count > 0 ? TablePile[^1] : null;

    /// <summary>Number of cards dealt to each player (6 normal, 9 extended).</summary>
    public int StartingHandSize => IsExtendedMode ? 9 : 6;

    /// <summary>Number of cards the dealer keeps (5 normal, 8 extended; 1 goes face-up).</summary>
    public int DealerHandSize => StartingHandSize - 1;
}
