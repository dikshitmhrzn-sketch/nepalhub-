using System.Diagnostics;

namespace NepalHub.Api.Common;

/// <summary>
/// One structured line per request: what was asked for, what the answer was, how long it took and
/// the request id that ties this line to every other line written while the request was in flight.
/// The id comes from the proxy when the proxy supplies one and from the platform when it does not,
/// is echoed back as a response header a person can quote in a bug report, and is only reflected
/// in a form that is safe to reflect: a forged header full of whitespace or control characters is
/// replaced rather than copied into the response.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context);
        context.Response.Headers["X-Request-Id"] = requestId;

        var started = Stopwatch.GetTimestamp();

        using (logger.BeginScope("{RequestId}", requestId))
        {
            int statusCode;
            try
            {
                await next(context);
                statusCode = context.Response.StatusCode;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "{RequestId} {Method} {Path} failed with an unhandled exception after {ElapsedMilliseconds}ms.",
                    requestId,
                    context.Request.Method,
                    context.Request.Path,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                throw;
            }

            var level = statusCode switch
            {
                >= 500 => LogLevel.Error,
                StatusCodes.Status429TooManyRequests => LogLevel.Warning,
                _ => LogLevel.Information,
            };

            logger.Log(
                level,
                "{RequestId} {Method} {Path} answered {StatusCode} in {ElapsedMilliseconds}ms.",
                requestId,
                context.Request.Method,
                context.Request.Path,
                statusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static string ResolveRequestId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Request-Id"].ToString();
        return IsSafeRequestId(supplied) ? supplied : context.TraceIdentifier;
    }

    internal static bool IsSafeRequestId(string value) =>
        value.Length is > 0 and <= 64
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');
}
