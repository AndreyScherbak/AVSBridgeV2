namespace AVSBridge.Shared.Models;

/// <summary>
/// Creates and shuffles a standard 54-card deck (52 + 2 Jokers).
/// </summary>
public static class DeckFactory
{
    public static List<IPlayableCard> CreateStandardDeck()
    {
        var deck = new List<IPlayableCard>(54);

        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                deck.Add(new Card(suit, rank));
            }
        }

        deck.Add(new JokerCard(JokerColor.Red));
        deck.Add(new JokerCard(JokerColor.Black));

        return deck;
    }

    public static void Shuffle(List<IPlayableCard> deck, Random? rng = null)
    {
        rng ??= Random.Shared;

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }
}
