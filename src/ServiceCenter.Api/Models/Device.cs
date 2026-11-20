namespace ServiceCenter.Api.Models;

public sealed class Device
{
    public int Id { get; init; }
    public int ClientId { get; init; }
    public string Brand { get; init; } = "";
    public string Model { get; init; } = "";
    public string SerialNumber { get; init; } = "";
}