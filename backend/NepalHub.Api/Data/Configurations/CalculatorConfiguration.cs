using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class CalculatorConfiguration : IEntityTypeConfiguration<Calculator>
{
    public void Configure(EntityTypeBuilder<Calculator> builder)
    {
        builder.ToTable("calculators");

        builder.HasKey(calculator => calculator.Id);
        builder.Property(calculator => calculator.Id).ValueGeneratedNever();
        builder.Property(calculator => calculator.Slug).HasMaxLength(80).IsRequired();
        builder.Property(calculator => calculator.Name).HasMaxLength(120).IsRequired();
        builder.Property(calculator => calculator.Summary).HasMaxLength(280).IsRequired();
        builder.Property(calculator => calculator.Description).IsRequired();
        builder.Property(calculator => calculator.Category)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(calculator => calculator.InputsJson).HasColumnType("json").IsRequired();
        builder.Property(calculator => calculator.SortOrder).HasDefaultValue(0).IsRequired();
        builder.Property(calculator => calculator.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(calculator => calculator.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(calculator => calculator.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne(calculator => calculator.CreatedByUser)
            .WithMany()
            .HasForeignKey(calculator => calculator.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("calculators_fk_created_by_user_id");

        builder.HasIndex(calculator => calculator.Slug)
            .IsUnique()
            .HasDatabaseName("ix_calculators_slug");

        // The catalogue is always read as "published rows in display order".
        builder.HasIndex(calculator => new { calculator.IsPublished, calculator.SortOrder })
            .HasDatabaseName("ix_calculators_is_published_sort_order");

        builder.HasIndex(calculator => calculator.Category)
            .HasDatabaseName("ix_calculators_category");

        builder.HasIndex(calculator => calculator.CreatedByUserId)
            .HasDatabaseName("ix_calculators_created_by_user_id");
    }
}
