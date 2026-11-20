namespace ServiceCenter.Api.Models;

public sealed class StatusHistory
{
    public int Id { get; init; }
    public OrderStatus Status { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
}