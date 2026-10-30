namespace NepalHub.Api.Features.Health;

/// <summary>
/// What an anonymous probe is told. Deliberately only what a monitor needs: whether the
/// process is up and whether the database answers. The server version and the environment
/// name are operational details an unauthenticated caller has no business learning, so they
/// are not part of the contract - a probe that wants them must be able to authenticate for
/// them like any other privileged read.
/// </summary>
public sealed record HealthResponse(
    string Status,
    string Database,
    DateTimeOffset Timestamp,
    string? Detail = null);
