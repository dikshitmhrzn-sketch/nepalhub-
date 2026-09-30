using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<CsrfTokenResponse>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<CsrfTokenResponse>> GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(ApiResponse<CsrfTokenResponse>.Ok(
            new CsrfTokenResponse(AuthConstants.CsrfHeaderName, tokens.RequestToken ?? string.Empty)));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<SessionUserResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SessionUserResponse>>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // No mail provider exists yet; verification is introduced with the
            // notification module, so accounts start confirmed.
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            var errors = IdentityErrors.Translate(created.Errors);
            var status = errors.Any(error => error.Code == "email_taken")
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

            return StatusCode(status, ApiResponse<object>.Fail(new ApiError("validation_failed", "Registration failed.", errors)));
        }

        var roleAssigned = await userManager.AddToRoleAsync(user, Roles.User);
        if (!roleAssigned.Succeeded)
        {
            logger.LogError("Could not assign the '{Role}' role to user {UserId}.", Roles.User, user.Id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(new ApiError("role_assignment_failed", "Could not finish creating the account.")));
        }

        await signInManager.SignInAsync(user, isPersistent: true, AuthConstants.Scheme);
        logger.LogInformation("Registered user {UserId}.", user.Id);

        var sessionUser = await SessionUserMapper.MapAsync(user, userManager, cancellationToken);

        return Created($"/api/account/me", ApiResponse<SessionUserResponse>.Ok(sessionUser));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<SessionUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<ApiResponse<SessionUserResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var result = await signInManager.PasswordSignInAsync(
            email,
            request.Password,
            isPersistent: request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await userManager.FindByNameAsync(email);
            if (user is null)
            {
                logger.LogError("Signed in user {Email} could not be loaded.", email);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Fail(new ApiError("session_failed", "Could not start the session.")));
            }

            if (!user.IsActive)
            {
                await signInManager.SignOutAsync();
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(new ApiError("account_disabled", "This account has been disabled.")));
            }

            return Ok(ApiResponse<SessionUserResponse>.Ok(
                await SessionUserMapper.MapAsync(user, userManager, cancellationToken)));
        }

        if (result.IsLockedOut)
        {
            return StatusCode(
                StatusCodes.Status423Locked,
                ApiResponse<object>.Fail(new ApiError("account_locked", "Too many failed attempts. Try again later.")));
        }

        if (result.IsNotAllowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(new ApiError("not_allowed", "This account cannot sign in.")));
        }

        if (result.RequiresTwoFactor)
        {
            return StatusCode(
                StatusCodes.Status501NotImplemented,
                ApiResponse<object>.Fail(new ApiError("two_factor_required", "Two-factor sign-in is not available yet.")));
        }

        return Unauthorized(ApiResponse<object>.Fail(
            new ApiError("invalid_credentials", "Email or password is incorrect.")));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        await signInManager.SignOutAsync();

        return Ok(ApiResponse<object>.Ok(new { signedOut = true }));
    }

    [HttpGet("session")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<SessionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SessionResponse>>> GetSession(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(ApiResponse<SessionResponse>.Ok(new SessionResponse(false, null)));
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Ok(ApiResponse<SessionResponse>.Ok(new SessionResponse(false, null)));
        }

        return Ok(ApiResponse<SessionResponse>.Ok(
            new SessionResponse(true, await SessionUserMapper.MapAsync(user, userManager, cancellationToken))));
    }
}
