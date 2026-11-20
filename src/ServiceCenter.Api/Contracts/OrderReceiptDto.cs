namespace ServiceCenter.Api.Contracts;

public sealed record OrderReceiptDto(
    int Id,
    string OrderNumber,
    string Status,
    DateTimeOffset CreatedAt,
    string ClientFullName,
    string ClientPhone,
    string DeviceBrand,
    string DeviceModel,
    string SerialNumber,
    string IssueDescription,
    string DiagnosticNotes,
    string EngineerName,
    decimal TotalCost,
    IReadOnlyList<RepairItemDto> Items,
    IReadOnlyList<StatusHistoryDto> History);