using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Auth;

internal static class SessionUserMapper
{
    internal static async Task<SessionUserResponse> MapAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        cancellationToken.ThrowIfCancellationRequested();

        return new SessionUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.AvatarUrl,
            user.EmailConfirmed,
            user.Level,
            user.Xp,
            [.. roles.Order(StringComparer.Ordinal)]);
    }
}
