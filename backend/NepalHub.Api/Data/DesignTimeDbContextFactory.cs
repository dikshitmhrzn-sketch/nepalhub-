using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NepalHub.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=127.0.0.1;Port=3307;Database=nepalhub;User=nepalhub_app;Password=;";
        var serverVersionText =
            Environment.GetEnvironmentVariable("Database__ServerVersion")
            ?? "8.4.0-mysql";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.Parse(serverVersionText),
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
