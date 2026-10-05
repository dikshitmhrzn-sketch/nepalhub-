using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;

namespace NepalHub.Api.Data.Configurations;

public sealed class BadgeDefinitionConfiguration : IEntityTypeConfiguration<BadgeDefinition>
{
    public void Configure(EntityTypeBuilder<BadgeDefinition> builder)
    {
        builder.ToTable("badges");

        builder.HasKey(badge => badge.Id);
        builder.Property(badge => badge.Id).ValueGeneratedNever();
        builder.Property(badge => badge.Code).HasMaxLength(50).IsRequired();
        builder.Property(badge => badge.Name).HasMaxLength(100).IsRequired();
        builder.Property(badge => badge.Description).HasMaxLength(250).IsRequired();
        builder.Property(badge => badge.Icon).HasMaxLength(40).IsRequired();
        builder.Property(badge => badge.Criteria)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(badge => badge.Threshold).IsRequired();
        builder.Property(badge => badge.SortOrder).IsRequired();

        // The code is what a client sends and a test asserts on, so it has to identify one
        // row and not two.
        builder.HasIndex(badge => badge.Code)
            .IsUnique()
            .HasDatabaseName("ix_badges_code");

        // Serving the account page means "every badge, in the order to show them".
        builder.HasIndex(badge => badge.SortOrder)
            .HasDatabaseName("ix_badges_sort_order");

        builder.HasData(NepalReferenceData.BadgeDefinitions.Select(badge => new BadgeDefinition
        {
            Id = badge.Id,
            Code = badge.Code,
            Name = badge.Name,
            Description = badge.Description,
            Icon = badge.Icon,
            Criteria = badge.Criteria,
            Threshold = badge.Threshold,
            SortOrder = badge.SortOrder,
        }));
    }
}
