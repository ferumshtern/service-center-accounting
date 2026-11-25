namespace ServiceCenter.Api.Data;

public static class DatabaseConnection
{
    public const string Name = "ServiceCenter";

    public static string Resolve(IConfiguration? configuration = null)
    {
        var configured = configuration?.GetConnectionString(Name)
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{Name}");

        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ServiceCenter");
        Directory.CreateDirectory(directory);

        return $"Data Source={Path.Combine(directory, "servicecenter.db")}";
    }
}
