using System.Text.Json;
using System.Text.Json.Serialization;
using AVSBridge.Shared.DTOs;
using AVSBridge.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace AVSBridge.Client.Services;

/// <summary>
/// Wraps SignalR HubConnection for type-safe game communication.
/// Manages connection lifecycle, sends commands, and raises events for UI consumption.
/// </summary>
public sealed class GameHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly JsonSerializerOptions _jsonOptions;

    // ── Connection state ──
    public string? RoomCode { get; private set; }
    public string? PlayerId { get; private set; }
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    // ── Events for UI binding ──
    public event Action<GameStateDto, HandDto>? OnGameStateUpdated;
    public event Action<string>? OnError;
    public event Action<string, string>? OnRoomJoined;        // roomCode, playerId
    public event Action<string>? OnPlayerJoined;
    public event Action<string>? OnPlayerLeft;
    public event Action<string>? OnGameStarted;
    public event Action<string>? OnTurnChanged;
    public event Action<string, int>? OnCardDrawn;
    public event Action<Suit, bool>? OnSuitDeclared;
    public event Action<Dictionary<string, int>>? OnRoundEnded;
    public event Action<string>? OnPlayerEliminated;
    public event Action<int>? OnDeckReshuffled;
    public event Action? OnConsecutiveQueensRoundEnd;
    public event Action<string>? OnJokerCancelled;
    public event Action? OnReconnecting;
    public event Action? OnReconnected;
    public event Action? OnDisconnected;

    public GameHubService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Connect to the SignalR hub. Call once before any game actions.
    /// </summary>
    public async Task ConnectAsync(string hubUrl)
    {
        if (_connection is not null)
            await DisposeAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();

        RegisterHandlers();

        _connection.Reconnecting += _ => { OnReconnecting?.Invoke(); return Task.CompletedTask; };
        _connection.Reconnected += _ => { OnReconnected?.Invoke(); return Task.CompletedTask; };
        _connection.Closed += _ => { OnDisconnected?.Invoke(); return Task.CompletedTask; };

        await _connection.StartAsync();
    }

    private void RegisterHandlers()
    {
        if (_connection is null) return;

        _connection.On<string, string>("RoomJoined", (roomCode, playerId) =>
        {
            RoomCode = roomCode;
            PlayerId = playerId;
            OnRoomJoined?.Invoke(roomCode, playerId);
        });

        _connection.On<GameStateDto, HandDto>("GameStateUpdated", (state, hand) =>
        {
            OnGameStateUpdated?.Invoke(state, hand);
        });

        _connection.On<string>("Error", msg => OnError?.Invoke(msg));
        _connection.On<string>("PlayerJoined", name => OnPlayerJoined?.Invoke(name));
        _connection.On<string>("PlayerLeft", name => OnPlayerLeft?.Invoke(name));
        _connection.On<string>("GameStarted", code => OnGameStarted?.Invoke(code));
        _connection.On<string>("TurnChanged", id => OnTurnChanged?.Invoke(id));
        _connection.On<string, int>("CardDrawn", (id, count) => OnCardDrawn?.Invoke(id, count));
        _connection.On<Suit, bool>("SuitDeclared", (suit, ugo) => OnSuitDeclared?.Invoke(suit, ugo));
        _connection.On<Dictionary<string, int>>("RoundEnded", scores => OnRoundEnded?.Invoke(scores));
        _connection.On<string>("PlayerEliminated", id => OnPlayerEliminated?.Invoke(id));
        _connection.On<int>("DeckReshuffled", mult => OnDeckReshuffled?.Invoke(mult));
        _connection.On("ConsecutiveQueensRoundEnd", () => OnConsecutiveQueensRoundEnd?.Invoke());
        _connection.On<string>("JokerCancelled", id => OnJokerCancelled?.Invoke(id));
    }

    // ── Commands ──

    public async Task<string> CreateRoomAsync(string playerName, bool extended = false)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string>("CreateRoom", playerName, extended);
    }

    public async Task JoinRoomAsync(string roomCode, string playerName)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("JoinRoom", roomCode, playerName);
    }

    public async Task StartGameAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("StartGame", RoomCode);
    }

    public async Task PlayCardsAsync(List<IPlayableCard> cards)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("PlayCards", RoomCode, cards);
    }

    public async Task DrawCardAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("DrawCard", RoomCode);
    }

    public async Task AcceptPenaltyAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("AcceptPenalty", RoomCode);
    }

    public async Task StartNewRoundAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("StartNewRound", RoomCode);
    }

    public async Task DeclareJackSuitAsync(Suit suit)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("DeclareJackSuit", RoomCode, suit);
    }

    public async Task DiscardCardAsync(IPlayableCard card)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("DiscardCard", RoomCode, card);
    }

    public async Task PassTurnAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("PassTurn", RoomCode);
    }

    public async Task SkipDealerCoverAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SkipDealerCover", RoomCode);
    }

    public async Task LeaveRoomAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("LeaveRoom", RoomCode);
        RoomCode = null;
        PlayerId = null;
    }

    private void EnsureConnected()
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Not connected to hub.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
