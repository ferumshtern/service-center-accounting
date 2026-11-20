namespace ServiceCenter.Api.Models;

public sealed class ServiceEngineer
{
    public int Id { get; init; }
    public string FullName { get; init; } = "";
    public string Specialization { get; init; } = "";
}
