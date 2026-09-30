using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;

namespace NepalHub.Api.Data.Configurations;

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");

        builder.HasKey(district => district.Id);
        builder.Property(district => district.Id).ValueGeneratedNever();
        builder.Property(district => district.Code).HasMaxLength(80).IsRequired();
        builder.Property(district => district.Name).HasMaxLength(100).IsRequired();
        builder.Property(district => district.SortOrder).IsRequired();

        builder.HasOne(district => district.Province)
            .WithMany(province => province.Districts)
            .HasForeignKey(district => district.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("districts_fk_province_id");

        builder.HasIndex(district => district.ProvinceId)
            .HasDatabaseName("ix_districts_province_id");

        builder.HasIndex(district => district.Code)
            .IsUnique()
            .HasDatabaseName("ix_districts_code");

        builder.HasIndex(district => new { district.ProvinceId, district.SortOrder })
            .IsUnique()
            .HasDatabaseName("ix_districts_province_id_sort_order");

        builder.HasData(NepalReferenceData.Districts.Select(district => new District
        {
            Id = district.Id,
            ProvinceId = district.ProvinceId,
            Code = district.Code,
            Name = district.Name,
            SortOrder = district.SortOrder,
        }));
    }
}
