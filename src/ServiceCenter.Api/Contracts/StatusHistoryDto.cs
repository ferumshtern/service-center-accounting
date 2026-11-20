namespace ServiceCenter.Api.Contracts;

public sealed record StatusHistoryDto(
    string Status,
    DateTimeOffset ChangedAt);