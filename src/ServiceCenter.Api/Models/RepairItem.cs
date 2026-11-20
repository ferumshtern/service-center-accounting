namespace ServiceCenter.Api.Models;

public sealed class RepairItem
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal => Quantity * UnitPrice;
}