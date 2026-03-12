namespace SentinelCrypto.Client.Models;

public sealed class CoinModel
{
    private const int MaxSparklinePoints = 30;
    private const int MaxVolumeHistory   = 20;

    public required string Symbol { get; set; }
    public decimal Price { get; set; }
    public decimal PreviousPrice { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal Volume { get; set; }
    public decimal High24h { get; set; }
    public decimal Low24h { get; set; }
    public decimal Volatility { get; set; }
    public bool    IsPanic { get; set; }
    public DateTime LastUpdated { get; set; }

    // ── Sparkline (30-second samples, 30 points = 15 min history) ─────────
    public List<decimal> SparklineData { get; } = [];
    public DateTime LastSparklineSample { get; private set; } = DateTime.MinValue;

    public void TryAddSparklinePoint(decimal price, DateTime now)
    {
        if ((now - LastSparklineSample).TotalSeconds < 30) return;
        SparklineData.Add(price);
        if (SparklineData.Count > MaxSparklinePoints) SparklineData.RemoveAt(0);
        LastSparklineSample = now;
    }

    // ── Relative Volume ───────────────────────────────────────────────────
    private readonly Queue<decimal> _volumeHistory = new();
    public decimal RelativeVolume { get; private set; } = 1m;

    public void UpdateVolumeHistory(decimal volume)
    {
        _volumeHistory.Enqueue(volume);
        if (_volumeHistory.Count > MaxVolumeHistory) _volumeHistory.Dequeue();
        if (_volumeHistory.Count > 0 && volume > 0)
            RelativeVolume = volume / _volumeHistory.Average();
    }

    // ── Render gating ────────────────────────────────────────────────────
    public bool HasSignificantChange(decimal threshold = 0.0001m)
    {
        if (PreviousPrice == 0) return true;
        return Math.Abs((Price - PreviousPrice) / PreviousPrice) >= threshold;
    }

    // ── Layout tier ───────────────────────────────────────────────────────
    public CardSize Size => Symbol switch
    {
        "BTCUSDT" or "ETHUSDT"              => CardSize.Large,
        "BNBUSDT" or "SOLUSDT" or "XRPUSDT" => CardSize.Medium,
        _                                    => CardSize.Small
    };
}

public enum CardSize { Small, Medium, Large }
