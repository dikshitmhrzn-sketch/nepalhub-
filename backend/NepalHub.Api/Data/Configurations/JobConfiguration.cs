using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.Slug).HasMaxLength(160).IsRequired();
        builder.Property(job => job.Title).HasMaxLength(200).IsRequired();
        builder.Property(job => job.Organization).HasMaxLength(160).IsRequired();

        // The posting is prose, not markup, so it is long text. The character ceiling is
        // enforced by the service rather than the column: a database limit surfaces as a
        // driver error with no field name, and staff need to be told which input was too long.
        builder.Property(job => job.Description)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(job => job.Category)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(job => job.EmploymentType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(job => job.WorkMode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(job => job.ApplicationUrl).HasMaxLength(500).IsRequired();
        builder.Property(job => job.ApplicationDeadline).HasColumnType("date").IsRequired();
        builder.Property(job => job.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(job => job.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(job => job.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        // Restrict rather than cascade: districts are reference data every other module reads,
        // so a posting must never be able to take one out of the database.
        builder.HasOne(job => job.District)
            .WithMany()
            .HasForeignKey(job => job.DistrictId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("jobs_fk_district_id");

        builder.HasOne(job => job.CreatedByUser)
            .WithMany()
            .HasForeignKey(job => job.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("jobs_fk_created_by_user_id");

        builder.HasIndex(job => job.Slug)
            .IsUnique()
            .HasDatabaseName("ix_jobs_slug");

        // Every public read filters on is_published first, so each composite index leads with
        // it. A plain single-column index on the second column would be useless here, because
        // the published predicate would still have to be checked row by row.
        builder.HasIndex(job => new { job.IsPublished, job.CreatedAt })
            .HasDatabaseName("ix_jobs_is_published_created_at");

        // Serves the deadline sort, and the public list's exclusion of closed postings.
        builder.HasIndex(job => new { job.IsPublished, job.ApplicationDeadline })
            .HasDatabaseName("ix_jobs_is_published_application_deadline");

        builder.HasIndex(job => new { job.IsPublished, job.Category })
            .HasDatabaseName("ix_jobs_is_published_category");

        builder.HasIndex(job => new { job.IsPublished, job.EmploymentType })
            .HasDatabaseName("ix_jobs_is_published_employment_type");

        builder.HasIndex(job => new { job.IsPublished, job.WorkMode })
            .HasDatabaseName("ix_jobs_is_published_work_mode");

        // Location filters resolve through the district, so the district leads here rather than
        // is_published: a query filters by district and published-ness together, and MySQL can
        // use whichever prefix it needs.
        builder.HasIndex(job => new { job.DistrictId, job.IsPublished })
            .HasDatabaseName("ix_jobs_district_id_is_published");

        builder.HasIndex(job => job.CreatedByUserId)
            .HasDatabaseName("ix_jobs_created_by_user_id");
    }
}
