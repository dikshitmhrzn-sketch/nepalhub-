using System.ComponentModel.DataAnnotations;

namespace NepalHub.Api.Features.Account;

public sealed class ChangePasswordRequest
{
    // The current password is bounded like login's: this endpoint is one of the few
    // places it is ever sent, and the bound keeps an oversized body away from the hasher.
    [Required(ErrorMessage = "Current password is required.")]
    [StringLength(200)]
    public string CurrentPassword { get; init; } = string.Empty;

    // The length cap mirrors registration; Identity's own policy answers for the rest
    // with the same password_* codes either endpoint would give.
    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, ErrorMessage = "Password must be at most 100 characters.")]
    public string NewPassword { get; init; } = string.Empty;
}
