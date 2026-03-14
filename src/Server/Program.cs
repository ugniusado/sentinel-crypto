using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SentinelCrypto.Server.Hubs;
using SentinelCrypto.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────

// Channel: decouples Binance socket from aggregator
builder.Services.AddSingleton<PriceChannelService>();

// Background workers
builder.Services.AddSingleton<PriceAggregatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceAggregatorService>());
builder.Services.AddHostedService<BinanceWebSocketService>();

// SignalR + optional Redis backplane
var signalR = builder.Services.AddSignalR(opts =>
{
    opts.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    signalR.AddStackExchangeRedis(redisConn, opts =>
        opts.Configuration.ChannelPrefix =
            StackExchange.Redis.RedisChannel.Literal("sentinel-crypto"));
}

// CORS — allow the Blazor WASM origin in development
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173", "https://localhost:5173"];

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // required for SignalR
    });
});

// Health checks
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Server is running"))
    .AddCheck<BinanceConnectivityCheck>("binance-websocket");

if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services
        .AddHealthChecks()
        .AddRedis(redisConn, name: "redis");
}

// Controllers (REST API)
builder.Services.AddControllers();
builder.Services.AddSingleton<MlForecastService>();

// OpenTelemetry tracing (exports via OTLP — Jaeger 1.35+ accepts OTLP on port 4317)
var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("sentinel-crypto-server"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
        }
    });

// ── App ──────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.UseCors();

app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await ctx.Response.WriteAsync(result);
    }
});

app.MapHub<CryptoHub>("/hubs/crypto");
app.MapControllers();

// Serve the Blazor WASM client (dev hot-reload + production static files)
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// ── Health check implementation ───────────────────────────────────────────────

public sealed class BinanceConnectivityCheck : IHealthCheck
{
    private readonly PriceAggregatorService _aggregator;

    public BinanceConnectivityCheck(PriceAggregatorService aggregator)
        => _aggregator = aggregator;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = _aggregator.GetCurrentSnapshot();
        return Task.FromResult(snapshot.Count > 0
            ? HealthCheckResult.Healthy($"Receiving data for {snapshot.Count} symbols")
            : HealthCheckResult.Degraded("No data received yet — Binance socket may be connecting"));
    }
}
