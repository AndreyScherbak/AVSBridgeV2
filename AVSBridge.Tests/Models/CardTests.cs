using AVSBridge.Shared.Models;

namespace AVSBridge.Tests.Models;

public class CardTests
{
    // --- Point values ---

    [Theory]
    [InlineData(Rank.Two, 2)]
    [InlineData(Rank.Three, 3)]
    [InlineData(Rank.Five, 5)]
    [InlineData(Rank.Six, 0)]
    [InlineData(Rank.Seven, 0)]
    [InlineData(Rank.Eight, 0)]
    [InlineData(Rank.Nine, 0)]
    [InlineData(Rank.Ten, 10)]
    [InlineData(Rank.Jack, 20)]
    [InlineData(Rank.Queen, 10)]
    [InlineData(Rank.King, 10)]
    [InlineData(Rank.Ace, 15)]
    public void Card_PointValue_IsCorrect(Rank rank, int expected)
    {
        var card = new Card(Suit.Hearts, rank);
        Assert.Equal(expected, card.PointValue);
    }

    [Fact]
    public void Joker_PointValue_Is50()
    {
        Assert.Equal(50, new JokerCard(JokerColor.Red).PointValue);
        Assert.Equal(50, new JokerCard(JokerColor.Black).PointValue);
    }

    // --- Card effects ---

    [Fact]
    public void Six_HasDrawTwoEffect()
    {
        var card = new Card(Suit.Clubs, Rank.Six);
        Assert.Equal(CardEffect.DrawTwo, card.Effect);
    }

    [Fact]
    public void Seven_HasSkipAndDrawEffect()
    {
        var card = new Card(Suit.Clubs, Rank.Seven);
        Assert.Equal(CardEffect.SkipAndDraw, card.Effect);
    }

    [Fact]
    public void Eight_HasCoverImmediatelyEffect()
    {
        var card = new Card(Suit.Clubs, Rank.Eight);
        Assert.Equal(CardEffect.CoverImmediately, card.Effect);
    }

    [Fact]
    public void Nine_HasDiscardOneEffect()
    {
        var card = new Card(Suit.Clubs, Rank.Nine);
        Assert.Equal(CardEffect.DiscardOne, card.Effect);
    }

    [Fact]
    public void Jack_HasDeclareAnySuitEffect()
    {
        var card = new Card(Suit.Hearts, Rank.Jack);
        Assert.Equal(CardEffect.DeclareAnySuit, card.Effect);
    }

    [Fact]
    public void KingOfHearts_HasSpecialEffect()
    {
        var kh = new Card(Suit.Hearts, Rank.King);
        Assert.Equal(CardEffect.KingOfHeartsDraw, kh.Effect);

        // Other Kings have no effect
        var ks = new Card(Suit.Spades, Rank.King);
        Assert.Equal(CardEffect.None, ks.Effect);
    }

    [Fact]
    public void Ace_HasSkipTurnEffect()
    {
        var card = new Card(Suit.Diamonds, Rank.Ace);
        Assert.Equal(CardEffect.SkipTurn, card.Effect);
    }

    [Fact]
    public void RedJoker_HasTripleTurnEffect()
    {
        Assert.Equal(CardEffect.RedJokerTriple, new JokerCard(JokerColor.Red).Effect);
    }

    [Fact]
    public void BlackJoker_HasAllDrawEffect()
    {
        Assert.Equal(CardEffect.BlackJokerAllDraw, new JokerCard(JokerColor.Black).Effect);
    }

    [Theory]
    [InlineData(Rank.Two)]
    [InlineData(Rank.Three)]
    [InlineData(Rank.Four)]
    [InlineData(Rank.Five)]
    [InlineData(Rank.Ten)]
    [InlineData(Rank.Queen)]
    public void NoEffectCards_HaveNoneEffect(Rank rank)
    {
        var card = new Card(Suit.Clubs, rank);
        Assert.Equal(CardEffect.None, card.Effect);
    }

    // --- CanPlayOn ---

    [Fact]
    public void Card_CanPlayOn_MatchingSuit()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var play = new Card(Suit.Hearts, Rank.Ten);
        Assert.True(play.CanPlayOn(top, null));
    }

    [Fact]
    public void Card_CanPlayOn_MatchingRank()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var play = new Card(Suit.Clubs, Rank.Three);
        Assert.True(play.CanPlayOn(top, null));
    }

    [Fact]
    public void Card_CannotPlayOn_NoMatch()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var play = new Card(Suit.Clubs, Rank.Ten);
        Assert.False(play.CanPlayOn(top, null));
    }

    [Fact]
    public void Jack_CanPlayOn_AnyCard()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        Assert.True(jack.CanPlayOn(top, null));
    }

    [Fact]
    public void Card_CanPlayOn_DeclaredSuit()
    {
        var top = new Card(Suit.Hearts, Rank.Jack);
        var play = new Card(Suit.Spades, Rank.Five);
        // Jack declared Spades
        Assert.True(play.CanPlayOn(top, Suit.Spades));
        Assert.False(play.CanPlayOn(top, Suit.Diamonds));
    }

    [Fact]
    public void Card_CanPlayOn_DeclaredSuit_MatchingRankAlsoWorks()
    {
        var top = new Card(Suit.Hearts, Rank.Jack);
        var play = new Card(Suit.Diamonds, Rank.Jack);
        // Declared Clubs, but rank matches → legal
        Assert.True(play.CanPlayOn(top, Suit.Clubs));
    }

    [Fact]
    public void Card_AfterRedJoker_OnlyRedSuitsAllowed()
    {
        var joker = new JokerCard(JokerColor.Red);
        var hearts = new Card(Suit.Hearts, Rank.Five);
        var clubs = new Card(Suit.Clubs, Rank.Five);

        Assert.True(hearts.CanPlayOn(joker, null));
        Assert.False(clubs.CanPlayOn(joker, null));
    }

    [Fact]
    public void Card_AfterBlackJoker_OnlyBlackSuitsAllowed()
    {
        var joker = new JokerCard(JokerColor.Black);
        var spades = new Card(Suit.Spades, Rank.Five);
        var diamonds = new Card(Suit.Diamonds, Rank.Five);

        Assert.True(spades.CanPlayOn(joker, null));
        Assert.False(diamonds.CanPlayOn(joker, null));
    }

    [Fact]
    public void Joker_CanPlayOn_MatchingColorCard()
    {
        var redCard = new Card(Suit.Hearts, Rank.Five);
        var blackCard = new Card(Suit.Spades, Rank.Five);

        Assert.True(new JokerCard(JokerColor.Red).CanPlayOn(redCard, null));
        Assert.False(new JokerCard(JokerColor.Red).CanPlayOn(blackCard, null));
        Assert.True(new JokerCard(JokerColor.Black).CanPlayOn(blackCard, null));
        Assert.False(new JokerCard(JokerColor.Black).CanPlayOn(redCard, null));
    }

    [Fact]
    public void Joker_CanPlayOn_OtherJoker()
    {
        var red = new JokerCard(JokerColor.Red);
        var black = new JokerCard(JokerColor.Black);

        Assert.True(red.CanPlayOn(black, null));
        Assert.True(black.CanPlayOn(red, null));
    }

    // --- Color helpers ---

    [Fact]
    public void Card_IsRed_IsBlack()
    {
        Assert.True(new Card(Suit.Hearts, Rank.Ace).IsRed);
        Assert.True(new Card(Suit.Diamonds, Rank.Ace).IsRed);
        Assert.True(new Card(Suit.Clubs, Rank.Ace).IsBlack);
        Assert.True(new Card(Suit.Spades, Rank.Ace).IsBlack);
    }
}
