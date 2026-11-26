namespace ServiceCenter.Api.Models;

// Спринт S2: гарантійне звернення пов'язує видане замовлення з новим гарантійним замовленням
public sealed class WarrantyClaim
{
    public int Id { get; init; }
    public int OriginalOrderId { get; init; }
    public int WarrantyOrderId { get; init; }
    public DateTimeOffset ClaimedAt { get; init; }
}
