using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class EventBookingConfiguration : IEntityTypeConfiguration<EventBooking>
{
    public void Configure(EntityTypeBuilder<EventBooking> builder)
    {
        builder.ToTable("event_bookings");

        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();

        builder.Property(booking => booking.Quantity).HasDefaultValue(1).IsRequired();

        builder.Property(booking => booking.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(EventBookingStatus.Confirmed);

        builder.Property(booking => booking.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(booking => booking.CancelledAt).HasColumnType("datetime(6)");

        // Cascade: there is no booking without the event, and an event staff remove must
        // not leave places behind pointing at a row that is gone.
        builder.HasOne(booking => booking.Event)
            .WithMany()
            .HasForeignKey(booking => booking.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("event_bookings_fk_event_id");

        // Deleting an account takes its bookings with it. The event's stored
        // registration_count is a deliberate counter rather than a row count precisely so
        // that no such deletion has to find and adjust every event it touched.
        builder.HasOne(booking => booking.Attendee)
            .WithMany()
            .HasForeignKey(booking => booking.AttendeeUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("event_bookings_fk_attendee_user_id");

        // One row per person per event for all time, cancelled or not. Booking again
        // reactivates the row, so a place can never be held twice by one account even
        // when two tabs submit at the same moment.
        builder.HasIndex(booking => new { booking.EventId, booking.AttendeeUserId })
            .IsUnique()
            .HasDatabaseName("ix_event_bookings_event_attendee");

        // The admin's attendee list and the event's own "who is coming" read.
        builder.HasIndex(booking => new { booking.EventId, booking.Status })
            .HasDatabaseName("ix_event_bookings_event_id_status");
    }
}
