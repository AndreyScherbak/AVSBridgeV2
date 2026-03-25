namespace AVSBridge.Shared.Models;

/// <summary>
/// Represents a player in the game.
/// Mutable: hand and score change during gameplay.
/// </summary>
public sealed class Player
{
    public string Id { get; }
    public string Name { get; }
    public List<IPlayableCard> Hand { get; } = [];
    public int Score { get; set; }
    public bool IsEliminated { get; set; }

    public Player(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// Sum of point values for all cards remaining in hand.
    /// Used for scoring when another player goes out.
    /// </summary>
    public int HandPointValue => Hand.Sum(c => c.PointValue);

    /// <summary>
    /// Apply score change with burn rules:
    /// +120 exactly → reset to 0.
    /// −120 exactly → reset to 0.
    /// ≥ +125 → player is eliminated (loses).
    /// </summary>
    public void ApplyScore(int points)
    {
        Score += points;

        if (Score == 120 || Score == -120)
        {
            Score = 0;
        }
        else if (Score >= 125)
        {
            IsEliminated = true;
        }
        // Reaching −125 or below: game continues, player does NOT win
    }
}
