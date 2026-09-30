using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NepalHub.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Port=3306;Database=nepalhub;User=root;Password=;";
        var serverVersionText =
            Environment.GetEnvironmentVariable("Database__ServerVersion")
            ?? "8.4.0-mysql";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, ServerVersion.Parse(serverVersionText))
            .Options;

        return new AppDbContext(options);
    }
}
