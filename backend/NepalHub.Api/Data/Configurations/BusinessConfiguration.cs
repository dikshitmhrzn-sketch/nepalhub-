using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");

        builder.HasKey(business => business.Id);
        builder.Property(business => business.Id).ValueGeneratedNever();
        builder.Property(business => business.Slug).HasMaxLength(160).IsRequired();
        builder.Property(business => business.Name).HasMaxLength(160).IsRequired();

        // The description is prose, not markup, so it is long text. The character ceiling is
        // enforced by the service rather than the column: a database limit surfaces as a driver
        // error with no field name, and staff need to be told which input was too long.
        builder.Property(business => business.Description)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(business => business.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(business => business.DistrictId).IsRequired();
        builder.Property(business => business.Address).HasMaxLength(240);
        builder.Property(business => business.Phone).HasMaxLength(20);
        builder.Property(business => business.Email).HasMaxLength(254);
        builder.Property(business => business.Website).HasMaxLength(500);
        builder.Property(business => business.IsPublished).HasDefaultValue(false).IsRequired();
        builder.Property(business => business.IsVerified).HasDefaultValue(false).IsRequired();

        builder.Property(business => business.VerifiedAt).HasColumnType("datetime(6)");

        builder.Property(business => business.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(business => business.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        // Restrict rather than cascade: districts are reference data every other module reads,
        // so a listing must never be able to take one out of the database.
        builder.HasOne(business => business.District)
            .WithMany()
            .HasForeignKey(business => business.DistrictId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("businesses_fk_district_id");

        builder.HasOne(business => business.CreatedByUser)
            .WithMany()
            .HasForeignKey(business => business.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("businesses_fk_created_by_user_id");

        builder.HasIndex(business => business.Slug)
            .IsUnique()
            .HasDatabaseName("ix_businesses_slug");

        // Every public read filters on is_published first, so each composite index leads with it. A
        // plain single-column index on the second column would be useless here, because the
        // published predicate would still have to be checked row by row.
        builder.HasIndex(business => new { business.IsPublished, business.Name })
            .HasDatabaseName("ix_businesses_is_published_name");

        builder.HasIndex(business => new { business.IsPublished, business.Category })
            .HasDatabaseName("ix_businesses_is_published_category");

        // The verified badge is the one filter that has no single-column equivalent, because
        // "verified and not verified" is asked about almost every time the list is opened.
        builder.HasIndex(business => new { business.IsPublished, business.IsVerified })
            .HasDatabaseName("ix_businesses_is_published_verified");

        builder.HasIndex(business => new { business.IsPublished, business.CreatedAt })
            .HasDatabaseName("ix_businesses_is_published_created_at");

        // Location filters resolve through the district, so the district leads here rather than
        // is_published: a query filters by district and published-ness together, and MySQL can use
        // whichever prefix it needs.
        builder.HasIndex(business => new { business.DistrictId, business.IsPublished })
            .HasDatabaseName("ix_businesses_district_id_is_published");

        builder.HasIndex(business => business.CreatedByUserId)
            .HasDatabaseName("ix_businesses_created_by_user_id");
    }
}