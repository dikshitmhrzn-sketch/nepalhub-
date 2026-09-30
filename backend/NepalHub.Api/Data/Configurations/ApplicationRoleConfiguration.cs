using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles");

        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(256);
        builder.Property(role => role.NormalizedName).HasMaxLength(256);
        builder.Property(role => role.ConcurrencyStamp).HasMaxLength(128);
        builder.Property(role => role.Description).HasMaxLength(200);
    }
}
