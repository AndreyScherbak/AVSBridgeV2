namespace AVSBridge.Shared.Models;

/// <summary>
/// A Joker card (Red or Black).
/// </summary>
public sealed record JokerCard(JokerColor Color) : IPlayableCard
{
    public bool IsRed => Color == JokerColor.Red;
    public bool IsBlack => Color == JokerColor.Black;

    public int PointValue => 50;

    public CardEffect Effect => Color switch
    {
        JokerColor.Red => CardEffect.RedJokerTriple,
        JokerColor.Black => CardEffect.BlackJokerAllDraw,
        _ => CardEffect.None
    };

    /// <summary>
    /// Red Joker is playable on any red card; Black Joker on any black card.
    /// A Joker can also be played on the other Joker (Joker-on-Joker cancel rule).
    /// </summary>
    public bool CanPlayOn(IPlayableCard topCard, Suit? declaredSuit)
    {
        if (topCard is JokerCard)
            return true; // Joker-on-Joker is always legal

        if (topCard is Card card)
        {
            // If a suit was declared via Jack, check declared suit color
            if (declaredSuit.HasValue)
            {
                var declaredIsRed = declaredSuit.Value is Suit.Hearts or Suit.Diamonds;
                return IsRed ? declaredIsRed : !declaredIsRed;
            }

            return IsRed ? card.IsRed : card.IsBlack;
        }

        return false;
    }

    public override string ToString() => $"{Color} Joker";
}
