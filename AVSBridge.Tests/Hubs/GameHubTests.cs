using System.Text.Json;
using System.Text.Json.Serialization;
using AVSBridge.Shared.DTOs;
using AVSBridge.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace AVSBridge.Tests.Hubs;

/// <summary>
/// Integration tests for GameHub using a real in-memory server.
/// Each test spins up a WebApplicationFactory, connects SignalR clients,
/// and verifies end-to-end hub behaviour.
/// </summary>
public sealed class GameHubTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<HubConnection> _connections = [];

    public GameHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var conn in _connections)
        {
            if (conn.State != HubConnectionState.Disconnected)
                await conn.DisposeAsync();
        }
    }

    // ── Helper ──────────────────────────────────────

    private async Task<HubConnection> CreateHubConnection()
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}gamehub", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        _connections.Add(connection);
        await connection.StartAsync();
        return connection;
    }

    private static async Task<T> WaitForMessage<T>(HubConnection connection, string method, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<T>();
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        cts.Token.Register(() => tcs.TrySetCanceled());

        connection.On<T>(method, value =>
        {
            tcs.TrySetResult(value);
        });

        return await tcs.Task;
    }

    private static async Task<(T1, T2)> WaitForMessage<T1, T2>(HubConnection connection, string method, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<(T1, T2)>();
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        cts.Token.Register(() => tcs.TrySetCanceled());

        connection.On<T1, T2>(method, (v1, v2) =>
        {
            tcs.TrySetResult((v1, v2));
        });

        return await tcs.Task;
    }

    // ── CreateRoom ──────────────────────────────────

    [Fact]
    public async Task CreateRoom_ReturnsRoomCode()
    {
        var conn = await CreateHubConnection();
        var roomCode = await conn.InvokeAsync<string>("CreateRoom", "Alice", false);

        Assert.NotNull(roomCode);
        Assert.Equal(6, roomCode.Length);
    }

    [Fact]
    public async Task CreateRoom_SendsRoomJoinedToCaller()
    {
        var conn = await CreateHubConnection();

        var roomJoinedTask = WaitForMessage<string, string>(conn, "RoomJoined");
        _ = conn.InvokeAsync<string>("CreateRoom", "Alice", false);

        var (roomCode, playerId) = await roomJoinedTask;
        Assert.NotNull(roomCode);
        Assert.NotNull(playerId);
    }

    [Fact]
    public async Task CreateRoom_SendsGameStateUpdated()
    {
        var conn = await CreateHubConnection();

        var stateTask = WaitForMessage<JsonElement, JsonElement>(conn, "GameStateUpdated");
        _ = conn.InvokeAsync<string>("CreateRoom", "Alice", false);

        var (state, hand) = await stateTask;
        Assert.Equal("WaitingForPlayers", state.GetProperty("phase").GetString());
    }

    // ── JoinRoom ────────────────────────────────────

    [Fact]
    public async Task JoinRoom_SecondPlayerJoins()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        var joinedTask = WaitForMessage<string, string>(guest, "RoomJoined");
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");
        var (joinedRoom, _) = await joinedTask;

        Assert.Equal(roomCode, joinedRoom);
    }

    [Fact]
    public async Task JoinRoom_InvalidCode_SendsError()
    {
        var conn = await CreateHubConnection();
        var errorTask = WaitForMessage<string>(conn, "Error");
        await conn.InvokeAsync("JoinRoom", "ZZZZZZ", "Bob");
        var error = await errorTask;

        Assert.Contains("Unable to join", error);
    }

    [Fact]
    public async Task JoinRoom_HostReceivesPlayerJoinedEvent()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var playerJoinedTask = WaitForMessage<string>(host, "PlayerJoined");

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        var joinedName = await playerJoinedTask;
        Assert.Equal("Bob", joinedName);
    }

    // ── StartGame ───────────────────────────────────

    [Fact]
    public async Task StartGame_WithTwoPlayers_DealsCards()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        // Wait for GameStarted event on guest side
        var gameStartedTask = WaitForMessage<string>(guest, "GameStarted");
        await host.InvokeAsync("StartGame", roomCode);

        var startedRoomCode = await gameStartedTask;
        Assert.Equal(roomCode, startedRoomCode);
    }

    [Fact]
    public async Task StartGame_EachPlayerReceivesOwnHand()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        // Capture the GameStateUpdated that follows StartGame
        var hostStateTask = WaitForMessage<JsonElement, JsonElement>(host, "GameStateUpdated");
        var guestStateTask = WaitForMessage<JsonElement, JsonElement>(guest, "GameStateUpdated");

        await host.InvokeAsync("StartGame", roomCode);

        var (hostState, hostHand) = await hostStateTask;
        var (guestState, guestHand) = await guestStateTask;

        // Both should see InProgress phase
        Assert.Equal("InProgress", hostState.GetProperty("phase").GetString());
        Assert.Equal("InProgress", guestState.GetProperty("phase").GetString());

        // Each player should have received their own hand (non-empty)
        Assert.True(hostHand.GetProperty("cards").GetArrayLength() > 0);
        Assert.True(guestHand.GetProperty("cards").GetArrayLength() > 0);
    }

    // ── Error handling ──────────────────────────────

    [Fact]
    public async Task StartGame_WithOnePlayer_SendsError()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var errorTask = WaitForMessage<string>(host, "Error");
        await host.InvokeAsync("StartGame", roomCode);
        var error = await errorTask;

        Assert.Contains("2", error); // "Need 2–10 players"
    }

    [Fact]
    public async Task PlayCards_WhenNotYourTurn_SendsError()
    {
        // Setup: 2 players, start game
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        await host.InvokeAsync("StartGame", roomCode);
        // Allow state to propagate
        await Task.Delay(200);

        // Try to play from both connections — one should get an error
        // since only one player's turn. We send an invalid card from guest.
        var errorTask = WaitForMessage<string>(guest, "Error");
        var fakeCard = new Card(Suit.Hearts, Rank.Two);
        await guest.InvokeAsync("PlayCards", roomCode, new List<IPlayableCard> { fakeCard });

        var error = await errorTask;
        Assert.NotNull(error);
    }

    // ── LeaveRoom ───────────────────────────────────

    [Fact]
    public async Task LeaveRoom_HostReceivesPlayerLeftEvent()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        var leftTask = WaitForMessage<string>(host, "PlayerLeft");
        await guest.InvokeAsync("LeaveRoom", roomCode);

        var leftName = await leftTask;
        Assert.Equal("Bob", leftName);
    }

    // ── Disconnection ───────────────────────────────

    [Fact]
    public async Task Disconnect_DuringWaiting_RemovesPlayer()
    {
        var host = await CreateHubConnection();
        var roomCode = await host.InvokeAsync<string>("CreateRoom", "Alice", false);

        var guest = await CreateHubConnection();
        await guest.InvokeAsync("JoinRoom", roomCode, "Bob");

        var leftTask = WaitForMessage<string>(host, "PlayerLeft");
        await guest.DisposeAsync();

        var leftName = await leftTask;
        Assert.Equal("Bob", leftName);
    }
}
