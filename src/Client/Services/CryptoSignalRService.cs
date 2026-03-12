using Microsoft.AspNetCore.SignalR.Client;

namespace SentinelCrypto.Client.Services;

public enum HubStatus { Disconnected, Connecting, Connected, Reconnecting }

/// <summary>
/// Manages the SignalR connection lifecycle and pipes incoming messages
/// into PriceStateService.
///
/// Auto-reconnects with exponential back-off. Exposes HubStatus
/// so the UI can show "Connecting…" / "Reconnecting…" toasts.
/// </summary>
public sealed class CryptoSignalRService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly PriceStateService _priceState;
    private readonly IConfiguration _config;
    private readonly ILogger<CryptoSignalRService> _logger;

    public HubStatus Status { get; private set; } = HubStatus.Disconnected;
    public event Action? OnStatusChanged;

    public CryptoSignalRService(
        PriceStateService priceState,
        IConfiguration config,
        ILogger<CryptoSignalRService> logger)
    {
        _priceState = priceState;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        var hubUrl = _config["SignalR:HubUrl"] ?? "http://localhost:5000/hubs/crypto";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // Wire up handlers BEFORE connecting
        _hub.On<AggregatedUpdateDto>("ReceivePriceUpdate", update =>
            _priceState.ApplyUpdate(update));

        _hub.On<IEnumerable<AggregatedUpdateDto>>("ReceiveInitialState", updates =>
            _priceState.ApplyInitialState(updates));

        _hub.Reconnecting += _ =>
        {
            SetStatus(HubStatus.Reconnecting);
            return Task.CompletedTask;
        };

        _hub.Reconnected += _ =>
        {
            SetStatus(HubStatus.Connected);
            return Task.CompletedTask;
        };

        _hub.Closed += _ =>
        {
            SetStatus(HubStatus.Disconnected);
            return Task.CompletedTask;
        };

        SetStatus(HubStatus.Connecting);

        try
        {
            await _hub.StartAsync();
            SetStatus(HubStatus.Connected);
            _logger.LogInformation("SignalR connected to {Url}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            SetStatus(HubStatus.Disconnected);
        }
    }

    private void SetStatus(HubStatus status)
    {
        Status = status;
        OnStatusChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
