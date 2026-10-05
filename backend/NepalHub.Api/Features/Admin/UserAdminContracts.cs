using System.ComponentModel.DataAnnotations;

namespace NepalHub.Api.Features.Admin;

/// <summary>
/// One account on the admin screen: what an administrator needs to recognise it — the
/// address, the name, where the account stands on XP and activity — plus the roles that
/// decide what it may do. Nothing about the account that only the account itself should
/// see is here: no password material, no logins, no tokens.
/// </summary>
public sealed record UserAdminSummaryResponse(
    Guid Id,
    string Email,
    string FullName,
    int Level,
    int Xp,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);

/// <summary>Granting or revoking one role. The name is checked against the known set.</summary>
public sealed class ChangeRoleRequest
{
    [Required(ErrorMessage = "A role is required.")]
    public string? Role { get; set; }
}

/// <summary>
/// Parses the account list's status filter, following the other filters' rule: blank or
/// "all" means no filter, "active" and "inactive" are what they say, and anything else is
/// refused rather than defaulted — a typo that answers with every account would make the
/// screen look like the filter did nothing.
/// </summary>
internal static class UserFilters
{
    internal static bool TryParseStatus(string? raw, out bool? active, out string field)
    {
        field = "status";
        active = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            active = true;

            return true;
        }

        if (trimmed.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            active = false;

            return true;
        }

        return false;
    }
}
