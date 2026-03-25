using AVSBridge.Shared.Models;

namespace AVSBridge.Tests.Models;

public class PlayerTests
{
    [Fact]
    public void ApplyScore_NormalAddition()
    {
        var player = new Player("1", "Test");
        player.ApplyScore(30);
        Assert.Equal(30, player.Score);
        Assert.False(player.IsEliminated);
    }

    [Fact]
    public void ApplyScore_Exactly120_ResetsToZero()
    {
        var player = new Player("1", "Test") { Score = 100 };
        player.ApplyScore(20); // 100 + 20 = 120 → burn → 0
        Assert.Equal(0, player.Score);
        Assert.False(player.IsEliminated);
    }

    [Fact]
    public void ApplyScore_Exactly125OrMore_Eliminated()
    {
        var player = new Player("1", "Test") { Score = 110 };
        player.ApplyScore(15); // 110 + 15 = 125 → eliminated
        Assert.True(player.IsEliminated);
    }

    [Fact]
    public void ApplyScore_ExactlyNegative120_ResetsToZero()
    {
        var player = new Player("1", "Test") { Score = -100 };
        player.ApplyScore(-20); // -100 + (-20) = -120 → burn → 0
        Assert.Equal(0, player.Score);
        Assert.False(player.IsEliminated);
    }

    [Fact]
    public void ApplyScore_BelowNegative125_NotEliminated()
    {
        var player = new Player("1", "Test") { Score = -100 };
        player.ApplyScore(-30); // -130 → continues (does NOT win or lose)
        Assert.Equal(-130, player.Score);
        Assert.False(player.IsEliminated);
    }

    [Fact]
    public void HandPointValue_SumsCards()
    {
        var player = new Player("1", "Test");
        player.Hand.Add(new Card(Suit.Hearts, Rank.Ace));   // 15
        player.Hand.Add(new Card(Suit.Clubs, Rank.Ten));    // 10
        player.Hand.Add(new JokerCard(JokerColor.Red));     // 50
        Assert.Equal(75, player.HandPointValue);
    }
}
