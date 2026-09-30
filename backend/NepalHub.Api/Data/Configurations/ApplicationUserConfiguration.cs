using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(user => user.FullName).HasMaxLength(120).IsRequired();
        builder.Property(user => user.AvatarUrl).HasMaxLength(500);
        builder.Property(user => user.PhoneNumber).HasMaxLength(20);
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254);
        builder.Property(user => user.PasswordHash).HasMaxLength(255);
        builder.Property(user => user.SecurityStamp).HasMaxLength(128);
        builder.Property(user => user.ConcurrencyStamp).HasMaxLength(128);
        builder.Property(user => user.Xp).HasDefaultValue(0);
        builder.Property(user => user.Level).HasDefaultValue(1);
        builder.Property(user => user.StreakCount).HasDefaultValue(0);
        builder.Property(user => user.IsActive).HasDefaultValue(true);

        builder.Property(user => user.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(user => user.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_users_normalized_email");

        builder.HasIndex(user => user.PhoneNumber)
            .IsUnique()
            .HasDatabaseName("ix_users_phone_number");

        builder.HasIndex(user => new { user.Level, user.Xp })
            .HasDatabaseName("ix_users_level_xp");

        builder.HasIndex(user => user.CreatedAt)
            .HasDatabaseName("ix_users_created_at");
    }
}
