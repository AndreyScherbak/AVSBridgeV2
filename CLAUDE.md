# AVS Bridge — CLAUDE.md

## 1. Project Overview

- **Name:** AVS Bridge (working title)
- **Type:** Real-time multiplayer card game (2–10 players)
- **Stack:** ASP.NET Core + SignalR (server), Blazor WebAssembly (client), .NET Shared library
- **Target Framework:** net10.0 (SDK 10.0.200 installed locally)
- **Goal:** MVP playable over the internet with friends

## 2. Solution Structure

```
AVSBridgeV2/
├── AVSBridge.sln
├── CLAUDE.md                ← this file
├── .gitignore
├── AVSBridge.Server/        — ASP.NET Core Web API + SignalR Hub (GameHub)
├── AVSBridge.Client/        — Blazor WebAssembly frontend
├── AVSBridge.Shared/        — Domain models, DTOs, game logic interfaces (shared)
├── AVSBridge.Tests/         — xUnit tests (primarily for GameEngine)
├── Legacy_Project/          — READ-ONLY reference (Unity prototype), gitignored
└── Telegram_Info/           — READ-ONLY reference (game design notes), gitignored
```

**Project references:**
- Server → Shared
- Client → Shared
- Tests → Shared, Server

> **IMPORTANT:** Never modify files inside `Legacy_Project/` or `Telegram_Info/`. They are reference-only.

## 3. Architecture Decisions

- All game state lives exclusively on the **Server** (clients never hold authoritative state).
- Clients receive only their own hand + public game state (prevents cheating).
- **SignalR** used for real-time bidirectional communication via `GameHub`.
- Game validation happens on the Server; Client may mirror validation for instant UI feedback.
- In-memory state for MVP: `Dictionary<string, GameState>` keyed by room code. No database.
- **GameEngine** is a pure stateless service: takes `GameState` + action → returns new `GameState` + events.

## 4. Localization

- **Default language:** Ukrainian (uk-UA)
- **Secondary language:** English (en-US)
- Use .NET resource files (`.resx`) under `AVSBridge.Client/Resources/`
- Language switcher must be accessible from the game UI at all times
- All user-facing strings go through `IStringLocalizer` — **no hardcoded UI text**

## 5. Game Rules (Source of Truth)

### Dealing

- Dealer gives every player **6 cards** (9 in extended mode).
- Dealer keeps **5 cards** (8 in extended) and places **1 card face-up** on the table.
- This counts as the dealer's turn. Dealer may immediately cover that card with any card of the **same rank**.
- Play continues **clockwise**.

### Valid Move

Play a card matching either the **RANK** or the **SUIT** of the top card on the table.

### Special Card Effects

| Card | Effect | Points |
|------|--------|--------|
| **2–5** | No effect | Face value (2–5 pts) |
| **6** | Next player draws **+2** cards. Stacks: two 6s = +4, three = +6, etc. | — |
| **7** | Next player(s) **skip turn AND draw 1** card. Stacks per player. | — |
| **8** | Must be covered immediately by the player who played it (by suit or another 8). If unable, keep drawing until a valid cover is found. | — |
| **9** | Player discards 1 card face-down under the deck. Multiple 9s in one turn = still only 1 discard. | — |
| **10** | No special effect. | +10 pts |
| **Jack** | Can be played on **any card of any suit**. Player declares the next required suit. If Jack ends the round: **−20 pts per Jack**. | +20 pts |
| **Queen** | Alone = no effect. **Four Queens played consecutively** = round ends immediately. With mixed deck: requires 4 Queens of **different suits**. | +10 pts |
| **King** | No special effect by default. **King of Hearts** specifically: next player draws **6 cards**, but only if King of Hearts is the **top card** (another King played on top cancels this). | +10 pts |
| **Ace** | Next player(s) **skip turn**, do NOT draw (unlike 7). | +15 pts |
| **Red Joker** | Player takes **3 consecutive turns** (following normal rules). Playable on any **red** card. Only red suits (Hearts, Diamonds) can follow. | +50 pts |
| **Black Joker** | **All other players draw 6 cards.** Playable on any **black** card. Only black suits (Clubs, Spades) can follow. | +50 pts |

### Jack Special Rule — "по уголовщині"

If the declared suit matches the Jack's own suit, the player must announce **"по уголовщині"** (by criminal law). This is a named game rule that should be tracked and displayed in the UI.

### Joker-on-Joker Rule

If any Joker is immediately covered by the **other** Joker in the **same turn**, the first Joker's effect is **cancelled**. Only the top Joker's effect applies.

### Scoring & Win/Loss

- A player who reaches **+125 points LOSES**; the round ends.
- Reaching exactly **+120 points**: score resets to **0** (burn rule).
- Reaching **−120 points**: score resets to **0** (negative burn rule).
- Reaching **−125 or below**: player does NOT win; game continues.
- Other players may continue until one winner remains.

### Deck Exhaustion

- When the draw pile runs out and a player needs to draw: keep the **top table card** in place, shuffle remaining table cards into a new draw pile.
- Each reshuffle **multiplies** all points earned this round: **×2** on 1st reshuffle, **×3** on 2nd, etc.

### Dealer Jack Exception

- If the dealer's initial face-up card is a Jack, **no suit is declared**. Next player plays matching the Jack's suit.
- Dealer may instead immediately play a second Jack and **then** declare a suit.

### No Valid Card

- If a player cannot play, they draw **1 card**. If it's playable, they may play it immediately. If not, turn passes to the next player.

## 6. Key Domain Types (AVSBridge.Shared)

```csharp
// Enums
enum Suit { Hearts, Diamonds, Clubs, Spades }
enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }
enum JokerColor { Red, Black }

// Core models (use record types)
record Card(Suit Suit, Rank Rank);
record JokerCard(JokerColor Color);   // or unify with Card via a discriminated union

// Entities
record Player(string Id, string Name, List<Card> Hand, int Score);

// Game state
record GameState(
    List<Player> Players,
    List<Card> DrawPile,
    List<Card> TablePile,
    int CurrentPlayerIndex,
    Suit? DeclaredSuit,          // set when Jack is played
    int PendingDraws,            // accumulated +2/+6 draws
    int SkipCount,               // accumulated skips from 7/Ace
    int DeckFlipMultiplier,      // starts at 1, increments on reshuffle
    bool IsExtendedMode          // 6 vs 9 starting cards
);

// Event DTOs
record CardPlayed(string PlayerId, List<Card> Cards);
record CardDrawn(string PlayerId, int Count);
record TurnSkipped(string PlayerId);
record SuitDeclared(string PlayerId, Suit Suit, bool IsUgolovshchina);
record RoundEnded(Dictionary<string, int> Scores);
record ScoreUpdated(string PlayerId, int NewScore);
record RoomCreated(string RoomCode);
record PlayerJoined(string PlayerId, string PlayerName);
```

## 7. SignalR Hub Methods (AVSBridge.Server — GameHub)

### Client → Server

| Method | Signature | Description |
|--------|-----------|-------------|
| `CreateRoom` | `(string playerName) → string roomCode` | Create a new room, return its code |
| `JoinRoom` | `(string roomCode, string playerName)` | Join existing room |
| `StartGame` | `(string roomCode)` | Host starts the game |
| `PlayCards` | `(string roomCode, List<Card> cards)` | Server validates, updates state, broadcasts |
| `DrawCard` | `(string roomCode)` | Draw from pile |
| `DeclareJackSuit` | `(string roomCode, Suit suit)` | After playing a Jack |
| `LeaveRoom` | `(string roomCode)` | Leave room gracefully |

### Server → Client (broadcast)

- `GameStarted(GameStateDto)` — initial state
- `CardPlayed(CardPlayedDto)` — a player played cards
- `CardDrawn(string playerId, int count)` — a player drew cards
- `TurnChanged(string currentPlayerId)` — whose turn it is
- `SuitDeclared(Suit suit, bool isUgolovshchina)` — Jack suit declaration
- `RoundEnded(ScoresDto)` — round finished
- `PlayerJoined(string name)` / `PlayerLeft(string name)`
- `Error(string message)` — validation errors

## 8. Development Commands

```bash
# Restore packages
dotnet restore

# Build entire solution
dotnet build

# Run tests
dotnet test

# Run server (from repo root)
dotnet run --project AVSBridge.Server

# Run client (from repo root)
dotnet run --project AVSBridge.Client

# Run both simultaneously (two terminals):
# Terminal 1: dotnet run --project AVSBridge.Server
# Terminal 2: dotnet run --project AVSBridge.Client
# Or configure Server to serve Client static files (preferred for production)
```

## 9. Coding Conventions

- **Language:** C# 12+, nullable enabled, implicit usings
- Use **record types** for immutable models (`Card`, all event DTOs)
- **GameEngine** must be a pure stateless service: `(GameState, Action) → (GameState, List<GameEvent>)`
- All public GameEngine methods must have corresponding **xUnit tests**
- No hardcoded strings in UI — all text via `IStringLocalizer`
- Follow standard C# naming: PascalCase for public members, camelCase for locals/parameters
- Prefer `sealed` classes where inheritance is not needed

## 10. Out of Scope for MVP

- Persistent database / player accounts
- Spectator mode
- Chat
- Mobile-native app (browser on mobile is fine)
- AI opponents
