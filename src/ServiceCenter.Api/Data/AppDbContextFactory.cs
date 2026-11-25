using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceCenter.Api.Data;

// Фабрика часу проєктування: надає контекст засобу dotnet-ef для створення й застосування міграцій. 
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DatabaseConnection.Resolve())
            .Options;

        return new AppDbContext(options);
    }
}
