using Microsoft.AspNetCore.SignalR;
using SentinelCrypto.Server.Models;
using SentinelCrypto.Server.Services;

namespace SentinelCrypto.Server.Hubs;

public sealed class CryptoHub : Hub<ICryptoClient>
{
    private readonly PriceAggregatorService _aggregator;
    private readonly ILogger<CryptoHub> _logger;

    public CryptoHub(PriceAggregatorService aggregator, ILogger<CryptoHub> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        // Send the current snapshot so the client doesn't show stale skeletons
        var snapshot = _aggregator.GetCurrentSnapshot();
        if (snapshot.Count > 0)
        {
            await Clients.Caller.ReceiveInitialState(snapshot.Values);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
