using AVSBridge.Shared.Models;

namespace AVSBridge.Client.Helpers;

/// <summary>
/// Maps game cards to their sprite image paths.
/// </summary>
public static class CardImageHelper
{
    private static readonly Dictionary<Rank, string> RankCodes = new()
    {
        [Rank.Two] = "2", [Rank.Three] = "3", [Rank.Four] = "4",
        [Rank.Five] = "5", [Rank.Six] = "6", [Rank.Seven] = "7",
        [Rank.Eight] = "8", [Rank.Nine] = "9", [Rank.Ten] = "10",
        [Rank.Jack] = "J", [Rank.Queen] = "Q", [Rank.King] = "K",
        [Rank.Ace] = "A"
    };

    private static readonly Dictionary<Suit, string> SuitCodes = new()
    {
        [Suit.Hearts] = "H", [Suit.Diamonds] = "D",
        [Suit.Clubs] = "C", [Suit.Spades] = "S"
    };

    public static string GetImagePath(IPlayableCard card) => card switch
    {
        Card c => $"images/cards/{RankCodes[c.Rank]}{SuitCodes[c.Suit]}.png",
        JokerCard { Color: JokerColor.Red } => "images/cards/JokerRed.png",
        JokerCard { Color: JokerColor.Black } => "images/cards/blackjoker.png",
        _ => "images/cards/blue_back.png"
    };

    public static string CardBack => "images/cards/blue_back.png";

    public static string GetSuitSymbol(Suit suit) => suit switch
    {
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs => "♣",
        Suit.Spades => "♠",
        _ => ""
    };

    public static string GetSuitColor(Suit suit) => suit switch
    {
        Suit.Hearts or Suit.Diamonds => "#e74c3c",
        _ => "#2c3e50"
    };

    public static string GetCardDisplayName(IPlayableCard card) => card switch
    {
        Card c => $"{c.Rank} of {c.Suit}",
        JokerCard j => $"{j.Color} Joker",
        _ => "Unknown"
    };
}
