using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.Property(plan => plan.Tier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(plan => plan.Name).HasMaxLength(80).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(500);

        builder.Property(plan => plan.PriceNpr).HasDefaultValue(0).IsRequired();
        builder.Property(plan => plan.IsActive).HasDefaultValue(true).IsRequired();

        builder.Property(plan => plan.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(plan => plan.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        // One row per tier, so "the featured plan" is a lookup and not a query over
        // however many versions of it staff have edited. A superseded plan is edited
        // in place rather than replaced, because past subscriptions point at this row.
        builder.HasIndex(plan => plan.Tier)
            .IsUnique()
            .HasDatabaseName("ix_plans_tier");
    }
}
