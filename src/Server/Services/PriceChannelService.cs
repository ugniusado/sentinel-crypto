using System.Threading.Channels;
using SentinelCrypto.Server.Models;

namespace SentinelCrypto.Server.Services;

/// <summary>
/// Singleton channel that decouples the WebSocket reader from the aggregator.
/// Using an unbounded channel means the socket can spike without dropping messages.
/// The aggregator drains it on a 100ms timer.
/// </summary>
public sealed class PriceChannelService
{
    private readonly Channel<PriceUpdate> _channel =
        Channel.CreateUnbounded<PriceUpdate>(new UnboundedChannelOptions
        {
            // Single writer (WebSocket service), multiple readers allowed
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });

    public ChannelWriter<PriceUpdate> Writer => _channel.Writer;
    public ChannelReader<PriceUpdate> Reader => _channel.Reader;
}
