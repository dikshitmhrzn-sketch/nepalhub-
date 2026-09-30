namespace NepalHub.Api.Common;

public sealed record ApiResponse<T>(bool Success, T? Data, ApiError? Error)
{
    public static ApiResponse<T> Ok(T data) => new(true, data, null);

    public static ApiResponse<T> Fail(ApiError error) => new(false, default, error);
}

public sealed record ApiError(string Code, string Message, object? Details = null);
