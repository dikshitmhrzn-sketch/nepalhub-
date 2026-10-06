using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(calendarEvent => calendarEvent.Id);
        builder.Property(calendarEvent => calendarEvent.Id).ValueGeneratedNever();
        builder.Property(calendarEvent => calendarEvent.Slug).HasMaxLength(160).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(200).IsRequired();

        // The description is prose, not markup, so it is long text. The character ceiling is
        // enforced by the service rather than the column: a database limit surfaces as a driver
        // error with no field name, and staff need to be told which input was too long.
        builder.Property(calendarEvent => calendarEvent.Description)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(calendarEvent => calendarEvent.Summary).HasMaxLength(400);
        builder.Property(calendarEvent => calendarEvent.Organiser).HasMaxLength(160);
        builder.Property(calendarEvent => calendarEvent.Venue).HasMaxLength(240);
        builder.Property(calendarEvent => calendarEvent.RegistrationUrl).HasMaxLength(500);

        // Nullable on purpose: an event with no capacity does not count seats, and a default
        // of some number would claim a limit the organiser never set.
        builder.Property(calendarEvent => calendarEvent.Capacity);

        // The one column the capacity check increments, so it defaults to zero rather than
        // to a value a staff member would have to remember to write.
        builder.Property(calendarEvent => calendarEvent.RegistrationCount)
            .HasDefaultValue(0)
            .IsRequired();

        // Whole rupees, like the plan catalogue price, and zero rather than null: "free"
        // is the ordinary case and a nullable column would make every reader handle both.
        builder.Property(calendarEvent => calendarEvent.TicketPrice)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(calendarEvent => calendarEvent.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(calendarEvent => calendarEvent.DistrictId).IsRequired();

        // Timestamps rather than dates, because an event that starts at seven in the evening must
        // not drop off the calendar at midnight on the day it was due to happen.
        builder.Property(calendarEvent => calendarEvent.StartsAt)
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(calendarEvent => calendarEvent.EndsAt)
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(calendarEvent => calendarEvent.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(calendarEvent => calendarEvent.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(calendarEvent => calendarEvent.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        // Set null, not cascade: an event readers have already planned around must not disappear
        // because the listing of its host business was retracted.
        builder.HasOne(calendarEvent => calendarEvent.Business)
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.BusinessId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("events_fk_business_id");

        // Restrict rather than cascade: districts are reference data every other module reads, so an
        // event must never be able to take one out of the database.
        builder.HasOne(calendarEvent => calendarEvent.District)
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.DistrictId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("events_fk_district_id");

        builder.HasOne(calendarEvent => calendarEvent.CreatedByUser)
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("events_fk_created_by_user_id");

        builder.HasIndex(calendarEvent => calendarEvent.Slug)
            .IsUnique()
            .HasDatabaseName("ix_events_slug");

        // Every public read filters on is_published first, so each composite index leads with it. A
        // plain single-column index on the second column would be useless here, because the
        // published predicate would still have to be checked row by row.
        builder.HasIndex(calendarEvent => new { calendarEvent.IsPublished, calendarEvent.StartsAt })
            .HasDatabaseName("ix_events_is_published_starts_at");

        builder.HasIndex(calendarEvent => new { calendarEvent.IsPublished, calendarEvent.EndsAt })
            .HasDatabaseName("ix_events_is_published_ends_at");

        builder.HasIndex(calendarEvent => new { calendarEvent.IsPublished, calendarEvent.Category })
            .HasDatabaseName("ix_events_is_published_category");

        // The Newest order, given an index for the same reason as the ones above: the public list is
        // sorted by creation time on every read, and a filter that has to sort before it can page
        // reads the whole published set.
        builder.HasIndex(calendarEvent => new { calendarEvent.IsPublished, calendarEvent.CreatedAt })
            .HasDatabaseName("ix_events_is_published_created_at");

        // Location filters resolve through the district, so the district leads here rather than
        // is_published: a query filters by district and published-ness together, and MySQL can use
        // whichever prefix it needs.
        builder.HasIndex(calendarEvent => new { calendarEvent.DistrictId, calendarEvent.IsPublished })
            .HasDatabaseName("ix_events_district_id_is_published");

        builder.HasIndex(calendarEvent => calendarEvent.BusinessId)
            .HasDatabaseName("ix_events_business_id");

        builder.HasIndex(calendarEvent => calendarEvent.CreatedByUserId)
            .HasDatabaseName("ix_events_created_by_user_id");
    }
}