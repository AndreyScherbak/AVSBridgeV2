using System.Text.Json.Serialization;
using AVSBridge.Server.Hubs;
using AVSBridge.Server.Services;
using AVSBridge.Shared.Engine;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<GameEngine>();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => builder.Environment.IsDevelopment())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ── Middleware ──
app.UseCors();

// ── Endpoints ──
app.MapHub<GameHub>("/gamehub");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program;
