using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class ContentSubmissionConfiguration
    : IEntityTypeConfiguration<ContentSubmission>
{
    public void Configure(EntityTypeBuilder<ContentSubmission> builder)
    {
        builder.ToTable("content_submissions");

        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Id).ValueGeneratedNever();

        builder.Property(submission => submission.Kind)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(submission => submission.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(SubmissionStatus.Pending);

        builder.Property(submission => submission.Note).HasMaxLength(500);
        builder.Property(submission => submission.ReviewNote).HasMaxLength(500);

        builder.Property(submission => submission.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(submission => submission.ReviewedAt).HasColumnType("datetime(6)");

        // Approval publishes the draft, so the draft must outlive the submission: the record
        // of who asked and who checked stays even if the row itself is later retracted.
        builder.HasOne(submission => submission.Job)
            .WithMany()
            .HasForeignKey(submission => submission.JobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_submissions_fk_job_id");

        builder.HasOne(submission => submission.Event)
            .WithMany()
            .HasForeignKey(submission => submission.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_submissions_fk_event_id");

        builder.HasOne(submission => submission.Deal)
            .WithMany()
            .HasForeignKey(submission => submission.DealId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_submissions_fk_deal_id");

        builder.HasOne(submission => submission.SubmittedBy)
            .WithMany()
            .HasForeignKey(submission => submission.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_submissions_fk_submitted_by_user_id");

        // Deleting a staff account must not re-open the question of who checked a draft.
        builder.HasOne(submission => submission.ReviewedBy)
            .WithMany()
            .HasForeignKey(submission => submission.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("content_submissions_fk_reviewed_by_user_id");

        // The queue itself: pending first is what the screen asks for, then newest.
        builder.HasIndex(submission => new { submission.Status, submission.CreatedAt })
            .HasDatabaseName("ix_content_submissions_status_created_at");

        // "What has this person sent in" — the author's own list on the submit screen.
        builder.HasIndex(submission => new { submission.SubmittedByUserId, submission.CreatedAt })
            .HasDatabaseName("ix_content_submissions_submitted_by_created_at");
    }
}
