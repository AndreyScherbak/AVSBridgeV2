namespace AVSBridge.Shared.Models;

/// <summary>
/// A standard playing card (not a Joker).
/// </summary>
public sealed record Card(Suit Suit, Rank Rank) : IPlayableCard
{
    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;
    public bool IsBlack => Suit is Suit.Clubs or Suit.Spades;

    /// <summary>
    /// Point value used for scoring at round end.
    /// </summary>
    public int PointValue => Rank switch
    {
        Rank.Two => 2,
        Rank.Three => 3,
        Rank.Four => 4,
        Rank.Five => 5,
        Rank.Six => 0,
        Rank.Seven => 0,
        Rank.Eight => 0,
        Rank.Nine => 0,
        Rank.Ten => 10,
        Rank.Jack => 20,
        Rank.Queen => 10,
        Rank.King => 10,
        Rank.Ace => 15,
        _ => 0
    };

    /// <summary>
    /// The special effect this card triggers when played.
    /// </summary>
    public CardEffect Effect => Rank switch
    {
        Rank.Six => CardEffect.DrawTwo,
        Rank.Seven => CardEffect.SkipAndDraw,
        Rank.Eight => CardEffect.CoverImmediately,
        Rank.Nine => CardEffect.DiscardOne,
        Rank.Jack => CardEffect.DeclareAnySuit,
        Rank.King when Suit == Suit.Hearts => CardEffect.KingOfHeartsDraw,
        Rank.Ace => CardEffect.SkipTurn,
        _ => CardEffect.None
    };

    /// <summary>
    /// Whether this card can be legally played on top of the given table card,
    /// considering the currently declared suit (from a Jack).
    /// Does NOT account for special stacking rules (6-on-6, 7-on-7, etc.).
    /// </summary>
    public bool CanPlayOn(IPlayableCard topCard, Suit? declaredSuit)
    {
        // Jack can be played on anything
        if (Rank == Rank.Jack)
            return true;

        if (topCard is JokerCard joker)
        {
            // After a Joker, only matching color suits are allowed
            return joker.Color == JokerColor.Red ? IsRed : IsBlack;
        }

        if (topCard is Card card)
        {
            // If a suit was declared (Jack), match that suit
            if (declaredSuit.HasValue)
                return Suit == declaredSuit.Value || Rank == card.Rank;

            // Normal: match rank or suit
            return Rank == card.Rank || Suit == card.Suit;
        }

        return false;
    }

    public override string ToString() => $"{Rank} of {Suit}";
}
