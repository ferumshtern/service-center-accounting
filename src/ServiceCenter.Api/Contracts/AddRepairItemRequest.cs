namespace ServiceCenter.Api.Contracts;

public sealed record AddRepairItemRequest(
    string? DiagnosticNotes,
    string Name,
    int Quantity,
    decimal UnitPrice);