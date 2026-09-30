using Microsoft.AspNetCore.Identity;

namespace NepalHub.Api.Data.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int Xp { get; set; }

    public int Level { get; set; } = 1;

    public int StreakCount { get; set; }

    public DateOnly? LastActiveOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;
}
