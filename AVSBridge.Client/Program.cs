using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AVSBridge.Client;
using AVSBridge.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// GameHubService is scoped (one per user session in WASM = effectively singleton)
builder.Services.AddScoped<GameHubService>();
builder.Services.AddScoped<LocalizationService>();

await builder.Build().RunAsync();
