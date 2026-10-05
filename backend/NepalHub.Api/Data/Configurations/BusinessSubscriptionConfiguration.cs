using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class BusinessSubscriptionConfiguration : IEntityTypeConfiguration<BusinessSubscription>
{
    public void Configure(EntityTypeBuilder<BusinessSubscription> builder)
    {
        builder.ToTable("business_subscriptions");

        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();

        builder.Property(subscription => subscription.StartsOn).HasColumnType("date").IsRequired();
        builder.Property(subscription => subscription.EndsOn).HasColumnType("date").IsRequired();

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(BusinessSubscriptionStatus.Active);

        builder.Property(subscription => subscription.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(subscription => subscription.CancelledAt).HasColumnType("datetime(6)");

        // Cascade rather than restrict: a subscription is only ever read through the listing
        // it belongs to, and a draft listing that staff remove must not leave behind a row
        // pointing at nothing.
        builder.HasOne(subscription => subscription.Business)
            .WithMany()
            .HasForeignKey(subscription => subscription.BusinessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_subscriptions_fk_business_id");

        // Restrict rather than cascade: plans are reference data that past subscriptions
        // point at, so a subscription must never be able to take its plan out of the table.
        builder.HasOne(subscription => subscription.Plan)
            .WithMany()
            .HasForeignKey(subscription => subscription.PlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("business_subscriptions_fk_plan_id");

        // Deleting a staff account must not cancel what it granted — the entitlement is a
        // fact about the listing, not about the person who approved it.
        builder.HasOne(subscription => subscription.GrantedByUser)
            .WithMany()
            .HasForeignKey(subscription => subscription.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("business_subscriptions_fk_granted_by_user_id");

        builder.HasOne(subscription => subscription.SubscriptionRequest)
            .WithMany()
            .HasForeignKey(subscription => subscription.SubscriptionRequestId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("business_subscriptions_fk_subscription_request_id");

        // "Is anything live for this listing" runs on every read of the directory, so the
        // listing leads and the status follows it.
        builder.HasIndex(subscription => new { subscription.BusinessId, subscription.Status })
            .HasDatabaseName("ix_business_subscriptions_business_id_status");

        // The queued expiry sweep a future background job would ask for, and the admin's
        // "ending soon" read today.
        builder.HasIndex(subscription => new { subscription.Status, subscription.EndsOn })
            .HasDatabaseName("ix_business_subscriptions_status_ends_on");
    }
}
