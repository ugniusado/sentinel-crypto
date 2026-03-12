using SentinelCrypto.Client.Models;

namespace SentinelCrypto.Client.Services;

public sealed class PriceStateService
{
    private readonly Dictionary<string, CoinModel> _coins = [];

    public event Action? OnPricesUpdated;

    public IReadOnlyDictionary<string, CoinModel> Coins => _coins;

    /// <summary>Full ordered list — Large first, then alpha within tier.</summary>
    public IEnumerable<CoinModel> OrderedCoins =>
        _coins.Values.OrderBy(c => c.Size).ThenBy(c => c.Symbol);

    public void ApplyUpdate(AggregatedUpdateDto update)
    {
        if (!_coins.TryGetValue(update.Symbol, out var coin))
        {
            coin = new CoinModel { Symbol = update.Symbol };
            _coins[update.Symbol] = coin;
        }

        coin.PreviousPrice = coin.Price;
        coin.Price         = update.LatestPrice;
        coin.PriceChangePercent = update.PriceChangePercent;
        coin.Volume        = update.Volume;
        coin.High24h       = update.High24h;
        coin.Low24h        = update.Low24h;
        coin.Volatility    = update.Volatility;
        coin.IsPanic       = update.IsPanic;
        coin.LastUpdated   = update.Timestamp;

        var now = update.Timestamp == default ? DateTime.UtcNow : update.Timestamp;
        coin.TryAddSparklinePoint(update.LatestPrice, now);
        coin.UpdateVolumeHistory(update.Volume);

        OnPricesUpdated?.Invoke();
    }

    public void ApplyInitialState(IEnumerable<AggregatedUpdateDto> updates)
    {
        foreach (var u in updates) ApplyUpdate(u);
    }
}

public sealed class AggregatedUpdateDto
{
    public string  Symbol             { get; set; } = "";
    public decimal LatestPrice        { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal Volume             { get; set; }
    public decimal High24h            { get; set; }
    public decimal Low24h             { get; set; }
    public decimal Volatility         { get; set; }
    public bool    IsPanic            { get; set; }
    public DateTime Timestamp         { get; set; }
}
