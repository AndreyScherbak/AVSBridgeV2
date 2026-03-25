using AVSBridge.Shared.Events;
using AVSBridge.Shared.Models;

namespace AVSBridge.Shared.Engine;

/// <summary>
/// Pure stateless game engine. Each method takes the current GameState + an action,
/// mutates the state in place, and returns a list of events describing what happened.
/// </summary>
public sealed class GameEngine
{
    // ────────────────────────────────────────────
    //  DealCards
    // ────────────────────────────────────────────

    /// <summary>
    /// Shuffle a fresh deck, deal cards to every player, place the dealer's
    /// face-up card on the table, and transition to InProgress.
    /// </summary>
    public List<IGameEvent> DealCards(GameState state, Random? rng = null)
    {
        if (state.Phase != GamePhase.WaitingForPlayers)
            throw new InvalidOperationException("Game is not in WaitingForPlayers phase.");
        if (state.Players.Count < 2 || state.Players.Count > 10)
            throw new InvalidOperationException("Need 2–10 players to start.");

        var events = new List<IGameEvent>();

        // Fresh deck
        state.DrawPile.Clear();
        state.DrawPile.AddRange(DeckFactory.CreateStandardDeck());
        DeckFactory.Shuffle(state.DrawPile, rng);

        state.TablePile.Clear();
        state.Phase = GamePhase.Dealing;

        int dealerIdx = state.DealerIndex;

        // Deal StartingHandSize to every player; dealer keeps DealerHandSize
        for (int i = 0; i < state.Players.Count; i++)
        {
            var player = state.Players[i];
            player.Hand.Clear();
            int count = (i == dealerIdx) ? state.DealerHandSize : state.StartingHandSize;
            for (int j = 0; j < count; j++)
            {
                player.Hand.Add(PopDraw(state));
            }
        }

        // Place one card face-up on the table (counts as dealer's turn)
        var faceUpCard = PopDraw(state);
        state.TablePile.Add(faceUpCard);

        // Reset all pending-effect fields
        ResetPendingEffects(state);

        state.Phase = GamePhase.InProgress;

        // Dealer Jack Exception: no suit declared; next player must match Jack's suit.
        // Normal CanPlayOn already handles this (declaredSuit == null → match suit/rank).

        // Check if dealer can cover the face-up card with a same-rank card
        bool dealerCanCover = state.Players[dealerIdx].Hand.Any(c => SameRankAs(c, faceUpCard));

        if (dealerCanCover)
        {
            state.CurrentPlayerIndex = dealerIdx;
            state.IsDealerCoverPhase = true;
        }
        else
        {
            state.CurrentPlayerIndex = GetNextActivePlayerIndex(state, dealerIdx);
            state.IsDealerCoverPhase = false;
        }

        events.Add(new GameStarted(state.RoomCode));
        events.Add(new TurnChanged(state.CurrentPlayer.Id));

        return events;
    }

    /// <summary>
    /// Dealer declines to cover the face-up card; advance to the next player.
    /// </summary>
    public List<IGameEvent> SkipDealerCover(GameState state, string playerId)
    {
        ValidateTurn(state, playerId);
        if (!state.IsDealerCoverPhase)
            throw new InvalidOperationException("Not in dealer cover phase.");

        state.IsDealerCoverPhase = false;
        var events = new List<IGameEvent>();
        state.CurrentPlayerIndex = GetNextActivePlayerIndex(state, state.DealerIndex);
        events.Add(new TurnChanged(state.CurrentPlayer.Id));
        return events;
    }

    // ────────────────────────────────────────────
    //  PlayCards
    // ────────────────────────────────────────────

    /// <summary>
    /// Play one card from the player's hand. Validates the move, applies effects,
    /// checks end-of-round conditions, and advances the turn.
    /// </summary>
    public List<IGameEvent> PlayCards(GameState state, string playerId, List<IPlayableCard> cards)
    {
        ValidateTurn(state, playerId);
        if (cards.Count == 0)
            throw new InvalidOperationException("Must play at least one card.");

        var player = state.CurrentPlayer;
        var events = new List<IGameEvent>();

        // ── Sub-state: covering an 8 ──
        if (state.AwaitingEightCover)
            return HandleEightCover(state, player, cards[0], events);

        // ── Sub-state: discarding for a 9 (redirected through DiscardCard) ──
        if (state.AwaitingNineDiscard)
            return DiscardCard(state, player.Id, cards[0]);

        var card = cards[0];

        // Card must be in hand
        if (!player.Hand.Contains(card))
            throw new InvalidOperationException($"Card {card} is not in player's hand.");

        // ── Dealer cover phase: only same-rank cards allowed ──
        if (state.IsDealerCoverPhase)
        {
            var topCard = state.TopTableCard!;
            if (!SameRankAs(card, topCard))
                throw new InvalidOperationException("During dealer cover, must play a card of the same rank.");

            player.Hand.Remove(card);
            state.TablePile.Add(card);
            state.IsDealerCoverPhase = false;
            events.Add(new CardPlayed(playerId, [card]));

            // Dealer Jack Exception: if dealer covers a Jack with another Jack, they may declare suit
            if (card is Card { Rank: Rank.Jack })
            {
                // Wait for DeclareJackSuit; don't advance turn yet
                return events;
            }

            state.CurrentPlayerIndex = GetNextActivePlayerIndex(state, state.DealerIndex);
            events.Add(new TurnChanged(state.CurrentPlayer.Id));
            return events;
        }

        // ── Pending-effect restrictions ──
        ValidatePendingEffectRestrictions(state, card);

        // ── Standard validation ──
        var top = state.TopTableCard!;
        if (!card.CanPlayOn(top, state.DeclaredSuit))
            throw new InvalidOperationException($"Card {card} cannot be played on {top}.");

        // Play the card
        player.Hand.Remove(card);
        state.TablePile.Add(card);
        state.DeclaredSuit = null;
        state.IsUgolovshchina = false;
        state.HasDrawnThisTurn = false;
        events.Add(new CardPlayed(playerId, [card]));

        // ── Cancel King-of-Hearts if a King covers it ──
        if (state.KingOfHeartsActive && card is Card { Rank: Rank.King })
        {
            state.PendingDraws -= 6;
            if (state.PendingDraws < 0) state.PendingDraws = 0;
            state.KingOfHeartsActive = false;
        }

        // Apply card effect
        ApplyCardEffect(state, player, card, events);

        // Hand empty → round ends (unless awaiting 8 cover, 9 discard, or Jack declaration)
        if (player.Hand.Count == 0
            && !state.AwaitingEightCover
            && !state.AwaitingNineDiscard)
        {
            if (card is Card { Rank: Rank.Jack })
            {
                // Still need suit declaration, but round will end after that
                return events;
            }
            return EndRound(state, player.Id, events);
        }

        // Four consecutive Queens of different suits → round ends
        if (state.ConsecutiveQueenCount >= 4 && state.ConsecutiveQueenSuits.Count >= 4)
        {
            events.Add(new ConsecutiveQueensRoundEnd());
            return EndRound(state, null, events);
        }

        // Advance turn unless awaiting a follow-up action
        if (!state.AwaitingEightCover
            && !state.AwaitingNineDiscard
            && card is not Card { Rank: Rank.Jack }) // Jack waits for DeclareJackSuit
        {
            AdvanceTurn(state, events);
        }

        return events;
    }

    // ────────────────────────────────────────────
    //  DrawCard
    // ────────────────────────────────────────────

    /// <summary>
    /// Draw cards. Handles: forced draws (6 / KoH penalties), skip acceptance (7 / Ace),
    /// and voluntary single-card draw (no playable card).
    /// </summary>
    public List<IGameEvent> DrawCard(GameState state, string playerId)
    {
        ValidateTurn(state, playerId);
        if (state.AwaitingEightCover || state.AwaitingNineDiscard)
            throw new InvalidOperationException("Must complete pending action first.");
        if (state.HasDrawnThisTurn)
            throw new InvalidOperationException("Already drew a card this turn.");

        var player = state.CurrentPlayer;
        var events = new List<IGameEvent>();

        // ── Accept skip (7 or Ace) ──
        if (state.SkipCount > 0)
        {
            state.SkipCount--;
            events.Add(new TurnSkipped(playerId));
            if (state.SkipRequiresDraw)
                DrawCardsFromPile(state, player, 1, events);

            AdvanceTurn(state, events);
            return events;
        }

        // ── Forced draw (6s / King of Hearts) ──
        if (state.PendingDraws > 0)
        {
            int count = state.PendingDraws;
            DrawCardsFromPile(state, player, count, events);
            state.PendingDraws = 0;
            state.KingOfHeartsActive = false;
            AdvanceTurn(state, events);
            return events;
        }

        // ── Voluntary draw ──
        var drawn = DrawCardsFromPile(state, player, 1, events);
        if (drawn.Count > 0 && drawn[0].CanPlayOn(state.TopTableCard!, state.DeclaredSuit))
        {
            // Drawn card is playable — player may play it or pass
            state.HasDrawnThisTurn = true;
        }
        else
        {
            AdvanceTurn(state, events);
        }

        return events;
    }

    // ────────────────────────────────────────────
    //  DeclareJackSuit
    // ────────────────────────────────────────────

    /// <summary>
    /// Declare the required suit after playing a Jack.
    /// </summary>
    public List<IGameEvent> DeclareJackSuit(GameState state, string playerId, Suit suit)
    {
        ValidateTurn(state, playerId);
        if (state.TopTableCard is not Card { Rank: Rank.Jack } jack)
            throw new InvalidOperationException("Top card is not a Jack.");
        if (state.DeclaredSuit.HasValue)
            throw new InvalidOperationException("Suit already declared.");

        bool isUgolovshchina = jack.Suit == suit;
        state.DeclaredSuit = suit;
        state.IsUgolovshchina = isUgolovshchina;

        var events = new List<IGameEvent>
        {
            new SuitDeclared(playerId, suit, isUgolovshchina)
        };

        // If hand is empty the round ends now
        if (state.CurrentPlayer.Hand.Count == 0)
            return EndRound(state, playerId, events);

        AdvanceTurn(state, events);
        return events;
    }

    // ────────────────────────────────────────────
    //  DiscardCard (9 effect)
    // ────────────────────────────────────────────

    /// <summary>
    /// Discard one card face-down under the draw pile (9 effect).
    /// </summary>
    public List<IGameEvent> DiscardCard(GameState state, string playerId, IPlayableCard card)
    {
        ValidateTurn(state, playerId);
        if (!state.AwaitingNineDiscard)
            throw new InvalidOperationException("No pending discard.");

        var player = state.CurrentPlayer;
        if (!player.Hand.Contains(card))
            throw new InvalidOperationException("Card not in hand.");

        player.Hand.Remove(card);
        state.DrawPile.Insert(0, card); // bottom of pile
        state.AwaitingNineDiscard = false;

        var events = new List<IGameEvent> { new DiscardedUnderDeck(playerId) };

        if (player.Hand.Count == 0)
            return EndRound(state, playerId, events);

        AdvanceTurn(state, events);
        return events;
    }

    // ────────────────────────────────────────────
    //  PassTurn
    // ────────────────────────────────────────────

    /// <summary>
    /// Pass after drawing a playable card (player declines to play it).
    /// </summary>
    public List<IGameEvent> PassTurn(GameState state, string playerId)
    {
        ValidateTurn(state, playerId);
        if (!state.HasDrawnThisTurn)
            throw new InvalidOperationException("Can only pass after drawing.");

        state.HasDrawnThisTurn = false;
        var events = new List<IGameEvent>();
        AdvanceTurn(state, events);
        return events;
    }

    // ────────────────────────────────────────────
    //  IsValidMove
    // ────────────────────────────────────────────

    /// <summary>
    /// Pure validation: can this card be played on the given top card?
    /// Does not account for pending-effect restrictions (6-stacking, etc.).
    /// </summary>
    public bool IsValidMove(IPlayableCard card, IPlayableCard topCard, Suit? declaredSuit)
    {
        return card.CanPlayOn(topCard, declaredSuit);
    }

    // ================================================================
    //  Private helpers
    // ================================================================

    private static void ValidateTurn(GameState state, string playerId)
    {
        if (state.Phase != GamePhase.InProgress)
            throw new InvalidOperationException("Game is not in progress.");
        if (state.CurrentPlayer.Id != playerId)
            throw new InvalidOperationException("It's not your turn.");
    }

    /// <summary>
    /// When a pending effect is active, only specific counter-cards are allowed.
    /// </summary>
    private static void ValidatePendingEffectRestrictions(GameState state, IPlayableCard card)
    {
        // Pending +2 draws from 6s (not KoH) — only a 6 can stack
        if (state.PendingDraws > 0 && !state.KingOfHeartsActive)
        {
            if (card is not Card { Rank: Rank.Six })
                throw new InvalidOperationException("Must play a 6 to stack or draw cards.");
        }

        // Pending KoH +6 — only a King cancels
        if (state.KingOfHeartsActive)
        {
            if (card is not Card { Rank: Rank.King })
                throw new InvalidOperationException("Must play a King to cancel King of Hearts or draw cards.");
        }

        // Pending skips from 7s — only a 7 can stack
        if (state.SkipCount > 0 && state.SkipRequiresDraw)
        {
            if (card is not Card { Rank: Rank.Seven })
                throw new InvalidOperationException("Must play a 7 to stack or accept skip.");
        }

        // Pending skips from Aces — only an Ace can stack
        if (state.SkipCount > 0 && !state.SkipRequiresDraw)
        {
            if (card is not Card { Rank: Rank.Ace })
                throw new InvalidOperationException("Must play an Ace to stack or accept skip.");
        }
    }

    // ────────────────────────────────────────────
    //  Effect application
    // ────────────────────────────────────────────

    private void ApplyCardEffect(GameState state, Player player, IPlayableCard card, List<IGameEvent> events)
    {
        // Track consecutive Queens
        if (card is Card { Rank: Rank.Queen } queen)
        {
            state.ConsecutiveQueenCount++;
            state.ConsecutiveQueenSuits.Add(queen.Suit);
        }
        else
        {
            state.ConsecutiveQueenCount = 0;
            state.ConsecutiveQueenSuits.Clear();
        }

        switch (card.Effect)
        {
            case CardEffect.DrawTwo: // 6
                state.PendingDraws += 2;
                break;

            case CardEffect.SkipAndDraw: // 7
                state.SkipCount++;
                state.SkipRequiresDraw = true;
                break;

            case CardEffect.CoverImmediately: // 8
                HandleEightPlayed(state, player, (Card)card, events);
                break;

            case CardEffect.DiscardOne: // 9 — multiple 9s in one turn = still only 1 discard
                if (player.Hand.Count > 0)
                    state.AwaitingNineDiscard = true;
                break;

            case CardEffect.DeclareAnySuit: // Jack — awaits DeclareJackSuit call
                break;

            case CardEffect.KingOfHeartsDraw: // King of Hearts
                state.PendingDraws += 6;
                state.KingOfHeartsActive = true;
                break;

            case CardEffect.SkipTurn: // Ace
                state.SkipCount++;
                state.SkipRequiresDraw = false;
                break;

            case CardEffect.RedJokerTriple: // Red Joker — 3 total turns (current + 2 extra)
                HandleJokerOnJokerCancel(state, events, isRedJoker: true);
                state.ExtraTurns += 2;
                break;

            case CardEffect.BlackJokerAllDraw: // Black Joker — all other players draw 6
                HandleJokerOnJokerCancel(state, events, isRedJoker: false);
                foreach (var other in state.Players)
                {
                    if (other.Id != player.Id && !other.IsEliminated)
                        DrawCardsFromPile(state, other, 6, events);
                }
                break;

            case CardEffect.FourQueensEnd: // tracked via ConsecutiveQueenCount above
            case CardEffect.None:
            default:
                break;
        }
    }

    private void HandleEightPlayed(GameState state, Player player, Card eight, List<IGameEvent> events)
    {
        // Does the player hold a valid cover? (same suit or another 8)
        bool hasCover = player.Hand.Any(c =>
            c is Card hc && (hc.Suit == eight.Suit || hc.Rank == Rank.Eight));

        if (hasCover)
        {
            state.AwaitingEightCover = true;
        }
        else
        {
            // Auto-draw until a valid cover is found
            AutoCoverEight(state, player, eight, events);
        }
    }

    private List<IGameEvent> HandleEightCover(
        GameState state, Player player, IPlayableCard coverCard, List<IGameEvent> events)
    {
        if (!player.Hand.Contains(coverCard))
            throw new InvalidOperationException("Card not in hand.");

        var eight = (Card)state.TopTableCard!;

        if (coverCard is not Card cc || (cc.Suit != eight.Suit && cc.Rank != Rank.Eight))
            throw new InvalidOperationException("Cover card must match the 8's suit or be another 8.");

        player.Hand.Remove(coverCard);
        state.TablePile.Add(coverCard);
        state.AwaitingEightCover = false;
        events.Add(new CardPlayed(player.Id, [coverCard]));

        // Covering with another 8 triggers another cover cycle
        if (cc.Rank == Rank.Eight)
        {
            HandleEightPlayed(state, player, cc, events);
            if (state.AwaitingEightCover)
                return events; // wait for next cover
        }

        // Queen tracking: an 8-cover sequence is not a Queen, reset
        state.ConsecutiveQueenCount = 0;
        state.ConsecutiveQueenSuits.Clear();

        if (player.Hand.Count == 0)
            return EndRound(state, player.Id, events);

        AdvanceTurn(state, events);
        return events;
    }

    private void AutoCoverEight(GameState state, Player player, Card eight, List<IGameEvent> events)
    {
        while (true)
        {
            EnsureDrawPile(state, events);
            if (state.DrawPile.Count == 0) break; // no cards left at all

            var drawn = PopDraw(state);
            player.Hand.Add(drawn);
            events.Add(new CardDrawn(player.Id, 1));

            if (drawn is Card dc && (dc.Suit == eight.Suit || dc.Rank == Rank.Eight))
            {
                // Play the cover
                player.Hand.Remove(drawn);
                state.TablePile.Add(drawn);
                events.Add(new CardPlayed(player.Id, [drawn]));

                // If cover is another 8, chain
                if (dc.Rank == Rank.Eight)
                {
                    bool hasCover = player.Hand.Any(c =>
                        c is Card hc && (hc.Suit == dc.Suit || hc.Rank == Rank.Eight));
                    if (hasCover)
                    {
                        state.AwaitingEightCover = true;
                        return;
                    }
                    AutoCoverEight(state, player, dc, events);
                }
                return;
            }
        }
    }

    private static void HandleJokerOnJokerCancel(GameState state, List<IGameEvent> events, bool isRedJoker)
    {
        // Check if the card directly beneath the just-played Joker was also a Joker
        if (state.TablePile.Count >= 2 && state.TablePile[^2] is JokerCard)
        {
            events.Add(new JokerCancelled(state.CurrentPlayer.Id));
            if (isRedJoker)
            {
                // Red played on Black → cancel Black's "all draw 6" (already applied — can't undo)
                // In practice, to make Joker-on-Joker fair the Black Joker draw should be
                // deferred, but for MVP the cancel only fully works for Red Joker's extra turns.
            }
            else
            {
                // Black played on Red → cancel Red's remaining extra turns
                state.ExtraTurns = 0;
            }
        }
    }

    // ────────────────────────────────────────────
    //  Turn management
    // ────────────────────────────────────────────

    private void AdvanceTurn(GameState state, List<IGameEvent> events)
    {
        state.HasDrawnThisTurn = false;

        // Red Joker: current player gets extra turns
        if (state.ExtraTurns > 0)
        {
            state.ExtraTurns--;
            events.Add(new TurnChanged(state.CurrentPlayer.Id));
            return;
        }

        // Normal advance
        state.CurrentPlayerIndex = GetNextActivePlayerIndex(state, state.CurrentPlayerIndex);
        events.Add(new TurnChanged(state.CurrentPlayer.Id));
    }

    private static int GetNextActivePlayerIndex(GameState state, int currentIndex)
    {
        int count = state.Players.Count;
        int next = (currentIndex + 1) % count;
        int safety = count;
        while (state.Players[next].IsEliminated && --safety > 0)
            next = (next + 1) % count;
        return next;
    }

    // ────────────────────────────────────────────
    //  Draw-pile management
    // ────────────────────────────────────────────

    private List<IPlayableCard> DrawCardsFromPile(
        GameState state, Player player, int count, List<IGameEvent> events)
    {
        var drawn = new List<IPlayableCard>();
        for (int i = 0; i < count; i++)
        {
            EnsureDrawPile(state, events);
            if (state.DrawPile.Count == 0) break;

            var card = PopDraw(state);
            player.Hand.Add(card);
            drawn.Add(card);
        }
        if (drawn.Count > 0)
            events.Add(new CardDrawn(player.Id, drawn.Count));
        return drawn;
    }

    private static void EnsureDrawPile(GameState state, List<IGameEvent> events)
    {
        if (state.DrawPile.Count > 0) return;
        if (state.TablePile.Count <= 1) return; // keep top card

        var topCard = state.TablePile[^1];
        var toShuffle = state.TablePile.GetRange(0, state.TablePile.Count - 1);
        state.TablePile.Clear();
        state.TablePile.Add(topCard);

        state.DrawPile.AddRange(toShuffle);
        DeckFactory.Shuffle(state.DrawPile);

        state.DeckFlipMultiplier++;
        events.Add(new DeckReshuffled(state.DeckFlipMultiplier));
    }

    private static IPlayableCard PopDraw(GameState state)
    {
        var card = state.DrawPile[^1];
        state.DrawPile.RemoveAt(state.DrawPile.Count - 1);
        return card;
    }

    // ────────────────────────────────────────────
    //  Round / game end
    // ────────────────────────────────────────────

    private List<IGameEvent> EndRound(GameState state, string? winnerPlayerId, List<IGameEvent> events)
    {
        state.Phase = GamePhase.RoundOver;
        var scores = new Dictionary<string, int>();

        foreach (var p in state.Players)
        {
            if (p.IsEliminated) continue;

            int roundPoints;
            if (p.Id == winnerPlayerId)
            {
                // Winner normally scores 0.
                // If they ended the round by playing a Jack → −20 per such Jack.
                roundPoints = 0;
                if (state.TopTableCard is Card { Rank: Rank.Jack })
                    roundPoints = -20;
            }
            else
            {
                roundPoints = p.HandPointValue * state.DeckFlipMultiplier;
            }

            p.ApplyScore(roundPoints);
            scores[p.Id] = p.Score;
            events.Add(new ScoreUpdated(p.Id, p.Score));

            if (p.IsEliminated)
                events.Add(new PlayerEliminated(p.Id));
        }

        events.Add(new RoundEnded(scores));

        // Only one active player left → game over
        if (state.Players.Count(p => !p.IsEliminated) <= 1)
            state.Phase = GamePhase.GameOver;

        return events;
    }

    // ────────────────────────────────────────────
    //  Misc helpers
    // ────────────────────────────────────────────

    private static void ResetPendingEffects(GameState state)
    {
        state.DeclaredSuit = null;
        state.IsUgolovshchina = false;
        state.PendingDraws = 0;
        state.SkipCount = 0;
        state.ExtraTurns = 0;
        state.ConsecutiveQueenCount = 0;
        state.ConsecutiveQueenSuits.Clear();
        state.DeckFlipMultiplier = 1;
        state.KingOfHeartsActive = false;
        state.SkipRequiresDraw = false;
        state.AwaitingEightCover = false;
        state.AwaitingNineDiscard = false;
        state.HasDrawnThisTurn = false;
        state.IsDealerCoverPhase = false;
    }

    private static bool SameRankAs(IPlayableCard a, IPlayableCard b)
    {
        return a is Card ca && b is Card cb && ca.Rank == cb.Rank;
    }
}
