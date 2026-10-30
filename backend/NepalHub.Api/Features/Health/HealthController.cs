using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;

namespace NepalHub.Api.Features.Health;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class HealthController(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<HealthController> logger)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<HealthResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HealthResponse>>> Get(CancellationToken cancellationToken)
    {
        var (database, detail) = await GetDatabaseStatusAsync(cancellationToken);

        // Status, database and a clock only. This endpoint answers before any authentication,
        // so everything it says is said to the whole internet: the environment name and the
        // database version stay out of it, and the configuration that holds them is read here
        // only to describe - never to echo - a connection string that failed to parse.
        var response = new HealthResponse(
            "healthy",
            database,
            DateTimeOffset.UtcNow,
            detail);
        return Ok(ApiResponse<HealthResponse>.Ok(response));
    }

    private async Task<(string Status, string? Detail)> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        // Resolving the context is inside the try on purpose: building it evaluates the
        // configured server version, and a value this deployment cannot parse used to throw
        // out of here — past every handler — and turn the whole endpoint into a 500 instead
        // of a health answer saying what was wrong.
        try
        {
            var dbContext = services.GetService<AppDbContext>();
            if (dbContext is null)
            {
                return ("not-configured", null);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            if (canConnect)
            {
                return ("connected", null);
            }

            return ("unreachable", "The database did not answer a connection attempt within 2 seconds.");
        }
        catch (OperationCanceledException)
        {
            return ("timeout", "The database connectivity check timed out.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database connectivity check failed.");
            return ("error", $"{Truncate(exception.Message, 300)} {DescribeConnectionStringShape()}");
        }
    }

    // Structure only — never a character of the value itself — so an unauthenticated probe
    // can say what class of string was stored (a mysql:// URL, a bare host, a key=value
    // pair) without disclosing credentials.
    private string DescribeConnectionStringShape()
    {
        var value = configuration.GetConnectionString("Default")?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return "(ConnectionStrings:Default is empty.)";
        }

        var shape = value.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase)
            ? "mysql:// URL"
            : "not a mysql:// URL";
        return value.Contains('=')
            ? $"({shape}, has '=' but the key could not be parsed.)"
            : $"({shape}, no '=' — not a key=value connection string.)";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
