using SentinelCrypto.Server.Models;

namespace SentinelCrypto.Server.Hubs;

/// <summary>
/// Strongly-typed SignalR client contract.
/// Every method here maps to a JS/Blazor handler on the client.
/// </summary>
public interface ICryptoClient
{
    /// <summary>Pushed every ~100ms with the latest aggregated snapshot.</summary>
    Task ReceivePriceUpdate(AggregatedUpdate update);

    /// <summary>Sent once on connect with initial state for all tracked symbols.</summary>
    Task ReceiveInitialState(IEnumerable<AggregatedUpdate> updates);
}
