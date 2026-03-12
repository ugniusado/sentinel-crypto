namespace SentinelCrypto.Server.Models;

public sealed record PriceUpdate
{
    public required string Symbol { get; init; }
    public required decimal Price { get; init; }
    public required decimal PriceChangePercent { get; init; }
    public required decimal Volume { get; init; }
    public required decimal High24h { get; init; }
    public required decimal Low24h { get; init; }
    public required DateTime Timestamp { get; init; }
}

public sealed record AggregatedUpdate
{
    public required string Symbol { get; init; }
    public required decimal LatestPrice { get; init; }
    public required decimal PriceChangePercent { get; init; }
    public required decimal Volume { get; init; }
    public required decimal High24h { get; init; }
    public required decimal Low24h { get; init; }

    /// <summary>Rolling 1-second volatility (std dev of prices in window).</summary>
    public required decimal Volatility { get; init; }

    /// <summary>True if coin dropped >3% in 5 minutes — triggers panic UI.</summary>
    public required bool IsPanic { get; init; }

    public required DateTime Timestamp { get; init; }
}

/// <summary>Raw tick deserialized from Binance individual symbol ticker stream.</summary>
public sealed class BinanceTicker
{
    // Binance field names are single letters — we deserialize manually
    public string? e { get; set; }  // event type
    public string? s { get; set; }  // symbol
    public string? c { get; set; }  // close/last price
    public string? P { get; set; }  // price change percent
    public string? v { get; set; }  // total traded base asset volume
    public string? h { get; set; }  // high price
    public string? l { get; set; }  // low price
    public long E { get; set; }     // event time (unix ms)
}
