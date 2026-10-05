using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Admin;

public enum UserAdminStatus
{
    Updated,
    NotFound,
    CannotModifySelf,
    AlreadyActive,
    AlreadyInactive,
    UnknownRole,
    RoleAlreadyAssigned,
    RoleNotAssigned,
    Failed,
}

public sealed record UserAdminOutcome(
    UserAdminStatus Status,
    UserAdminSummaryResponse? User = null,
    IReadOnlyList<FieldError>? Errors = null);

/// <summary>
/// The administrator's view of the accounts: the list, and the three things an
/// administrator may do to one of them — switch it off, switch it on, change what it is
/// allowed to do.
///
/// <para>
/// Two rules hold everywhere. An administrator never acts on their own account, because
/// a self-inflicted lockout has nobody left to undo it. And each change is written to the
/// audit log in the same save as the change itself, so the record of what happened and
/// the fact that it happened cannot drift apart.
/// </para>
/// </summary>
public sealed class UserAdminService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAuditLog auditLog)
{
    /// <summary>
    /// One page of accounts, newest first. The filters narrow rather than re-order: search
    /// matches the address or the name, the status flag picks active against disabled, and
    /// a role filter answers the question administrators actually ask — who can moderate,
    /// who owns a business.
    /// </summary>
    public async Task<PagedResult<UserAdminSummaryResponse>> ListAsync(
        PageRequest paging,
        string? search,
        bool? active,
        (string Name, Guid Id)? role,
        CancellationToken cancellationToken)
    {
        var matching = dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            matching = matching.Where(user =>
                (user.Email != null && user.Email.Contains(needle))
                || user.FullName.Contains(needle));
        }

        if (active is not null)
        {
            matching = matching.Where(user => user.IsActive == active);
        }

        if (role is not null)
        {
            var roleId = role.Value.Id;
            matching = matching.Where(user =>
                dbContext.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id && userRole.RoleId == roleId));
        }

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var roles = await RolesForAsync(rows.Select(user => user.Id), cancellationToken);

        var items = rows
            .Select(user => ToSummary(
                user,
                roles.TryGetValue(user.Id, out var assigned) ? assigned : []))
            .ToList();

        return new PagedResult<UserAdminSummaryResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Resolves a role filter: one of the four known names, and a row in the roles table
    /// behind it. Anything else answers null, which the controller turns into a field
    /// error — a filter the screen cannot honour should say so, not quietly list everyone.
    /// </summary>
    public async Task<(string Name, Guid Id)?> FindRoleAsync(string role, CancellationToken cancellationToken)
    {
        var canonical = Canonical(role);

        if (canonical is null)
        {
            return null;
        }

        var entity = await roleManager.FindByNameAsync(canonical);

        return entity is null ? null : (canonical, entity.Id);
    }

    /// <summary>
    /// Switches one account on or off. A disabled account cannot sign in again — the
    /// login endpoint already answers 403 for it — which is the whole point of the
    /// switch: the fastest way to stop an abusive account without deleting the record
    /// of what it did.
    /// </summary>
    public async Task<UserAdminOutcome> SetActiveAsync(
        Guid targetId,
        bool active,
        Guid adminId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(targetId.ToString());

        if (user is null)
        {
            return new UserAdminOutcome(UserAdminStatus.NotFound);
        }

        if (user.Id == adminId)
        {
            return new UserAdminOutcome(UserAdminStatus.CannotModifySelf);
        }

        if (user.IsActive == active)
        {
            return new UserAdminOutcome(
                active ? UserAdminStatus.AlreadyActive : UserAdminStatus.AlreadyInactive);
        }

        var before = new { isActive = user.IsActive };

        user.IsActive = active;

        auditLog.Record(
            adminId,
            active ? AuditActions.UserActivated : AuditActions.UserDeactivated,
            "user",
            user.Id.ToString(),
            before,
            new { isActive = active },
            ip);

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return new UserAdminOutcome(
                UserAdminStatus.Failed, null, IdentityErrors.Translate(result.Errors));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserAdminOutcome(UserAdminStatus.Updated, await SummaryAsync(user));
    }

    /// <summary>
    /// Grants or revokes one role. The change lands in the audit log with the role list
    /// as it was and as it became, so the trail answers "what was this account" without
    /// anybody reconstructing it from successive entries.
    ///
    /// <para>
    /// The affected account keeps its current cookie until it signs in again — the roles
    /// are baked into the ticket — which is why granting a role is followed by a fresh
    /// sign-in wherever it matters.
    /// </para>
    /// </summary>
    public async Task<UserAdminOutcome> ChangeRoleAsync(
        Guid targetId,
        string role,
        bool assign,
        Guid adminId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(targetId.ToString());

        if (user is null)
        {
            return new UserAdminOutcome(UserAdminStatus.NotFound);
        }

        if (user.Id == adminId)
        {
            return new UserAdminOutcome(UserAdminStatus.CannotModifySelf);
        }

        var canonical = Canonical(role);

        if (canonical is null
            || await roleManager.FindByNameAsync(canonical) is null)
        {
            return new UserAdminOutcome(UserAdminStatus.UnknownRole);
        }

        var current = (await userManager.GetRolesAsync(user))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var assigned = current.Contains(canonical, StringComparer.Ordinal);

        if (assign == assigned)
        {
            return new UserAdminOutcome(
                assign ? UserAdminStatus.RoleAlreadyAssigned : UserAdminStatus.RoleNotAssigned);
        }

        var after = assign
            ? current.Append(canonical).Order(StringComparer.Ordinal).ToArray()
            : current
                .Where(name => !name.Equals(canonical, StringComparison.Ordinal))
                .ToArray();

        auditLog.Record(
            adminId,
            assign ? AuditActions.UserRoleGranted : AuditActions.UserRoleRevoked,
            "user",
            user.Id.ToString(),
            new { roles = current },
            new { roles = after },
            ip);

        var result = assign
            ? await userManager.AddToRoleAsync(user, canonical)
            : await userManager.RemoveFromRoleAsync(user, canonical);

        if (!result.Succeeded)
        {
            return new UserAdminOutcome(
                UserAdminStatus.Failed, null, IdentityErrors.Translate(result.Errors));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserAdminOutcome(UserAdminStatus.Updated, await SummaryAsync(user));
    }

    /// <summary>
    /// The four roles the platform knows, matched by name however the caller spelled the
    /// case. Anything outside the set — and any name with no row behind it — is not a
    /// role this system can hand out.
    /// </summary>
    private static string? Canonical(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? null
            : Roles.All.FirstOrDefault(definition =>
                definition.Name.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;

    private async Task<UserAdminSummaryResponse> SummaryAsync(ApplicationUser user)
    {
        var roles = (await userManager.GetRolesAsync(user))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return ToSummary(user, roles);
    }

    /// <summary>
    /// The roles on this page of accounts, in two queries rather than two per row. The
    /// list screen shows every account's roles at once, and one query per account would
    /// make a large page crawl.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> RolesForAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.ToList();

        var rows = await dbContext.UserRoles
            .Where(userRole => ids.Contains(userRole.UserId))
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Name ?? string.Empty)
                    .Order(StringComparer.Ordinal)
                    .ToList());
    }

    private static UserAdminSummaryResponse ToSummary(
        ApplicationUser user, IReadOnlyList<string> roles) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Level,
            user.Xp,
            user.IsActive,
            roles,
            user.CreatedAt);
}
