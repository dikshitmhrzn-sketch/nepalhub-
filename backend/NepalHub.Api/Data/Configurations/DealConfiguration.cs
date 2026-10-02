using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("deals");

        builder.HasKey(deal => deal.Id);
        builder.Property(deal => deal.Id).ValueGeneratedNever();
        builder.Property(deal => deal.Slug).HasMaxLength(160).IsRequired();
        builder.Property(deal => deal.Title).HasMaxLength(200).IsRequired();

        // The description is prose, not markup, so it is long text. The character ceiling is
        // enforced by the service rather than the column: a database limit surfaces as a driver
        // error with no field name, and staff need to be told which input was too long.
        builder.Property(deal => deal.Description)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(deal => deal.Summary).HasMaxLength(400);
        builder.Property(deal => deal.DiscountText).HasMaxLength(120);
        builder.Property(deal => deal.RedemptionUrl).HasMaxLength(500);

        // Date-only columns for a day-granular offer. The range is required rather than nullable,
        // so there is no "no end date" branch for the expiry filter to forget to handle.
        builder.Property(deal => deal.StartsOn).HasColumnType("date").IsRequired();
        builder.Property(deal => deal.EndsOn).HasColumnType("date").IsRequired();

        builder.Property(deal => deal.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(deal => deal.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(deal => deal.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        // Set null, not cascade: a deal is public copy a reader may have saved, so a retracted
        // business must leave its offers standing rather than delete them.
        builder.HasOne(deal => deal.Business)
            .WithMany()
            .HasForeignKey(deal => deal.BusinessId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("deals_fk_business_id");

        // Restrict rather than cascade: districts are reference data every other module reads, so a
        // deal must never be able to take one out of the database.
        builder.HasOne(deal => deal.District)
            .WithMany()
            .HasForeignKey(deal => deal.DistrictId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("deals_fk_district_id");

        builder.HasOne(deal => deal.CreatedByUser)
            .WithMany()
            .HasForeignKey(deal => deal.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("deals_fk_created_by_user_id");

        builder.HasIndex(deal => deal.Slug)
            .IsUnique()
            .HasDatabaseName("ix_deals_slug");

        // Every public read filters on is_published first, so each composite index leads with it. A
        // plain single-column index on the second column would be useless here, because the
        // published predicate would still have to be checked row by row.
        builder.HasIndex(deal => new { deal.IsPublished, deal.EndsOn })
            .HasDatabaseName("ix_deals_is_published_ends_on");

        builder.HasIndex(deal => new { deal.IsPublished, deal.StartsOn })
            .HasDatabaseName("ix_deals_is_published_starts_on");

        // The Newest order, given an index for the same reason as the ones above: the public list is
        // sorted by creation time on every read, and a filter that has to sort before it can page
        // reads the whole published set.
        builder.HasIndex(deal => new { deal.IsPublished, deal.CreatedAt })
            .HasDatabaseName("ix_deals_is_published_created_at");

        // Location filters resolve through the district, so the district leads here rather than
        // is_published: a query filters by district and published-ness together, and MySQL can use
        // whichever prefix it needs.
        builder.HasIndex(deal => new { deal.DistrictId, deal.IsPublished })
            .HasDatabaseName("ix_deals_district_id_is_published");

        builder.HasIndex(deal => deal.BusinessId)
            .HasDatabaseName("ix_deals_business_id");

        builder.HasIndex(deal => deal.CreatedByUserId)
            .HasDatabaseName("ix_deals_created_by_user_id");
    }
}