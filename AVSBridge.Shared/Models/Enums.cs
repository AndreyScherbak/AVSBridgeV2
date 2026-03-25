namespace AVSBridge.Shared.Models;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

public enum JokerColor
{
    Red,
    Black
}

/// <summary>
/// Describes the special effect triggered by playing a card.
/// </summary>
public enum CardEffect
{
    None,
    DrawTwo,          // 6: next player draws +2 (stackable)
    SkipAndDraw,      // 7: next player(s) skip turn AND draw 1 (stackable per player)
    CoverImmediately, // 8: must be covered immediately by the player who played it
    DiscardOne,       // 9: discard 1 card face-down under the deck
    DeclareAnySuit,   // Jack: playable on any card, declares next suit
    FourQueensEnd,    // Queen: 4 consecutive Queens end the round
    KingOfHeartsDraw, // King of Hearts: next player draws 6
    SkipTurn,         // Ace: next player(s) skip turn (no draw)
    RedJokerTriple,   // Red Joker: player takes 3 consecutive turns
    BlackJokerAllDraw // Black Joker: all other players draw 6
}

/// <summary>
/// Current phase of the game.
/// </summary>
public enum GamePhase
{
    WaitingForPlayers,
    Dealing,
    InProgress,
    RoundOver,
    GameOver
}
