using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SentinelCrypto.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<SentinelCrypto.Client.App>("#app");

builder.Services.AddSingleton<PriceStateService>();
builder.Services.AddSingleton<CryptoSignalRService>();
builder.Services.AddSingleton<DashboardStateService>();

builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

var signalR = app.Services.GetRequiredService<CryptoSignalRService>();
_ = signalR.StartAsync();

await app.RunAsync();
