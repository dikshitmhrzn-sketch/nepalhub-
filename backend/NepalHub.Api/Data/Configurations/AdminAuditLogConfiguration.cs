using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("admin_audit_logs");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.Action).HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.TargetType).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.TargetId).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Ip).HasMaxLength(45);

        // JSON snapshots, unbounded on purpose: a before/after pair for a row with a long
        // description must never be silently truncated, because a truncated audit trail
        // reads as a complete one.
        builder.Property(entry => entry.Before);
        builder.Property(entry => entry.After);

        builder.Property(entry => entry.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.HasOne(entry => entry.AdminUser)
            .WithMany()
            .HasForeignKey(entry => entry.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("admin_audit_logs_fk_admin_user_id");

        // Reading the log is always newest-first over a window of time.
        builder.HasIndex(entry => entry.CreatedAt)
            .HasDatabaseName("ix_admin_audit_logs_created_at");

        // "What did this administrator do?" without scanning the whole table.
        builder.HasIndex(entry => entry.AdminUserId)
            .HasDatabaseName("ix_admin_audit_logs_admin_user_id");

        // "Who has ever touched this row?" for the subject of an action.
        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId })
            .HasDatabaseName("ix_admin_audit_logs_target");
    }
}
