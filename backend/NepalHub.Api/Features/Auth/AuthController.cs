using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Email;

namespace NepalHub.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    EmailSettings emailSettings,
    EmailDispatchService emailDispatch,
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
        var requiresVerification = emailSettings.EnforcesVerification;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // With no way to send the link, demanding one would lock every new account out
            // of the site it just joined — so verification only applies once a provider is
            // configured, and the default install keeps starting accounts confirmed.
            EmailConfirmed = !requiresVerification,
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

        if (requiresVerification)
        {
            // The account is created but not signed into: a session before confirmation would
            // make the confirmation optional, and the answer carries the same shape it always
            // did — isEmailConfirmed: false — so the client knows to show "check your email".
            await emailDispatch.SendVerificationAsync(user, cancellationToken);
            logger.LogInformation("Registered user {UserId} pending email confirmation.", user.Id);

            return Created(
                "/api/account/me",
                ApiResponse<SessionUserResponse>.Ok(await SessionUserMapper.MapAsync(user, userManager, cancellationToken)));
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
        var user = await userManager.FindByNameAsync(email);
        if (user is null)
        {
            // The same answer a wrong password gets: whether an address holds an account
            // must not be readable from which failure message comes back.
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("invalid_credentials", "Email or password is incorrect.")));
        }

        // The password is checked before anything else is allowed to name a reason, and the
        // cookie is only minted once every check has passed — so an unconfirmed or disabled
        // account never carries a session, not even for the length of a response.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!user.IsActive)
            {
                await signInManager.SignOutAsync();
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(new ApiError("account_disabled", "This account has been disabled.")));
            }

            if (emailSettings.EnforcesVerification && !user.EmailConfirmed)
            {
                // Only now, with the password matched, does the answer say why: telling an
                // anonymous caller "confirm your email first" would confirm the account
                // exists, but a caller who already knows the password learns only the
                // next step they are owed.
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(new ApiError(
                        "email_not_confirmed",
                        "Confirm your email address before signing in.")));
            }

            await signInManager.SignInAsync(user, isPersistent: request.RememberMe, AuthConstants.Scheme);

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
    [EnableRateLimiting(RateLimitPolicies.Logout)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        await signInManager.SignOutAsync();

        return Ok(ApiResponse<object>.Ok(new { signedOut = true }));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> VerifyEmail(VerifyEmailRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            // Identity checks the token — purpose, expiry, security stamp — and confirming an
            // address that is already confirmed succeeds again, so the browser's back button
            // or a duplicated link lands on the same answer instead of an error page.
            var confirmed = await userManager.ConfirmEmailAsync(user, request.Token);
            if (confirmed.Succeeded)
            {
                logger.LogInformation("Email confirmed for user {UserId}.", user.Id);
                return Ok(ApiResponse<object>.Ok(new { verified = true }));
            }
        }

        // One answer for a bad token, an expired token, a token minted for another purpose
        // and an address nobody holds: a reply that distinguishes them would be a lookup
        // oracle for which addresses are registered.
        return BadRequest(ApiResponse<object>.Fail(
            new ApiError("token_invalid", "This link is invalid or has expired. Request a new one.")));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.ResendVerification)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is { EmailConfirmed: false })
        {
            await emailDispatch.SendVerificationAsync(user, cancellationToken);
        }

        // The same answer whether the address holds an unconfirmed account, a confirmed one
        // or nothing at all — send or no-send is exactly the signal this endpoint must not
        // give away. The send itself, if any, fails into a log line rather than the reply.
        return Ok(ApiResponse<object>.Ok(new { sent = true }));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Unlike resend, this looks for the account whether or not it has confirmed:
        // the address proves the same thing either way, and refusing the only recovery
        // route to someone who never opened their first link would trap them.
        if (user is not null)
        {
            await emailDispatch.SendPasswordResetAsync(user, cancellationToken);
        }

        // Identical for a known address, an unknown one and a sender that just failed —
        // this endpoint answers "check your email", never "there is no email to check".
        return Ok(ApiResponse<object>.Ok(new { sent = true }));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return BadRequestTokenInvalid();
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (result.Succeeded)
        {
            // Identity changed the security stamp on the way through, which is what makes
            // the link single-use and ends every other session this account holds: the
            // password changed, so nothing that was signed in before may stay signed in.
            logger.LogInformation("Password reset for user {UserId}.", user.Id);
            return Ok(ApiResponse<object>.Ok(new { reset = true }));
        }

        // Identity checks the token before it judges the new password, so an invalid or
        // expired link can only ever come back as one thing — and a link the password
        // policy rejected is still good, leaving the person free to try a better one.
        if (result.Errors.Any(error => error.Code == "InvalidToken"))
        {
            return BadRequestTokenInvalid();
        }

        var errors = IdentityErrors.Translate(result.Errors);
        return BadRequest(ApiResponse<object>.Fail(new ApiError("validation_failed", "Choose a new password.", errors)));
    }

    private BadRequestObjectResult BadRequestTokenInvalid() =>
        BadRequest(ApiResponse<object>.Fail(
            new ApiError("token_invalid", "This link is invalid or has expired. Request a new one.")));

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
