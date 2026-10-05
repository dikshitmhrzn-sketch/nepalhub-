using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class SubscriptionRequestConfiguration : IEntityTypeConfiguration<SubscriptionRequest>
{
    public void Configure(EntityTypeBuilder<SubscriptionRequest> builder)
    {
        builder.ToTable("subscription_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();

        builder.Property(request => request.Note).HasMaxLength(500);
        builder.Property(request => request.ReviewNote).HasMaxLength(500);

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(SubscriptionRequestStatus.Pending);

        builder.Property(request => request.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(request => request.ReviewedAt).HasColumnType("datetime(6)");

        // Cascade: the request is only ever read as part of the listing it is about, and
        // removing a draft listing leaves nothing worth keeping.
        builder.HasOne(request => request.Business)
            .WithMany()
            .HasForeignKey(request => request.BusinessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("subscription_requests_fk_business_id");

        // Restrict rather than cascade: plans are reference data, and approving a request
        // writes a subscription that points at this row forever after.
        builder.HasOne(request => request.Plan)
            .WithMany()
            .HasForeignKey(request => request.PlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("subscription_requests_fk_plan_id");

        builder.HasOne(request => request.Requester)
            .WithMany()
            .HasForeignKey(request => request.RequesterUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("subscription_requests_fk_requester_user_id");

        // Deleting a staff account must not silently drop the requests it decided on.
        builder.HasOne(request => request.ReviewedBy)
            .WithMany()
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("subscription_requests_fk_reviewed_by_user_id");

        // The queue itself: pending first is what the screen asks for, then newest.
        builder.HasIndex(request => new { request.Status, request.CreatedAt })
            .HasDatabaseName("ix_subscription_requests_status_created_at");

        builder.HasIndex(request => new { request.BusinessId, request.Status })
            .HasDatabaseName("ix_subscription_requests_business_id_status");
    }
}
