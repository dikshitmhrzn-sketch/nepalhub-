namespace NepalHub.Api.Features.Auth;

public sealed record RoleDefinition(string Name, string Description);

public static class Roles
{
    public const string User = "user";
    public const string Business = "business";
    public const string Moderator = "moderator";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<RoleDefinition> All =
    [
        new(User, "Standard registered member."),
        new(Business, "Owner or manager of a listed business."),
        new(Moderator, "Reviews reported content and abuse reports."),
        new(Admin, "Full access to platform administration."),
    ];
}

public static class AuthPolicies
{
    public const string AdminOnly = "admin-only";
    public const string StaffOnly = "staff-only";
}
