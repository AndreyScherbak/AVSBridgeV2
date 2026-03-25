namespace AVSBridge.Shared.Models;

/// <summary>
/// Marker interface unifying Card and JokerCard so they can share
/// a hand list and table pile.
/// </summary>
public interface IPlayableCard
{
    int PointValue { get; }
    CardEffect Effect { get; }
    bool CanPlayOn(IPlayableCard topCard, Suit? declaredSuit);
}
