using System.Text.Json.Serialization;

namespace AVSBridge.Shared.Models;

/// <summary>
/// Marker interface unifying Card and JokerCard so they can share
/// a hand list and table pile.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Card), "card")]
[JsonDerivedType(typeof(JokerCard), "joker")]
public interface IPlayableCard
{
    int PointValue { get; }
    CardEffect Effect { get; }
    bool CanPlayOn(IPlayableCard topCard, Suit? declaredSuit);
}
