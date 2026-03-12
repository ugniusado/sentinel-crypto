namespace SentinelCrypto.Client.Services;

public enum ViewMode { All = 0, Favorites = 1, TopGainers = 2, PanicAlerts = 3 }
public enum ColorMode { Default, ColorBlind }

public sealed class DashboardStateService
{
    public ViewMode   CurrentView    { get; private set; } = ViewMode.All;
    public ColorMode  ColorMode      { get; private set; } = ColorMode.Default;
    public bool       HeatmapEnabled { get; private set; }
    public bool       CommandPaletteOpen { get; private set; }
    public int        TotalUpdates   { get; private set; }

    public HashSet<string> Favorites { get; } = [];

    public event Action? OnStateChanged;

    public void SetView(ViewMode view)            { CurrentView = view; Notify(); }
    public void ToggleHeatmap()                   { HeatmapEnabled = !HeatmapEnabled; Notify(); }
    public void ToggleColorMode()                 { ColorMode = ColorMode == ColorMode.Default ? ColorMode.ColorBlind : ColorMode.Default; Notify(); }
    public void OpenCommandPalette()              { CommandPaletteOpen = true; Notify(); }
    public void CloseCommandPalette()             { CommandPaletteOpen = false; Notify(); }
    public void ToggleFavorite(string symbol)     { if (!Favorites.Add(symbol)) Favorites.Remove(symbol); Notify(); }
    public void IncrementUpdates()                { TotalUpdates++; } // hot path — no notify

    private void Notify() => OnStateChanged?.Invoke();
}
