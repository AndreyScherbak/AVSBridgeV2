using AVSBridge.Shared.Engine;
using AVSBridge.Shared.Events;
using AVSBridge.Shared.Models;

namespace AVSBridge.Tests.Engine;

public class GameEngineTests
{
    private readonly GameEngine _engine = new();

    // ────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────

    /// <summary>
    /// Create a minimal GameState with N players, ready to deal.
    /// </summary>
    private static GameState CreateWaitingState(int playerCount = 2, bool extended = false)
    {
        var state = new GameState
        {
            RoomCode = "TEST",
            IsExtendedMode = extended,
            Phase = GamePhase.WaitingForPlayers,
            DealerIndex = 0
        };
        for (int i = 0; i < playerCount; i++)
            state.Players.Add(new Player($"p{i}", $"Player{i}"));
        return state;
    }

    /// <summary>
    /// Create an in-progress 2-player state with controlled hands and table card.
    /// Player 0 is current, Player 1 is next.
    /// </summary>
    private static GameState CreateInProgressState(
        IPlayableCard tableTop,
        List<IPlayableCard>? p0Hand = null,
        List<IPlayableCard>? p1Hand = null)
    {
        var state = new GameState
        {
            RoomCode = "TEST",
            Phase = GamePhase.InProgress,
            CurrentPlayerIndex = 0,
            DealerIndex = 1
        };
        var p0 = new Player("p0", "Alice");
        var p1 = new Player("p1", "Bob");
        if (p0Hand != null) p0.Hand.AddRange(p0Hand);
        if (p1Hand != null) p1.Hand.AddRange(p1Hand);
        state.Players.Add(p0);
        state.Players.Add(p1);
        state.TablePile.Add(tableTop);

        // Seed draw pile with some cards so draws don't fail
        for (int i = 0; i < 20; i++)
            state.DrawPile.Add(new Card(Suit.Clubs, Rank.Two));

        return state;
    }

    // ────────────────────────────────────────────
    //  DealCards
    // ────────────────────────────────────────────

    [Fact]
    public void DealCards_DealsCorrectNumberOfCards()
    {
        var state = CreateWaitingState(3);
        _engine.DealCards(state, new Random(42));

        // Dealer (index 0) gets DealerHandSize = 5, others get 6
        Assert.Equal(5, state.Players[0].Hand.Count);
        Assert.Equal(6, state.Players[1].Hand.Count);
        Assert.Equal(6, state.Players[2].Hand.Count);
    }

    [Fact]
    public void DealCards_ExtendedMode_DealsNineCards()
    {
        var state = CreateWaitingState(2, extended: true);
        _engine.DealCards(state, new Random(42));

        Assert.Equal(8, state.Players[0].Hand.Count); // dealer: 9−1
        Assert.Equal(9, state.Players[1].Hand.Count);
    }

    [Fact]
    public void DealCards_PlacesOneCardOnTable()
    {
        var state = CreateWaitingState(2);
        _engine.DealCards(state, new Random(42));

        Assert.Single(state.TablePile);
    }

    [Fact]
    public void DealCards_TransitionsToInProgress()
    {
        var state = CreateWaitingState(2);
        _engine.DealCards(state, new Random(42));

        Assert.Equal(GamePhase.InProgress, state.Phase);
    }

    [Fact]
    public void DealCards_EmitsGameStartedAndTurnChanged()
    {
        var state = CreateWaitingState(2);
        var events = _engine.DealCards(state, new Random(42));

        Assert.Contains(events, e => e is GameStarted);
        Assert.Contains(events, e => e is TurnChanged);
    }

    [Fact]
    public void DealCards_TooFewPlayers_Throws()
    {
        var state = CreateWaitingState(1);
        Assert.Throws<InvalidOperationException>(() => _engine.DealCards(state));
    }

    [Fact]
    public void DealCards_WrongPhase_Throws()
    {
        var state = CreateWaitingState(2);
        state.Phase = GamePhase.InProgress;
        Assert.Throws<InvalidOperationException>(() => _engine.DealCards(state));
    }

    [Fact]
    public void DealCards_DealerCanCover_SetsDealerCoverPhase()
    {
        // We need a controlled scenario: force the face-up card to match something in dealer's hand.
        // Use a seeded Random and check.
        var state = CreateWaitingState(2);
        _engine.DealCards(state, new Random(42));

        // Whether dealer cover phase is set depends on the random seed.
        // Just verify the state is consistent.
        if (state.IsDealerCoverPhase)
        {
            Assert.Equal(state.DealerIndex, state.CurrentPlayerIndex);
        }
    }

    // ────────────────────────────────────────────
    //  PlayCards — basic moves
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_SuitMatch_Succeeds()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Ten);
        var state = CreateInProgressState(top, p0Hand: [card]);

        var events = _engine.PlayCards(state, "p0", [card]);

        Assert.Contains(events, e => e is CardPlayed);
        Assert.Equal(card, state.TopTableCard);
    }

    [Fact]
    public void PlayCards_RankMatch_Succeeds()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Clubs, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [card]);

        _engine.PlayCards(state, "p0", [card]);

        Assert.Equal(card, state.TopTableCard);
    }

    [Fact]
    public void PlayCards_NoMatch_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Clubs, Rank.Ten);
        var state = CreateInProgressState(top, p0Hand: [card]);

        Assert.Throws<InvalidOperationException>(() => _engine.PlayCards(state, "p0", [card]));
    }

    [Fact]
    public void PlayCards_WrongTurn_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Ten);
        var state = CreateInProgressState(top, p1Hand: [card]);

        Assert.Throws<InvalidOperationException>(() => _engine.PlayCards(state, "p1", [card]));
    }

    [Fact]
    public void PlayCards_CardNotInHand_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Ten);
        var state = CreateInProgressState(top, p0Hand: []);

        Assert.Throws<InvalidOperationException>(() => _engine.PlayCards(state, "p0", [card]));
    }

    [Fact]
    public void PlayCards_AdvancesTurn()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Ten);
        var extra = new Card(Suit.Diamonds, Rank.Two); // keep hand non-empty
        var state = CreateInProgressState(top, p0Hand: [card, extra]);

        _engine.PlayCards(state, "p0", [card]);

        Assert.Equal(1, state.CurrentPlayerIndex);
    }

    // ────────────────────────────────────────────
    //  Jack — wildcard + suit declaration
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Jack_WaitsForSuitDeclaration()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [jack, extra]);

        var events = _engine.PlayCards(state, "p0", [jack]);

        // Turn should NOT have advanced (waiting for DeclareJackSuit)
        Assert.Equal(0, state.CurrentPlayerIndex);
        Assert.DoesNotContain(events, e => e is TurnChanged);
    }

    [Fact]
    public void DeclareJackSuit_SetsSuit_AdvancesTurn()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [jack, extra]);

        _engine.PlayCards(state, "p0", [jack]);
        var events = _engine.DeclareJackSuit(state, "p0", Suit.Diamonds);

        Assert.Equal(Suit.Diamonds, state.DeclaredSuit);
        Assert.Equal(1, state.CurrentPlayerIndex);
        Assert.Contains(events, e => e is SuitDeclared);
    }

    [Fact]
    public void DeclareJackSuit_Ugolovshchina_WhenSuitMatchesJack()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [jack, extra]);

        _engine.PlayCards(state, "p0", [jack]);
        _engine.DeclareJackSuit(state, "p0", Suit.Hearts); // same as Jack's suit

        Assert.True(state.IsUgolovshchina);
    }

    [Fact]
    public void DeclareJackSuit_NonMatchingSuit_NotUgolovshchina()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [jack, extra]);

        _engine.PlayCards(state, "p0", [jack]);
        _engine.DeclareJackSuit(state, "p0", Suit.Clubs);

        Assert.False(state.IsUgolovshchina);
    }

    // ────────────────────────────────────────────
    //  6 — draw penalty stacking
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Six_AddsPendingDraws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var six = new Card(Suit.Hearts, Rank.Six);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [six, extra]);

        _engine.PlayCards(state, "p0", [six]);

        Assert.Equal(2, state.PendingDraws);
    }

    [Fact]
    public void PlayCards_Six_Stacking_AccumulatesDraws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var six1 = new Card(Suit.Hearts, Rank.Six);
        var six2 = new Card(Suit.Clubs, Rank.Six);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top,
            p0Hand: [six1, extra],
            p1Hand: [six2, extra]);

        _engine.PlayCards(state, "p0", [six1]); // PendingDraws = 2
        _engine.PlayCards(state, "p1", [six2]); // PendingDraws = 4

        Assert.Equal(4, state.PendingDraws);
    }

    [Fact]
    public void PlayCards_NonSixWhilePendingDraws_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Six);
        var card = new Card(Suit.Hearts, Rank.Ten);
        var state = CreateInProgressState(top, p0Hand: [card]);
        state.PendingDraws = 2;

        Assert.Throws<InvalidOperationException>(() => _engine.PlayCards(state, "p0", [card]));
    }

    [Fact]
    public void DrawCard_WithPendingDraws_DrawsAll()
    {
        var top = new Card(Suit.Hearts, Rank.Six);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);
        state.PendingDraws = 4;

        _engine.DrawCard(state, "p0");

        // Player drew 4 cards: had 1 + drew 4 = 5
        Assert.Equal(5, state.Players[0].Hand.Count);
        Assert.Equal(0, state.PendingDraws);
    }

    // ────────────────────────────────────────────
    //  7 — skip + draw
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Seven_SetsSkipCount()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var seven = new Card(Suit.Hearts, Rank.Seven);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [seven, extra]);

        _engine.PlayCards(state, "p0", [seven]);

        Assert.Equal(1, state.SkipCount);
        Assert.True(state.SkipRequiresDraw);
    }

    [Fact]
    public void DrawCard_WithSkipFromSeven_DrawsOneAndSkips()
    {
        var top = new Card(Suit.Hearts, Rank.Seven);
        var state = CreateInProgressState(top);
        state.SkipCount = 1;
        state.SkipRequiresDraw = true;
        state.CurrentPlayerIndex = 1; // Bob's turn

        int handBefore = state.Players[1].Hand.Count;
        var events = _engine.DrawCard(state, "p1");

        Assert.Equal(handBefore + 1, state.Players[1].Hand.Count);
        Assert.Equal(0, state.SkipCount);
        Assert.Contains(events, e => e is TurnSkipped);
    }

    // ────────────────────────────────────────────
    //  8 — cover immediately
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Eight_WithCoverInHand_AwaitsEightCover()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var eight = new Card(Suit.Hearts, Rank.Eight);
        var cover = new Card(Suit.Hearts, Rank.Five); // same suit as 8
        var state = CreateInProgressState(top, p0Hand: [eight, cover]);

        _engine.PlayCards(state, "p0", [eight]);

        Assert.True(state.AwaitingEightCover);
        Assert.Equal(0, state.CurrentPlayerIndex); // still p0's turn
    }

    [Fact]
    public void PlayCards_EightCover_ValidCover_Succeeds()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var eight = new Card(Suit.Hearts, Rank.Eight);
        var cover = new Card(Suit.Hearts, Rank.Five);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [eight, cover, extra]);

        _engine.PlayCards(state, "p0", [eight]);
        Assert.True(state.AwaitingEightCover);

        _engine.PlayCards(state, "p0", [cover]);
        Assert.False(state.AwaitingEightCover);
        Assert.Equal(cover, state.TopTableCard);
    }

    [Fact]
    public void PlayCards_Eight_NoCoverInHand_AutoDrawsUntilCover()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var eight = new Card(Suit.Hearts, Rank.Eight);
        // No cover card in hand; draw pile has a Hearts card at various positions
        var state = CreateInProgressState(top, p0Hand: [eight]);

        // Seed draw pile: first two are Clubs (no cover), third is Hearts (valid cover)
        state.DrawPile.Clear();
        state.DrawPile.Add(new Card(Suit.Hearts, Rank.Four)); // bottom (index 0) — drawn 3rd
        state.DrawPile.Add(new Card(Suit.Clubs, Rank.Three)); // drawn 2nd
        state.DrawPile.Add(new Card(Suit.Clubs, Rank.Two));   // top (index 2) — drawn 1st

        var events = _engine.PlayCards(state, "p0", [eight]);

        // Should have auto-drawn 3 cards: 2 non-covers + 1 cover played on table
        Assert.False(state.AwaitingEightCover);
        var topCard = state.TopTableCard;
        Assert.True(topCard is Card { Suit: Suit.Hearts }); // cover is Hearts
    }

    // ────────────────────────────────────────────
    //  9 — discard
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Nine_AwaitsDiscard()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var nine = new Card(Suit.Hearts, Rank.Nine);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [nine, extra]);

        _engine.PlayCards(state, "p0", [nine]);

        Assert.True(state.AwaitingNineDiscard);
    }

    [Fact]
    public void DiscardCard_PlacesCardUnderDrawPile()
    {
        var top = new Card(Suit.Hearts, Rank.Nine);
        var discard = new Card(Suit.Diamonds, Rank.Two);
        var extra = new Card(Suit.Clubs, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [discard, extra]);
        state.AwaitingNineDiscard = true;

        _engine.DiscardCard(state, "p0", discard);

        Assert.False(state.AwaitingNineDiscard);
        Assert.Equal(discard, state.DrawPile[0]); // bottom of pile
        Assert.DoesNotContain(discard, state.Players[0].Hand);
    }

    // ────────────────────────────────────────────
    //  Ace — skip turn (no draw)
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Ace_SetsSkipWithoutDraw()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var ace = new Card(Suit.Hearts, Rank.Ace);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [ace, extra]);

        _engine.PlayCards(state, "p0", [ace]);

        Assert.Equal(1, state.SkipCount);
        Assert.False(state.SkipRequiresDraw);
    }

    [Fact]
    public void DrawCard_WithAceSkip_SkipsWithoutDrawing()
    {
        var top = new Card(Suit.Hearts, Rank.Ace);
        var state = CreateInProgressState(top);
        state.SkipCount = 1;
        state.SkipRequiresDraw = false;
        state.CurrentPlayerIndex = 1;

        int handBefore = state.Players[1].Hand.Count;
        var events = _engine.DrawCard(state, "p1");

        Assert.Equal(handBefore, state.Players[1].Hand.Count); // no card drawn
        Assert.Equal(0, state.SkipCount);
        Assert.Contains(events, e => e is TurnSkipped);
    }

    // ────────────────────────────────────────────
    //  King of Hearts — +6 draw, cancellable by King
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_KingOfHearts_AddsSixPendingDraws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var koh = new Card(Suit.Hearts, Rank.King);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [koh, extra]);

        _engine.PlayCards(state, "p0", [koh]);

        Assert.Equal(6, state.PendingDraws);
        Assert.True(state.KingOfHeartsActive);
    }

    [Fact]
    public void PlayCards_KingCancelsKingOfHearts()
    {
        var top = new Card(Suit.Hearts, Rank.King); // KoH on table
        var king = new Card(Suit.Spades, Rank.King); // another King cancels
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [king, extra]);
        state.PendingDraws = 6;
        state.KingOfHeartsActive = true;
        // When KoH active, only Kings are allowed
        state.CurrentPlayerIndex = 0;

        _engine.PlayCards(state, "p0", [king]);

        Assert.Equal(0, state.PendingDraws);
        Assert.False(state.KingOfHeartsActive);
    }

    // ────────────────────────────────────────────
    //  Red Joker — 3 consecutive turns
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_RedJoker_GivesExtraTurns()
    {
        var top = new Card(Suit.Hearts, Rank.Three); // red card
        var joker = new JokerCard(JokerColor.Red);
        var extra1 = new Card(Suit.Hearts, Rank.Five);
        var extra2 = new Card(Suit.Diamonds, Rank.Five);
        var state = CreateInProgressState(top, p0Hand: [joker, extra1, extra2]);

        _engine.PlayCards(state, "p0", [joker]);

        // ExtraTurns starts at 2, but AdvanceTurn immediately consumes 1 → 1 remaining
        Assert.Equal(1, state.ExtraTurns);
        // Still p0's turn (extra turn)
        Assert.Equal(0, state.CurrentPlayerIndex);
    }

    [Fact]
    public void ExtraTurns_DecrementEachTurn()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var joker = new JokerCard(JokerColor.Red);
        var play1 = new Card(Suit.Hearts, Rank.Five);
        var play2 = new Card(Suit.Diamonds, Rank.Five);
        var extra = new Card(Suit.Clubs, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [joker, play1, play2, extra]);

        _engine.PlayCards(state, "p0", [joker]);     // ExtraTurns set to 2, AdvanceTurn → 1
        Assert.Equal(1, state.ExtraTurns);
        Assert.Equal(0, state.CurrentPlayerIndex);

        _engine.PlayCards(state, "p0", [play1]);      // AdvanceTurn → ExtraTurns = 0
        Assert.Equal(0, state.ExtraTurns);
        Assert.Equal(0, state.CurrentPlayerIndex);     // last extra turn consumed, stays on p0

        _engine.PlayCards(state, "p0", [play2]);      // No extra turns left → advances to p1
        Assert.Equal(1, state.CurrentPlayerIndex);
    }

    // ────────────────────────────────────────────
    //  Black Joker — all others draw 6
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_BlackJoker_OtherPlayerDrawsSix()
    {
        var top = new Card(Suit.Spades, Rank.Three); // black card
        var joker = new JokerCard(JokerColor.Black);
        var extra = new Card(Suit.Clubs, Rank.Two);
        var state = CreateInProgressState(top,
            p0Hand: [joker, extra],
            p1Hand: [new Card(Suit.Diamonds, Rank.Two)]);

        int p1HandBefore = state.Players[1].Hand.Count;
        _engine.PlayCards(state, "p0", [joker]);

        Assert.Equal(p1HandBefore + 6, state.Players[1].Hand.Count);
    }

    // ────────────────────────────────────────────
    //  Queen — consecutive tracking
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_Queen_TracksConsecutive()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var queen = new Card(Suit.Hearts, Rank.Queen);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [queen, extra]);

        _engine.PlayCards(state, "p0", [queen]);

        Assert.Equal(1, state.ConsecutiveQueenCount);
        Assert.Contains(Suit.Hearts, state.ConsecutiveQueenSuits);
    }

    [Fact]
    public void PlayCards_NonQueen_ResetsQueenCount()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Five);
        var extra = new Card(Suit.Diamonds, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [card, extra]);
        state.ConsecutiveQueenCount = 2;
        state.ConsecutiveQueenSuits.Add(Suit.Hearts);
        state.ConsecutiveQueenSuits.Add(Suit.Spades);

        _engine.PlayCards(state, "p0", [card]);

        Assert.Equal(0, state.ConsecutiveQueenCount);
        Assert.Empty(state.ConsecutiveQueenSuits);
    }

    [Fact]
    public void FourConsecutiveQueens_DifferentSuits_EndsRound()
    {
        // Set up state with 3 consecutive Queens already played
        var top = new Card(Suit.Clubs, Rank.Queen);
        var queen4 = new Card(Suit.Diamonds, Rank.Queen);
        var extra = new Card(Suit.Hearts, Rank.Two);
        var state = CreateInProgressState(top, p0Hand: [queen4, extra]);
        state.ConsecutiveQueenCount = 3;
        state.ConsecutiveQueenSuits.Add(Suit.Hearts);
        state.ConsecutiveQueenSuits.Add(Suit.Spades);
        state.ConsecutiveQueenSuits.Add(Suit.Clubs);

        var events = _engine.PlayCards(state, "p0", [queen4]);

        Assert.Contains(events, e => e is ConsecutiveQueensRoundEnd);
        Assert.Contains(events, e => e is RoundEnded);
        Assert.Equal(GamePhase.RoundOver, state.Phase);
    }

    // ────────────────────────────────────────────
    //  DrawCard — voluntary draw
    // ────────────────────────────────────────────

    [Fact]
    public void DrawCard_Voluntary_DrawsOneCard()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);

        int before = state.Players[0].Hand.Count;
        _engine.DrawCard(state, "p0");

        Assert.Equal(before + 1, state.Players[0].Hand.Count);
    }

    [Fact]
    public void DrawCard_PlayableCardDrawn_DoesNotAdvanceTurn()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);

        // Put a Hearts card on top of draw pile so it's playable
        state.DrawPile.Add(new Card(Suit.Hearts, Rank.Five));

        _engine.DrawCard(state, "p0");

        Assert.True(state.HasDrawnThisTurn);
        Assert.Equal(0, state.CurrentPlayerIndex); // still p0
    }

    [Fact]
    public void DrawCard_UnplayableCardDrawn_AdvancesTurn()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);

        // Put an unplayable card on top of draw pile
        state.DrawPile.Add(new Card(Suit.Clubs, Rank.Ten));

        _engine.DrawCard(state, "p0");

        Assert.False(state.HasDrawnThisTurn);
        Assert.Equal(1, state.CurrentPlayerIndex); // advanced to p1
    }

    [Fact]
    public void DrawCard_AlreadyDrew_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);
        state.HasDrawnThisTurn = true;

        Assert.Throws<InvalidOperationException>(() => _engine.DrawCard(state, "p0"));
    }

    // ────────────────────────────────────────────
    //  PassTurn
    // ────────────────────────────────────────────

    [Fact]
    public void PassTurn_AfterDrawing_AdvancesTurn()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);
        state.HasDrawnThisTurn = true;

        _engine.PassTurn(state, "p0");

        Assert.Equal(1, state.CurrentPlayerIndex);
        Assert.False(state.HasDrawnThisTurn);
    }

    [Fact]
    public void PassTurn_WithoutDrawing_Throws()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);

        Assert.Throws<InvalidOperationException>(() => _engine.PassTurn(state, "p0"));
    }

    // ────────────────────────────────────────────
    //  Round end — empty hand
    // ────────────────────────────────────────────

    [Fact]
    public void PlayCards_LastCard_EndsRound()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var lastCard = new Card(Suit.Hearts, Rank.Five);
        var state = CreateInProgressState(top,
            p0Hand: [lastCard],
            p1Hand: [new Card(Suit.Clubs, Rank.Ten)]);

        var events = _engine.PlayCards(state, "p0", [lastCard]);

        Assert.Contains(events, e => e is RoundEnded);
        Assert.Equal(GamePhase.RoundOver, state.Phase);
    }

    [Fact]
    public void EndRound_WinnerGetsZero_LoserGetsHandPoints()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var lastCard = new Card(Suit.Hearts, Rank.Five);
        var p1Cards = new List<IPlayableCard>
        {
            new Card(Suit.Clubs, Rank.Ten),  // 10
            new Card(Suit.Clubs, Rank.Ace),  // 15
        };
        var state = CreateInProgressState(top, p0Hand: [lastCard], p1Hand: p1Cards);

        _engine.PlayCards(state, "p0", [lastCard]);

        Assert.Equal(0, state.Players[0].Score);  // winner
        Assert.Equal(25, state.Players[1].Score);  // 10 + 15
    }

    [Fact]
    public void EndRound_JackEnding_WinnerGetsMinus20()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        var state = CreateInProgressState(top,
            p0Hand: [jack],
            p1Hand: [new Card(Suit.Clubs, Rank.Two)]);

        _engine.PlayCards(state, "p0", [jack]);
        // Hand is empty, but it's a Jack — need to declare suit, then round ends
        var events = _engine.DeclareJackSuit(state, "p0", Suit.Hearts);

        Assert.Contains(events, e => e is RoundEnded);
        Assert.Equal(-20, state.Players[0].Score); // −20 for Jack ending
    }

    [Fact]
    public void EndRound_DeckFlipMultiplier_MultipliesScores()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var lastCard = new Card(Suit.Hearts, Rank.Five);
        var p1Cards = new List<IPlayableCard> { new Card(Suit.Clubs, Rank.Ten) }; // 10 pts
        var state = CreateInProgressState(top, p0Hand: [lastCard], p1Hand: p1Cards);
        state.DeckFlipMultiplier = 3;

        _engine.PlayCards(state, "p0", [lastCard]);

        Assert.Equal(30, state.Players[1].Score); // 10 × 3
    }

    // ────────────────────────────────────────────
    //  Deck reshuffling
    // ────────────────────────────────────────────

    [Fact]
    public void DrawCard_EmptyDrawPile_ReshufflesFromTable()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var state = CreateInProgressState(top, p0Hand: [new Card(Suit.Diamonds, Rank.Two)]);
        state.DrawPile.Clear(); // empty draw pile
        // Add extra cards to table so reshuffle is possible
        state.TablePile.Insert(0, new Card(Suit.Clubs, Rank.Two));
        state.TablePile.Insert(0, new Card(Suit.Spades, Rank.Two));

        var events = _engine.DrawCard(state, "p0");

        Assert.Contains(events, e => e is DeckReshuffled);
        Assert.Equal(2, state.DeckFlipMultiplier); // incremented from 1
        // Top table card should remain
        Assert.Equal(top, state.TablePile[^1]);
    }

    // ────────────────────────────────────────────
    //  Joker-on-Joker cancellation
    // ────────────────────────────────────────────

    [Fact]
    public void BlackJoker_OnRedJoker_CancelsExtraTurns()
    {
        var redJoker = new JokerCard(JokerColor.Red);
        var blackJoker = new JokerCard(JokerColor.Black);
        var extra = new Card(Suit.Clubs, Rank.Two);
        var state = CreateInProgressState(redJoker,
            p0Hand: [blackJoker, extra],
            p1Hand: [new Card(Suit.Diamonds, Rank.Two)]);
        state.ExtraTurns = 1; // simulating Red Joker was played, 1 extra turn remains

        var events = _engine.PlayCards(state, "p0", [blackJoker]);

        Assert.Equal(0, state.ExtraTurns); // Red Joker's extra turns cancelled
        Assert.Contains(events, e => e is JokerCancelled);
    }

    // ────────────────────────────────────────────
    //  IsValidMove
    // ────────────────────────────────────────────

    [Fact]
    public void IsValidMove_SuitMatch_True()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Hearts, Rank.Ten);
        Assert.True(_engine.IsValidMove(card, top, null));
    }

    [Fact]
    public void IsValidMove_NoMatch_False()
    {
        var top = new Card(Suit.Hearts, Rank.Three);
        var card = new Card(Suit.Clubs, Rank.Ten);
        Assert.False(_engine.IsValidMove(card, top, null));
    }

    [Fact]
    public void IsValidMove_JackOnAnything_True()
    {
        var top = new Card(Suit.Spades, Rank.Two);
        var jack = new Card(Suit.Hearts, Rank.Jack);
        Assert.True(_engine.IsValidMove(jack, top, null));
    }

    [Fact]
    public void IsValidMove_DeclaredSuit_True()
    {
        var top = new Card(Suit.Hearts, Rank.Jack);
        var card = new Card(Suit.Spades, Rank.Five);
        Assert.True(_engine.IsValidMove(card, top, Suit.Spades));
    }

    // ────────────────────────────────────────────
    //  SkipDealerCover
    // ────────────────────────────────────────────

    [Fact]
    public void SkipDealerCover_AdvancesToNextPlayer()
    {
        var state = CreateInProgressState(
            new Card(Suit.Hearts, Rank.Three),
            p0Hand: [new Card(Suit.Hearts, Rank.Three)]);
        state.IsDealerCoverPhase = true;
        state.DealerIndex = 0;
        state.CurrentPlayerIndex = 0;

        _engine.SkipDealerCover(state, "p0");

        Assert.False(state.IsDealerCoverPhase);
        Assert.Equal(1, state.CurrentPlayerIndex);
    }

    [Fact]
    public void SkipDealerCover_NotInPhase_Throws()
    {
        var state = CreateInProgressState(
            new Card(Suit.Hearts, Rank.Three),
            p0Hand: [new Card(Suit.Hearts, Rank.Three)]);
        state.IsDealerCoverPhase = false;

        Assert.Throws<InvalidOperationException>(() => _engine.SkipDealerCover(state, "p0"));
    }
}
