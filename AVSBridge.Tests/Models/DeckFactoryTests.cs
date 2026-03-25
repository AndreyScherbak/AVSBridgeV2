using AVSBridge.Shared.Models;

namespace AVSBridge.Tests.Models;

public class DeckFactoryTests
{
    [Fact]
    public void CreateStandardDeck_Has54Cards()
    {
        var deck = DeckFactory.CreateStandardDeck();
        Assert.Equal(54, deck.Count);
    }

    [Fact]
    public void CreateStandardDeck_Has52StandardCards()
    {
        var deck = DeckFactory.CreateStandardDeck();
        Assert.Equal(52, deck.OfType<Card>().Count());
    }

    [Fact]
    public void CreateStandardDeck_Has2Jokers()
    {
        var deck = DeckFactory.CreateStandardDeck();
        var jokers = deck.OfType<JokerCard>().ToList();
        Assert.Equal(2, jokers.Count);
        Assert.Contains(jokers, j => j.Color == JokerColor.Red);
        Assert.Contains(jokers, j => j.Color == JokerColor.Black);
    }

    [Fact]
    public void CreateStandardDeck_AllRanksAndSuits()
    {
        var deck = DeckFactory.CreateStandardDeck();
        var cards = deck.OfType<Card>().ToList();

        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                Assert.Contains(cards, c => c.Suit == suit && c.Rank == rank);
            }
        }
    }

    [Fact]
    public void Shuffle_ChangesOrder()
    {
        var deck1 = DeckFactory.CreateStandardDeck();
        var deck2 = DeckFactory.CreateStandardDeck();

        DeckFactory.Shuffle(deck2, new Random(42));

        // Very unlikely to remain in same order after shuffle
        Assert.False(deck1.SequenceEqual(deck2));
    }
}
