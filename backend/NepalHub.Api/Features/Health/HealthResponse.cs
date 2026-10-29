namespace NepalHub.Api.Features.Health;

public sealed record HealthResponse(
    string Status,
    string Database,
    DateTimeOffset Timestamp,
    string? ServerVersion = null,
    string? Detail = null);
