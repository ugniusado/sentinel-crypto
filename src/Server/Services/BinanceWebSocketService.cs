using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SentinelCrypto.Server.Models;

namespace SentinelCrypto.Server.Services;

/// <summary>
/// BackgroundService that connects to Binance's combined stream endpoint,
/// reads raw ticker ticks, and writes them into the PriceChannelService buffer.
///
/// Stream format: wss://stream.binance.com:9443/stream?streams=btcusdt@ticker/ethusdt@ticker/...
/// </summary>
public sealed class BinanceWebSocketService : BackgroundService
{
    // Symbols to track — extend this list freely
    private static readonly string[] Symbols =
    [
        "btcusdt", "ethusdt", "bnbusdt", "solusdt", "xrpusdt",
        "adausdt", "dogeusdt", "avaxusdt", "linkusdt", "maticusdt"
    ];

    private readonly PriceChannelService _channel;
    private readonly ILogger<BinanceWebSocketService> _logger;
    private readonly IConfiguration _config;

    // Tracks 5-minute price history for panic detection per symbol
    private readonly Dictionary<string, Queue<(DateTime Time, decimal Price)>> _priceHistory = [];

    public BinanceWebSocketService(
        PriceChannelService channel,
        ILogger<BinanceWebSocketService> logger,
        IConfiguration config)
    {
        _channel = channel;
        _logger = logger;
        _config = config;

        foreach (var sym in Symbols)
            _priceHistory[sym.ToUpperInvariant()] = new Queue<(DateTime, decimal)>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Binance WebSocket disconnected. Reconnecting in 5s...");
                await Task.Delay(5_000, stoppingToken);
            }
        }

        _channel.Writer.TryComplete();
    }

    private async Task ConnectAndReadAsync(CancellationToken ct)
    {
        var streams = string.Join("/", Symbols.Select(s => $"{s}@ticker"));
        var baseUrl = _config["Binance:StreamUrl"] ?? "wss://stream.binance.com:9443";
        var uri = new Uri($"{baseUrl}/stream?streams={streams}");

        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        _logger.LogInformation("Connecting to Binance stream: {Uri}", uri);
        await ws.ConnectAsync(uri, ct);
        _logger.LogInformation("Connected to Binance stream");

        // Receive buffer — 8 KB is plenty for a single ticker JSON payload
        var buffer = new byte[8192];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new System.IO.MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            ms.Seek(0, System.IO.SeekOrigin.Begin);
            ParseAndEnqueue(ms.ToArray());
        }
    }

    private void ParseAndEnqueue(byte[] data)
    {
        try
        {
            // Combined stream envelope: { "stream": "btcusdt@ticker", "data": { ... } }
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var tickerData))
                return;

            var ticker = JsonSerializer.Deserialize<BinanceTicker>(tickerData.GetRawText());
            if (ticker?.s is null || ticker.c is null)
                return;

            var symbol = ticker.s.ToUpperInvariant();
            var price = decimal.Parse(ticker.c, System.Globalization.CultureInfo.InvariantCulture);
            var now = DateTime.UtcNow;

            // Maintain rolling 5-minute window for panic detection
            if (_priceHistory.TryGetValue(symbol, out var history))
            {
                history.Enqueue((now, price));
                var cutoff = now.AddMinutes(-5);
                while (history.Count > 0 && history.Peek().Time < cutoff)
                    history.Dequeue();
            }

            var update = new PriceUpdate
            {
                Symbol = symbol,
                Price = price,
                PriceChangePercent = decimal.TryParse(ticker.P, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct) ? pct : 0m,
                Volume = decimal.TryParse(ticker.v, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var vol) ? vol : 0m,
                High24h = decimal.TryParse(ticker.h, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var high) ? high : 0m,
                Low24h = decimal.TryParse(ticker.l, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var low) ? low : 0m,
                Timestamp = now
            };

            _channel.Writer.TryWrite(update);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse Binance message");
        }
    }
}
