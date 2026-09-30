using Microsoft.AspNetCore.Identity;

namespace NepalHub.Api.Data.Entities;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
