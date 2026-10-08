using System.ComponentModel.DataAnnotations;

namespace NepalHub.Api.Features.Auth;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    // The minimum length is deliberately not repeated here: Identity owns the
    // password policy and reports "password_too_short" and "password_too_weak"
    // with the same codes as the rest of the password errors. This is only an
    // upper bound, so an oversized body cannot reach the hasher.
    [StringLength(100, ErrorMessage = "Password must be at most 100 characters.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Full name must be at least 2 characters.")]
    public string FullName { get; init; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    public string? PhoneNumber { get; init; }
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(200)]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public sealed record CsrfTokenResponse(string HeaderName, string Token);

public sealed class VerifyEmailRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    // The token travels beside the address because it is not self-describing: it is an
    // opaque Identity payload, and the account it belongs to is looked up by email.
    // The bound is an upper limit for the body only — Identity's own token is a few
    // hundred characters of base64.
    [Required(ErrorMessage = "Token is required.")]
    [StringLength(2000)]
    public string Token { get; init; } = string.Empty;
}

public sealed class ResendVerificationRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;
}

public sealed record SessionUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool IsEmailConfirmed,
    int Level,
    int Xp,
    IReadOnlyList<string> Roles);

public sealed record SessionResponse(bool Authenticated, SessionUserResponse? User);
