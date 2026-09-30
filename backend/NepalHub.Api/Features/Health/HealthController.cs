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
public sealed class HealthController(IServiceProvider services, ILogger<HealthController> logger)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<HealthResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HealthResponse>>> Get(CancellationToken cancellationToken)
    {
        var response = new HealthResponse("healthy", await GetDatabaseStatusAsync(cancellationToken), DateTimeOffset.UtcNow);
        return Ok(ApiResponse<HealthResponse>.Ok(response));
    }

    private async Task<string> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        var dbContext = services.GetService<AppDbContext>();
        if (dbContext is null)
        {
            return "not-configured";
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            return canConnect ? "connected" : "unreachable";
        }
        catch (OperationCanceledException)
        {
            return "timeout";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database connectivity check failed.");
            return "unreachable";
        }
    }
}
