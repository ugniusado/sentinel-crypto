using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using SentinelCrypto.Server.Models;

namespace SentinelCrypto.Server.Services;

public class MlForecastService
{
    // Window size (seasonality period) per interval
    private static int WindowSize(string interval, int seriesLen)
    {
        var w = interval switch
        {
            "15m" => 96,
            "4h"  => 42,
            "1d"  => 7,
            "1w"  => 4,
            _     => 24   // 1h default
        };
        // SSA constraint: windowSize <= seriesLength / 2
        return Math.Min(w, seriesLen / 2 - 1);
    }

    public Task<ForecastResponse> ForecastAsync(ForecastRequest req) =>
        Task.Run(() => Forecast(req));

    private ForecastResponse Forecast(ForecastRequest req)
    {
        var prices = req.Prices;
        if (prices.Length < 12)
            return new ForecastResponse([], [], []);

        var ml = new MLContext(seed: 42);
        var data = prices.Select(p => new PriceInput { Value = p });
        var idv  = ml.Data.LoadFromEnumerable(data);

        var windowSize = WindowSize(req.Interval, prices.Length);

        var pipeline = ml.Forecasting.ForecastBySsa(
            outputColumnName:             nameof(PriceForecast.Forecast),
            inputColumnName:              nameof(PriceInput.Value),
            windowSize:                   windowSize,
            seriesLength:                 prices.Length,
            trainSize:                    prices.Length,
            horizon:                      req.Horizon,
            confidenceLevel:              0.90f,
            confidenceLowerBoundColumn:   nameof(PriceForecast.LowerBound),
            confidenceUpperBoundColumn:   nameof(PriceForecast.UpperBound));

        var model  = pipeline.Fit(idv);
        var engine = model.CreateTimeSeriesEngine<PriceInput, PriceForecast>(ml);
        var result = engine.Predict();

        return new ForecastResponse(result.Forecast, result.UpperBound, result.LowerBound);
    }
}
