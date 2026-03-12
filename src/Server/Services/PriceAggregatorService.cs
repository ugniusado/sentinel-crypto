using Microsoft.AspNetCore.SignalR;
using SentinelCrypto.Server.Hubs;
using SentinelCrypto.Server.Models;

namespace SentinelCrypto.Server.Services;

/// <summary>
/// BackgroundService that drains the PriceChannelService every 100ms.
///
/// Per flush window:
///   - Keeps the LATEST price per symbol (discards intermediate ticks).
///   - Calculates rolling 1-second volatility (std dev of prices seen in window).
///   - Detects "panic" if 5-minute price change exceeds -3%.
///   - Broadcasts AggregatedUpdate to all SignalR clients.
/// </summary>
public sealed class PriceAggregatorService : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);
    private const decimal PanicThreshold = -3m; // percent

    private readonly PriceChannelService _channel;
    private readonly IHubContext<CryptoHub, ICryptoClient> _hub;
    private readonly ILogger<PriceAggregatorService> _logger;

    // Snapshot visible to hub for initial-state delivery on new connections
    private readonly Dictionary<string, AggregatedUpdate> _snapshot = [];
    private readonly Lock _snapshotLock = new();

    // Per-symbol: track 5-min opening price for panic detection
    private readonly Dictionary<string, decimal> _fiveMinOpenPrice = [];
    private readonly Dictionary<string, DateTime> _fiveMinWindowStart = [];

    public PriceAggregatorService(
        PriceChannelService channel,
        IHubContext<CryptoHub, ICryptoClient> hub,
        ILogger<PriceAggregatorService> logger)
    {
        _channel = channel;
        _hub = hub;
        _logger = logger;
    }

    public Dictionary<string, AggregatedUpdate> GetCurrentSnapshot()
    {
        lock (_snapshotLock)
            return new Dictionary<string, AggregatedUpdate>(_snapshot);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Drain everything currently in the channel
            var batch = new Dictionary<string, List<PriceUpdate>>();

            while (_channel.Reader.TryRead(out var update))
            {
                if (!batch.TryGetValue(update.Symbol, out var list))
                {
                    list = [];
                    batch[update.Symbol] = list;
                }
                list.Add(update);
            }

            if (batch.Count == 0) continue;

            var aggregated = new List<AggregatedUpdate>(batch.Count);

            foreach (var (symbol, ticks) in batch)
            {
                var latest = ticks[^1]; // most recent tick wins

                // Volatility = population std dev of prices seen in this 100ms window
                var volatility = ComputeVolatility(ticks);

                // Panic detection: has the price dropped >3% in the past 5 minutes?
                var now = DateTime.UtcNow;
                if (!_fiveMinWindowStart.TryGetValue(symbol, out var windowStart)
                    || (now - windowStart) >= TimeSpan.FromMinutes(5))
                {
                    _fiveMinWindowStart[symbol] = now;
                    _fiveMinOpenPrice[symbol] = latest.Price;
                }

                var fiveMinChange = _fiveMinOpenPrice.TryGetValue(symbol, out var openPrice) && openPrice > 0
                    ? ((latest.Price - openPrice) / openPrice) * 100m
                    : 0m;

                var isPanic = fiveMinChange <= PanicThreshold;

                var update = new AggregatedUpdate
                {
                    Symbol = symbol,
                    LatestPrice = latest.Price,
                    PriceChangePercent = latest.PriceChangePercent,
                    Volume = latest.Volume,
                    High24h = latest.High24h,
                    Low24h = latest.Low24h,
                    Volatility = volatility,
                    IsPanic = isPanic,
                    Timestamp = latest.Timestamp
                };

                aggregated.Add(update);

                lock (_snapshotLock)
                    _snapshot[symbol] = update;
            }

            // Broadcast each update individually so clients can process them incrementally
            foreach (var update in aggregated)
            {
                try
                {
                    await _hub.Clients.All.ReceivePriceUpdate(update);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error broadcasting update for {Symbol}", update.Symbol);
                }
            }
        }
    }

    private static decimal ComputeVolatility(List<PriceUpdate> ticks)
    {
        if (ticks.Count <= 1) return 0m;

        var mean = ticks.Average(t => t.Price);
        var variance = ticks.Average(t => (t.Price - mean) * (t.Price - mean));
        return (decimal)Math.Sqrt((double)variance);
    }
}
