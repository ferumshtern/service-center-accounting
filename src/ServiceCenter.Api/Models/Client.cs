namespace ServiceCenter.Api.Models;

public sealed class Client
{
    public int Id { get; init; }
    public string FullName { get; init; } = "";
    public string Phone { get; init; } = "";
}