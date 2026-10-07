using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NepalHub.Api.Data;

/// <summary>
/// What "up" means for this site: a database that answers. The probe at <c>/health</c> runs this
/// on every call, so an instance whose connection string has gone missing or whose server has gone
/// away reports unhealthy and can be pulled from rotation instead of serving errors to real people.
/// </summary>
public sealed class DatabaseHealthCheck(
    IServiceProvider services,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetService<AppDbContext>();
        if (dbContext is null)
        {
            return HealthCheckResult.Unhealthy("not configured");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            return await dbContext.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy("connected")
                : HealthCheckResult.Unhealthy("unreachable");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("timeout");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database health check failed.");
            return HealthCheckResult.Unhealthy("unreachable", exception);
        }
    }
}
