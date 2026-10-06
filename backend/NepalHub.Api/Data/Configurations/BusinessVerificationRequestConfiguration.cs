using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class BusinessVerificationRequestConfiguration
    : IEntityTypeConfiguration<BusinessVerificationRequest>
{
    public void Configure(EntityTypeBuilder<BusinessVerificationRequest> builder)
    {
        builder.ToTable("business_verification_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(BusinessVerificationStatus.Pending);

        builder.Property(request => request.Note).HasMaxLength(500);
        builder.Property(request => request.ReviewNote).HasMaxLength(500);

        builder.Property(request => request.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(request => request.ReviewedAt).HasColumnType("datetime(6)");

        // Approval publishes the listing, so the listing must outlive the request: the
        // record of who asked and who checked stays even if the row itself is retracted.
        builder.HasOne(request => request.Business)
            .WithMany()
            .HasForeignKey(request => request.BusinessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_verifications_fk_business_id");

        builder.HasOne(request => request.SubmittedBy)
            .WithMany()
            .HasForeignKey(request => request.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_verifications_fk_submitted_by_user_id");

        // Deleting a staff account must not re-open the question of who checked a listing.
        builder.HasOne(request => request.ReviewedBy)
            .WithMany()
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("business_verifications_fk_reviewed_by_user_id");

        // The queue itself: pending first is what the screen asks for, then newest.
        builder.HasIndex(request => new { request.Status, request.CreatedAt })
            .HasDatabaseName("ix_business_verifications_status_created_at");

        // "Is there already an open request for this listing" — the rule the service
        // refuses on, so the lookup needs an index rather than a scan.
        builder.HasIndex(request => new { request.BusinessId, request.Status })
            .HasDatabaseName("ix_business_verifications_business_status");
    }
}
