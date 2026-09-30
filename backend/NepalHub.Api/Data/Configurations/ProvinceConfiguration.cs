using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;

namespace NepalHub.Api.Data.Configurations;

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces");

        builder.HasKey(province => province.Id);
        builder.Property(province => province.Id).ValueGeneratedNever();
        builder.Property(province => province.Code).HasMaxLength(50).IsRequired();
        builder.Property(province => province.Name).HasMaxLength(100).IsRequired();
        builder.Property(province => province.SortOrder).IsRequired();

        builder.HasIndex(province => province.Code)
            .IsUnique()
            .HasDatabaseName("ix_provinces_code");

        builder.HasIndex(province => province.Name)
            .IsUnique()
            .HasDatabaseName("ix_provinces_name");

        builder.HasData(NepalReferenceData.Provinces.Select(province => new Province
        {
            Id = province.Id,
            Code = province.Code,
            Name = province.Name,
            SortOrder = province.SortOrder,
        }));
    }
}
