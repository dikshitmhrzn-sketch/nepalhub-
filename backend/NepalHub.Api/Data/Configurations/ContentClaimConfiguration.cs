using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class ContentClaimConfiguration : IEntityTypeConfiguration<ContentClaim>
{
    public void Configure(EntityTypeBuilder<ContentClaim> builder)
    {
        builder.ToTable("content_claims");

        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.Id).ValueGeneratedNever();

        builder.Property(claim => claim.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(claim => claim.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(ContentClaimStatus.Pending);

        builder.Property(claim => claim.Note).HasMaxLength(500);
        builder.Property(claim => claim.ReviewNote).HasMaxLength(500);

        builder.Property(claim => claim.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(claim => claim.ReviewedAt).HasColumnType("datetime(6)");

        // Exactly one of the three content FKs is set, matching Kind. Cascade so deleting
        // a draft removes its claim instead of leaving a row pointing at nothing.
        builder.HasOne(claim => claim.Job)
            .WithMany()
            .HasForeignKey(claim => claim.JobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_claims_fk_job_id");

        builder.HasOne(claim => claim.Event)
            .WithMany()
            .HasForeignKey(claim => claim.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_claims_fk_event_id");

        builder.HasOne(claim => claim.Deal)
            .WithMany()
            .HasForeignKey(claim => claim.DealId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_claims_fk_deal_id");

        builder.HasOne(claim => claim.Claimant)
            .WithMany()
            .HasForeignKey(claim => claim.ClaimantUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("content_claims_fk_claimant_user_id");

        // The poster's account box: pending first is what the screen asks for, then newest.
        builder.HasIndex(claim => new { claim.Status, claim.CreatedAt })
            .HasDatabaseName("ix_content_claims_status_created_at");

        // "Has this person already asked about this row" and "who holds the active claim"
        // — both rules the service refuses on, so both lookups need an index.
        builder.HasIndex(claim => new { claim.JobId, claim.ClaimantUserId, claim.Status })
            .HasDatabaseName("ix_content_claims_job_claimant_status");

        builder.HasIndex(claim => new { claim.EventId, claim.ClaimantUserId, claim.Status })
            .HasDatabaseName("ix_content_claims_event_claimant_status");

        builder.HasIndex(claim => new { claim.DealId, claim.ClaimantUserId, claim.Status })
            .HasDatabaseName("ix_content_claims_deal_claimant_status");

        builder.HasIndex(claim => claim.ClaimantUserId)
            .HasDatabaseName("ix_content_claims_claimant_user_id");
    }
}
