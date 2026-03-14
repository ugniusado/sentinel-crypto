using System.Net.Http.Json;
using System.Text.Json;
using SentinelCrypto.Client.Models;

namespace SentinelCrypto.Client.Services;

/// <summary>Fetches OHLCV kline data from the Binance public REST API.</summary>
public class BinanceHistoricalService
{
    // Static client — no base-address restriction so we can reach api.binance.com
    private static readonly HttpClient _http = new();

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<KlineData>> GetKlinesAsync(
        string symbol, string interval = "1h", int limit = 200)
    {
        var url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
        var raw = await _http.GetFromJsonAsync<JsonElement[][]>(url, _json);
        if (raw is null) return [];

        return raw.Select(k => new KlineData
        {
            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(k[0].GetInt64()).UtcDateTime,
            Open     = decimal.Parse(k[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            High     = decimal.Parse(k[2].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            Low      = decimal.Parse(k[3].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            Close    = decimal.Parse(k[4].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            Volume   = decimal.Parse(k[5].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
        }).ToList();
    }
}
