namespace SentinelCrypto.Client.Services;

public record TrendPrediction
{
    public string       Signal      { get; init; } = "Hold";
    public string       Risk        { get; init; } = "Medium";
    public int          Score       { get; init; }
    public List<string> Reasons     { get; init; } = [];
    public double       Rsi         { get; init; }
    public double       MacdDiff    { get; init; }
    public double       BbPosition  { get; init; }
    // Long / Short recommendation
    public string       LsDirection  { get; init; } = "Neutral";
    public string       LsConfidence { get; init; } = "Low";
    // Price forecasts
    public decimal      CurrentPrice { get; init; }
    public decimal      Forecast1d   { get; init; }
    public decimal      Forecast3d   { get; init; }
    public decimal      Forecast1w   { get; init; }
    public decimal      Forecast1m   { get; init; }
    public decimal      Forecast1y   { get; init; }
}

public static class IndicatorService
{
    // ── Simple Moving Average ────────────────────────────────────────────
    public static double[] Sma(double[] prices, int period)
    {
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            if (i < period - 1) { result[i] = double.NaN; continue; }
            double sum = 0;
            for (var j = i - period + 1; j <= i; j++) sum += prices[j];
            result[i] = sum / period;
        }
        return result;
    }

    // ── Exponential Moving Average ───────────────────────────────────────
    public static double[] Ema(double[] prices, int period)
    {
        var result = new double[prices.Length];
        for (var i = 0; i < period - 1; i++) result[i] = double.NaN;
        double sum = 0;
        for (var i = 0; i < period; i++) sum += prices[i];
        result[period - 1] = sum / period;
        var k = 2.0 / (period + 1);
        for (var i = period; i < prices.Length; i++)
            result[i] = prices[i] * k + result[i - 1] * (1 - k);
        return result;
    }

    // ── RSI (Wilder smoothing) ───────────────────────────────────────────
    public static double[] Rsi(double[] prices, int period = 14)
    {
        var result = new double[prices.Length];
        for (var i = 0; i < period; i++) result[i] = double.NaN;

        double avgGain = 0, avgLoss = 0;
        for (var i = 1; i <= period; i++)
        {
            var d = prices[i] - prices[i - 1];
            if (d > 0) avgGain += d; else avgLoss -= d;
        }
        avgGain /= period; avgLoss /= period;
        result[period] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);

        for (var i = period + 1; i < prices.Length; i++)
        {
            var d    = prices[i] - prices[i - 1];
            var gain = d > 0 ? d : 0;
            var loss = d < 0 ? -d : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
        }
        return result;
    }

    // ── MACD ─────────────────────────────────────────────────────────────
    public static (double[] macd, double[] signal, double[] histogram) Macd(
        double[] prices, int fast = 12, int slow = 26, int sig = 9)
    {
        var emaFast  = Ema(prices, fast);
        var emaSlow  = Ema(prices, slow);
        var macd     = emaFast.Zip(emaSlow, (f, s) =>
            double.IsNaN(f) || double.IsNaN(s) ? double.NaN : f - s).ToArray();

        // Feed only valid macd values into the signal EMA
        var filled   = macd.Select(x => double.IsNaN(x) ? 0.0 : x).ToArray();
        var signal   = Ema(filled, sig);
        var histo    = macd.Zip(signal, (m, s) => double.IsNaN(m) ? double.NaN : m - s).ToArray();
        return (macd, signal, histo);
    }

    // ── Bollinger Bands ──────────────────────────────────────────────────
    public static (double[] upper, double[] middle, double[] lower) BollingerBands(
        double[] prices, int period = 20, double mult = 2.0)
    {
        var upper  = new double[prices.Length];
        var middle = new double[prices.Length];
        var lower  = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            if (i < period - 1) { upper[i] = middle[i] = lower[i] = double.NaN; continue; }
            var slice = prices[(i - period + 1)..(i + 1)];
            var mean  = slice.Average();
            var std   = Math.Sqrt(slice.Sum(x => (x - mean) * (x - mean)) / period);
            upper[i]  = mean + mult * std;
            middle[i] = mean;
            lower[i]  = mean - mult * std;
        }
        return (upper, middle, lower);
    }

    // ── Trend Prediction ─────────────────────────────────────────────────
    public static TrendPrediction Predict(
        double[] closes,
        double[] rsi, double[] macdLine, double[] signalLine,
        double[] bbUpper, double[] bbLower, double[] bbMiddle,
        double[] sma20, double[] sma50,
        string interval = "1h")
    {
        var n       = closes.Length - 1;
        var score   = 0;
        var reasons = new List<string>();

        // RSI
        var curRsi = rsi[n];
        if      (curRsi < 30) { score += 35; reasons.Add($"RSI {curRsi:F1} — oversold, historically a strong buy zone."); }
        else if (curRsi < 40) { score += 18; reasons.Add($"RSI {curRsi:F1} — recovering from oversold territory."); }
        else if (curRsi < 50) { score +=  8; }
        else if (curRsi < 60) { score -=  8; }
        else if (curRsi < 70) { score -= 18; reasons.Add($"RSI {curRsi:F1} — approaching overbought, watch for reversal."); }
        else                  { score -= 35; reasons.Add($"RSI {curRsi:F1} — overbought, elevated reversal risk."); }

        // MACD crossover
        var macdDiff     = macdLine[n] - signalLine[n];
        var prevMacdDiff = macdLine[n - 1] - signalLine[n - 1];
        if      (macdDiff > 0 && prevMacdDiff <= 0) { score += 30; reasons.Add("MACD bullish crossover — momentum turning positive."); }
        else if (macdDiff < 0 && prevMacdDiff >= 0) { score -= 30; reasons.Add("MACD bearish crossover — momentum turning negative."); }
        else if (macdDiff > 0)                       { score += 15; reasons.Add("MACD above signal line — positive momentum sustained."); }
        else                                         { score -= 15; reasons.Add("MACD below signal line — negative momentum sustained."); }

        // Bollinger Band position
        var bbWidth = bbUpper[n] - bbLower[n];
        var bbPos   = bbWidth > 0 ? (closes[n] - bbLower[n]) / bbWidth * 100 : 50;
        if      (bbPos < 15) { score += 25; reasons.Add("Price near lower Bollinger Band — potential mean-reversion bounce."); }
        else if (bbPos < 35) { score += 10; }
        else if (bbPos > 85) { score -= 25; reasons.Add("Price near upper Bollinger Band — potential mean-reversion pullback."); }
        else if (bbPos > 65) { score -= 10; }

        // SMA trend
        if (!double.IsNaN(sma20[n]) && !double.IsNaN(sma50[n]))
        {
            if      (sma20[n] > sma50[n] && sma20[n - 1] <= sma50[n - 1]) { score += 20; reasons.Add("Golden Cross — SMA20 crossed above SMA50, bullish structural shift."); }
            else if (sma20[n] < sma50[n] && sma20[n - 1] >= sma50[n - 1]) { score -= 20; reasons.Add("Death Cross — SMA20 crossed below SMA50, bearish structural shift."); }
            else if (sma20[n] > sma50[n]) { score += 10; reasons.Add("SMA20 above SMA50 — uptrend structure intact."); }
            else                          { score -= 10; reasons.Add("SMA20 below SMA50 — downtrend structure intact."); }
        }

        var signal = score switch
        {
            >= 55  => "Strong Buy",
            >= 20  => "Buy",
            >= -20 => "Hold",
            >= -55 => "Sell",
            _      => "Strong Sell"
        };

        var volatility  = bbMiddle[n] > 0 ? bbWidth / bbMiddle[n] * 100 : 0;
        var conflicting = Math.Abs(score) < 25;
        var risk = (volatility > 6 || conflicting) ? "High"
                 : (volatility > 3 || Math.Abs(score) < 45) ? "Medium"
                 : "Low";

        // ── Long / Short recommendation ───────────────────────────────────
        var absScore     = Math.Abs(score);
        var lsDirection  = score >= 30 ? "Long" : score <= -30 ? "Short" : "Neutral";
        var lsConfidence = absScore >= 65 ? "High" : absScore >= 40 ? "Medium" : "Low";

        // ── Price forecast ────────────────────────────────────────────────
        // Candles per day for the chosen interval
        double cpd = interval switch
        {
            "15m" => 96.0,
            "4h"  => 6.0,
            "1d"  => 1.0,
            "1w"  => 1.0 / 7.0,
            _     => 24.0  // 1h default
        };

        // Per-candle momentum: average of last 20 candles % change
        var window = Math.Min(20, n);
        double sumPct = 0;
        for (var i = n - window + 1; i <= n; i++)
            sumPct += (closes[i] - closes[i - 1]) / closes[i - 1];
        var perCandleMom = sumPct / window;

        // Convert to per-day and add score-derived directional bias (max ±0.3%/day)
        var perDayMom      = perCandleMom * cpd;
        var scoreBiasDay   = (score / 100.0) * 0.003;
        var dailyRate      = perDayMom + scoreBiasDay;

        // Project forward with mean-reversion decay: shorter = more momentum-driven,
        // longer = heavily dampened toward the signal bias alone
        static decimal Project(double cur, double rate, double decay, double days) =>
            (decimal)(cur * Math.Pow(1.0 + rate * decay, days));

        var cur = closes[n];

        return new TrendPrediction
        {
            Signal       = signal,
            Risk         = risk,
            Score        = Math.Clamp(score, -100, 100),
            Reasons      = reasons,
            Rsi          = curRsi,
            MacdDiff     = macdDiff,
            BbPosition   = bbPos,
            LsDirection  = lsDirection,
            LsConfidence = lsConfidence,
            CurrentPrice = (decimal)cur,
            Forecast1d   = Project(cur, dailyRate, 1.00, 1),
            Forecast3d   = Project(cur, dailyRate, 0.85, 3),
            Forecast1w   = Project(cur, dailyRate, 0.65, 7),
            Forecast1m   = Project(cur, dailyRate, 0.25, 30),
            Forecast1y   = Project(cur, dailyRate, 0.05, 365),
        };
    }
}
