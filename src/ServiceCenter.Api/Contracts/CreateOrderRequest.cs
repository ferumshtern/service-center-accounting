namespace ServiceCenter.Api.Contracts;

public sealed record CreateOrderRequest(
    string ClientFullName,
    string ClientPhone,
    string DeviceBrand,
    string DeviceModel,
    string SerialNumber,
    string IssueDescription,
    int EngineerId);