using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class BusinessClaimConfiguration : IEntityTypeConfiguration<BusinessClaim>
{
    public void Configure(EntityTypeBuilder<BusinessClaim> builder)
    {
        builder.ToTable("business_claims");

        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.Id).ValueGeneratedNever();

        builder.Property(claim => claim.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(BusinessClaimStatus.Pending);

        builder.Property(claim => claim.Evidence).HasMaxLength(500);
        builder.Property(claim => claim.ReviewNote).HasMaxLength(500);

        builder.Property(claim => claim.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(claim => claim.ReviewedAt).HasColumnType("datetime(6)");

        // An approval writes ownership onto the listing, so the listing must outlive the
        // claim: the record of who was approved is worth keeping even once the row that
        // proved it is no longer the subject of anything.
        builder.HasOne(claim => claim.Business)
            .WithMany()
            .HasForeignKey(claim => claim.BusinessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_claims_fk_business_id");

        builder.HasOne(claim => claim.Claimant)
            .WithMany()
            .HasForeignKey(claim => claim.ClaimantUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_claims_fk_claimant_user_id");

        // Deleting a staff account must not re-open the question of who owns a listing.
        builder.HasOne(claim => claim.ReviewedBy)
            .WithMany()
            .HasForeignKey(claim => claim.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("business_claims_fk_reviewed_by_user_id");

        // The queue itself: pending first is what the screen asks for, then newest.
        builder.HasIndex(claim => new { claim.Status, claim.CreatedAt })
            .HasDatabaseName("ix_business_claims_status_created_at");

        // "Has this person already asked about this listing" — the rule the service
        // refuses on, so the lookup needs an index rather than a scan.
        builder.HasIndex(claim => new { claim.BusinessId, claim.ClaimantUserId, claim.Status })
            .HasDatabaseName("ix_business_claims_business_claimant_status");
    }
}
