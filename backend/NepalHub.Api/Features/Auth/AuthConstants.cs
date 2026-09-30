using Microsoft.AspNetCore.Identity;

namespace NepalHub.Api.Features.Auth;

public static class AuthConstants
{
    public static readonly string Scheme = IdentityConstants.ApplicationScheme;

    public const string CookieName = ".NepalHub.Auth";

    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    public const string CsrfCookieName = ".NepalHub.Csrf";
}
