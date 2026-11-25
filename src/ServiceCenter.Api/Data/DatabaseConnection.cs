namespace ServiceCenter.Api.Data;

// Один спосіб визначити рядок підключення для сервера і для засобу міграцій dotnet-ef,
// щоб обидва працювали з тим самим файлом бази.
public static class DatabaseConnection
{
    public const string Name = "ServiceCenter";

    // 1. ConnectionStrings:ServiceCenter з конфігурації або змінна середовища ConnectionStrings__ServiceCenter.
    // 2. Інакше — абсолютний шлях поза каталогами коду:
    //    Windows: %LOCALAPPDATA%\ServiceCenter\servicecenter.db, Linux/macOS: ~/.local/share/ServiceCenter/servicecenter.db.
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
