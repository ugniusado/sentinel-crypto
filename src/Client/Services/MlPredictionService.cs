using System.Net.Http.Json;

namespace SentinelCrypto.Client.Services;

public record MlForecastResult(float[] Predicted, float[] UpperBound, float[] LowerBound);

public class MlPredictionService(HttpClient http)
{
    public async Task<MlForecastResult?> ForecastAsync(
        double[] prices, string interval, int horizon = 20)
    {
        try
        {
            var req = new
            {
                Prices   = prices.Select(p => (float)p).ToArray(),
                Interval = interval,
                Horizon  = horizon
            };
            var resp = await http.PostAsJsonAsync("/api/forecast", req);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<MlForecastResult>()
                : null;
        }
        catch { return null; }
    }
}
