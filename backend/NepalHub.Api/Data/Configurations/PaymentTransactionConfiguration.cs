using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();

        builder.Property(payment => payment.Reference).HasMaxLength(40).IsRequired();
        builder.Property(payment => payment.Description).HasMaxLength(200).IsRequired();
        builder.Property(payment => payment.FailureCode).HasMaxLength(40);
        builder.Property(payment => payment.Provider).HasMaxLength(16).IsRequired().HasDefaultValue("esewa");
        builder.Property(payment => payment.ProviderReference).HasMaxLength(64);
        builder.Property(payment => payment.Ip).HasMaxLength(45);

        builder.Property(payment => payment.AmountNpr).IsRequired();

        builder.Property(payment => payment.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PaymentStatus.Pending);

        builder.Property(payment => payment.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(payment => payment.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(payment => payment.SettledAt).HasColumnType("datetime(6)");
        builder.Property(payment => payment.RefundedAt).HasColumnType("datetime(6)");

        // Money rows outlive the accounts and the requests they were raised against: this
        // application deactivates users rather than deleting them, and a receipt that
        // vanished because somebody removed a plan row is a receipt nobody can answer for.
        builder.HasOne(payment => payment.User)
            .WithMany()
            .HasForeignKey(payment => payment.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payment_transactions_fk_user_id");

        builder.HasOne(payment => payment.SubscriptionRequest)
            .WithMany()
            .HasForeignKey(payment => payment.SubscriptionRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payment_transactions_fk_subscription_request_id");

        builder.HasOne(payment => payment.EventBooking)
            .WithMany()
            .HasForeignKey(payment => payment.EventBookingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payment_transactions_fk_event_booking_id");

        // The reference is the idempotency anchor: a callback, a status check and an admin
        // retry all arrive through the same string and must find one row, never two.
        builder.HasIndex(payment => payment.Reference)
            .IsUnique()
            .HasDatabaseName("ix_payment_transactions_reference");

        // The buyer's own history screen and the admin list both page newest-first.
        builder.HasIndex(payment => new { payment.UserId, payment.CreatedAt })
            .HasDatabaseName("ix_payment_transactions_user_id_created_at");

        builder.HasIndex(payment => payment.Status)
            .HasDatabaseName("ix_payment_transactions_status");

        builder.HasIndex(payment => payment.SubscriptionRequestId)
            .HasDatabaseName("ix_payment_transactions_subscription_request_id");

        builder.HasIndex(payment => payment.EventBookingId)
            .HasDatabaseName("ix_payment_transactions_event_booking_id");
    }
}
