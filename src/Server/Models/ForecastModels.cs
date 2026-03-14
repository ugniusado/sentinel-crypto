namespace SentinelCrypto.Server.Models;

public record ForecastRequest(float[] Prices, string Interval = "1h", int Horizon = 20);
public record ForecastResponse(float[] Predicted, float[] UpperBound, float[] LowerBound);

// ML.NET input/output schema
public class PriceInput  { public float Value { get; set; } }
public class PriceForecast
{
    public float[] Forecast   { get; set; } = [];
    public float[] LowerBound { get; set; } = [];
    public float[] UpperBound { get; set; } = [];
}
