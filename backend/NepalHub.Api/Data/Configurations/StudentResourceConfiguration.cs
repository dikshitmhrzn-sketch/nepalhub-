using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class StudentResourceConfiguration : IEntityTypeConfiguration<StudentResource>
{
    public void Configure(EntityTypeBuilder<StudentResource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(resource => resource.Id);
        builder.Property(resource => resource.Id).ValueGeneratedNever();
        builder.Property(resource => resource.Slug).HasMaxLength(160).IsRequired();
        builder.Property(resource => resource.Title).HasMaxLength(200).IsRequired();
        builder.Property(resource => resource.Summary).HasMaxLength(300).IsRequired();

        // The body is prose, not markup, so it is long text. The character ceiling is
        // enforced by the service rather than the column: a database limit surfaces as a
        // driver error with no field name, and staff need to be told which input was too long.
        builder.Property(resource => resource.Body)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(resource => resource.Type)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(resource => resource.Category)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(resource => resource.Level)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(resource => resource.ExternalUrl).HasMaxLength(500);
        builder.Property(resource => resource.AwardAmountNpr).HasColumnType("decimal(12,2)");
        builder.Property(resource => resource.Provider).HasMaxLength(160);
        builder.Property(resource => resource.Eligibility).HasMaxLength(500);
        builder.Property(resource => resource.ExpiresOn).HasColumnType("date");
        builder.Property(resource => resource.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(resource => resource.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(resource => resource.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne(resource => resource.CreatedByUser)
            .WithMany()
            .HasForeignKey(resource => resource.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("resources_fk_created_by_user_id");

        builder.HasIndex(resource => resource.Slug)
            .IsUnique()
            .HasDatabaseName("ix_resources_slug");

        // Every public read filters on is_published first, so each composite index leads with
        // it. A plain single-column index on the second column would be useless here, because
        // the published predicate would still have to be checked row by row.
        builder.HasIndex(resource => new { resource.IsPublished, resource.Type })
            .HasDatabaseName("ix_resources_is_published_resource_type");

        // Serves the default catalogue order, which is newest first.
        builder.HasIndex(resource => new { resource.IsPublished, resource.CreatedAt })
            .HasDatabaseName("ix_resources_is_published_created_at");

        builder.HasIndex(resource => new { resource.IsPublished, resource.Level })
            .HasDatabaseName("ix_resources_is_published_education_level");

        builder.HasIndex(resource => new { resource.IsPublished, resource.Category })
            .HasDatabaseName("ix_resources_is_published_category");

        // The catalogue always excludes expired rows, and "closing soon" is a sort, so the
        // deadline is filtered and ordered on together.
        builder.HasIndex(resource => new { resource.IsPublished, resource.ExpiresOn })
            .HasDatabaseName("ix_resources_is_published_expires_on");

        builder.HasIndex(resource => resource.CreatedByUserId)
            .HasDatabaseName("ix_resources_created_by_user_id");
    }
}
