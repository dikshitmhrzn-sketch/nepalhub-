using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();

        builder.Property(report => report.TargetType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(ReportStatus.Open);

        builder.Property(report => report.Note).HasMaxLength(500);
        builder.Property(report => report.ResolutionNote).HasMaxLength(500);

        builder.Property(report => report.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(report => report.ResolvedAt).HasColumnType("datetime(6)");

        builder.HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("reports_fk_reporter_user_id");

        // SetNull rather than cascade: closing a report must not delete the report, and the
        // moderator's part in it is a fact about the moderator, not about the reporter.
        builder.HasOne(report => report.ResolvedBy)
            .WithMany()
            .HasForeignKey(report => report.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reports_fk_resolved_by_user_id");

        // One report per person per target, enforced by the database rather than by a check
        // in the service, so a double-click or two tabs open at once cannot produce two
        // rows even if they race.
        builder.HasIndex(report => new { report.ReporterUserId, report.TargetType, report.TargetId })
            .IsUnique()
            .HasDatabaseName("ix_reports_reporter_target");

        // The queue itself: "the open reports, newest first", which is the only order the
        // moderation screen asks for.
        builder.HasIndex(report => new { report.Status, report.CreatedAt })
            .HasDatabaseName("ix_reports_status_created_at");
    }
}
