using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class UserBadgeAwardConfiguration : IEntityTypeConfiguration<UserBadgeAward>
{
    public void Configure(EntityTypeBuilder<UserBadgeAward> builder)
    {
        builder.ToTable("user_badges");

        builder.HasKey(award => award.Id);
        builder.Property(award => award.Id).ValueGeneratedNever();

        builder.Property(award => award.AwardedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.HasOne(award => award.User)
            .WithMany()
            .HasForeignKey(award => award.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("user_badges_fk_user_id");

        builder.HasOne(award => award.BadgeDefinition)
            .WithMany()
            .HasForeignKey(award => award.BadgeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("user_badges_fk_badge_id");

        // The guarantee the whole feature rests on. A badge is earned once and never
        // twice, so a replayed submit, a retried request or two concurrent tabs all end
        // with exactly one row and no error for the person to see.
        builder.HasIndex(award => new { award.UserId, award.BadgeDefinitionId })
            .IsUnique()
            .HasDatabaseName("ix_user_badges_user_id_badge_id");

        // "My badges, newest first", which is how the account page orders the earned list.
        builder.HasIndex(award => new { award.UserId, award.AwardedAt })
            .HasDatabaseName("ix_user_badges_user_id_awarded_at");

        builder.HasIndex(award => award.BadgeDefinitionId)
            .HasDatabaseName("ix_user_badges_badge_id");
    }
}
