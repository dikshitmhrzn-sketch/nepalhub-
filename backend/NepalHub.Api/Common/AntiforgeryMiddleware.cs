using Microsoft.AspNetCore.Antiforgery;

namespace NepalHub.Api.Common;

public sealed class AntiforgeryMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    ILogger<AntiforgeryMiddleware> logger) : IDisposable
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rejected {Method} {Path}: missing or invalid antiforgery token.",
                    context.Request.Method,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail(new ApiError(
                        "csrf_failed",
                        "The security token is missing or expired. Reload the page and try again.")));

                return;
            }
        }

        await next(context);
    }

    public void Dispose()
    {
    }

    private static bool RequiresValidation(HttpRequest request) =>
        !SafeMethods.Contains(request.Method)
        && request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
