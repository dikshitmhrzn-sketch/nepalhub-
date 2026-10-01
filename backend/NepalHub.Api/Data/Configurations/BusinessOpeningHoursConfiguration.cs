using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class BusinessOpeningHoursConfiguration : IEntityTypeConfiguration<BusinessOpeningHours>
{
    public void Configure(EntityTypeBuilder<BusinessOpeningHours> builder)
    {
        builder.ToTable("business_hours");

        // The composite key is the whole integrity model of this table: one row per weekday per
        // business, guaranteed by the database rather than by a service check that a future write
        // path could forget to call.
        builder.HasKey(hours => new { hours.BusinessId, hours.DayOfWeek });

        builder.Property(hours => hours.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(hours => hours.IsClosed).HasDefaultValue(false).IsRequired();
        builder.Property(hours => hours.OpensAt).HasColumnType("time(6)");
        builder.Property(hours => hours.ClosesAt).HasColumnType("time(6)");

        // Cascade is right here, and it is the opposite of the district foreign key on the parent.
        // Hours have no meaning without the listing they describe and nothing else references them,
        // so they should disappear with it; districts are shared reference data and must not.
        builder.HasOne(hours => hours.Business)
            .WithMany(business => business.OpeningHours)
            .HasForeignKey(hours => hours.BusinessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("business_hours_fk_business_id");
    }
}