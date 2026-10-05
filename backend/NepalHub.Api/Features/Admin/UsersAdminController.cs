using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Admin;

/// <summary>
/// Administering the accounts themselves: who is on the platform, whether they are still
/// able to sign in, and what they are allowed to do.
///
/// Admin only, not staff. Disabling an account and handing out roles change what a person
/// can reach, which belongs to the people who oversee the moderators rather than to the
/// moderators themselves — the same line the audit log reader already draws.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class UsersAdminController(
    UserAdminService userAdminService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The account list. The status filter parses as a string so a typo produces a field
    /// error the screen can explain, and a role filter that names no real role is
    /// refused rather than ignored — a filter that silently does nothing would read as
    /// "nobody has this role".
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<UserAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? status,
        string? role,
        CancellationToken cancellationToken)
    {
        if (!UserFilters.TryParseStatus(status, out var active, out var field))
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                $"{field}_invalid",
                $"'{field}' is not a status this list understands.",
                FieldErrorsFor(field))));
        }

        (string Name, Guid Id)? roleFilter = null;

        if (!string.IsNullOrWhiteSpace(role))
        {
            roleFilter = await userAdminService.FindRoleAsync(role, cancellationToken);

            if (roleFilter is null)
            {
                return BadRequest(ApiResponse<object>.Fail(new ApiError(
                    "role_invalid",
                    "That role does not exist.",
                    FieldErrorsFor("role"))));
            }
        }

        var result = await userAdminService.ListAsync(
            PageRequest.Normalise(page, pageSize),
            TrimOrNull(search),
            active,
            roleFilter,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<UserAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>Switches one account off: it can no longer sign in.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<ApiResponse<UserAdminSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> Deactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveAsync(id, false, cancellationToken);

    /// <summary>
    /// Switches one account back on. Unlike its opposite this may be pointed at one's own
    /// account: the account that most needs unlocking is one that locked itself out.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<ApiResponse<UserAdminSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> Activate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveAsync(id, true, cancellationToken);

    /// <summary>
    /// Hands one of the four roles to an account. The account signs in again before it
    /// means anything, because the roles travel inside the cookie.
    /// </summary>
    [HttpPost("{id:guid}/roles/grant")]
    [ProducesResponseType<ApiResponse<UserAdminSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> Grant(
        Guid id,
        ChangeRoleRequest request,
        CancellationToken cancellationToken) =>
        ChangeRoleAsync(id, request.Role, assign: true, cancellationToken);

    /// <summary>Takes one of the four roles back off an account.</summary>
    [HttpPost("{id:guid}/roles/revoke")]
    [ProducesResponseType<ApiResponse<UserAdminSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> Revoke(
        Guid id,
        ChangeRoleRequest request,
        CancellationToken cancellationToken) =>
        ChangeRoleAsync(id, request.Role, assign: false, cancellationToken);

    private async Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> SetActiveAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return SessionInvalid();
        }

        var outcome = await userAdminService.SetActiveAsync(
            id,
            active,
            admin.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            UserAdminStatus.Updated => Ok(
                ApiResponse<UserAdminSummaryResponse>.Ok(outcome.User!)),

            UserAdminStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("user_not_found", "No account with that id."))),

            UserAdminStatus.CannotModifySelf => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "cannot_modify_self",
                    "Administrators cannot change their own account this way."))),

            UserAdminStatus.AlreadyActive => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_active", "That account is already active."))),

            UserAdminStatus.AlreadyInactive => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_inactive", "That account is already disabled."))),

            _ => ValidationFailed(outcome.Errors!),
        };
    }

    private async Task<ActionResult<ApiResponse<UserAdminSummaryResponse>>> ChangeRoleAsync(
        Guid id,
        string? role,
        bool assign,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return SessionInvalid();
        }

        var outcome = await userAdminService.ChangeRoleAsync(
            id,
            role ?? string.Empty,
            assign,
            admin.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            UserAdminStatus.Updated => Ok(
                ApiResponse<UserAdminSummaryResponse>.Ok(outcome.User!)),

            UserAdminStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("user_not_found", "No account with that id."))),

            UserAdminStatus.CannotModifySelf => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "cannot_modify_self",
                    "Administrators cannot change their own account this way."))),

            UserAdminStatus.UnknownRole => BadRequest(ApiResponse<object>.Fail(
                new ApiError("role_invalid", "That role does not exist.",
                    FieldErrorsFor("role")))),

            UserAdminStatus.RoleAlreadyAssigned => Conflict(ApiResponse<object>.Fail(
                new ApiError("role_already_assigned", "That account already has this role."))),

            UserAdminStatus.RoleNotAssigned => Conflict(ApiResponse<object>.Fail(
                new ApiError("role_not_assigned", "That account does not have this role."))),

            _ => ValidationFailed(outcome.Errors!),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));

    private BadRequestObjectResult ValidationFailed(IReadOnlyList<FieldError> errors) =>
        BadRequest(ApiResponse<object>.Fail(
            new ApiError("validation_failed", "The request could not be saved.", errors)));

    private static IReadOnlyList<FieldError> FieldErrorsFor(string field) =>
        [new FieldError(field, "invalid", "Choose one of the offered values.")];

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
