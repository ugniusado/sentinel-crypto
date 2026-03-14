namespace SentinelCrypto.Client.Models;

public record KlineData
{
    public DateTime OpenTime { get; init; }
    public decimal  Open     { get; init; }
    public decimal  High     { get; init; }
    public decimal  Low      { get; init; }
    public decimal  Close    { get; init; }
    public decimal  Volume   { get; init; }
}
