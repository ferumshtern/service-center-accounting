namespace ServiceCenter.Api.Contracts;

public sealed record RepairItemDto(
    int Id,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);