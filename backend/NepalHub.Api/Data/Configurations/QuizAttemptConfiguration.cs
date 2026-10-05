using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts");

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.Score).IsRequired();
        builder.Property(attempt => attempt.MaxScore).IsRequired();
        builder.Property(attempt => attempt.XpAwarded).IsRequired();

        builder.Property(attempt => attempt.CompletedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.HasOne(attempt => attempt.User)
            .WithMany()
            .HasForeignKey(attempt => attempt.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("quiz_attempts_fk_user_id");

        builder.HasOne(attempt => attempt.DailyQuiz)
            .WithMany()
            .HasForeignKey(attempt => attempt.DailyQuizId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("quiz_attempts_fk_daily_quiz_id");

        // The whole integrity model rests on this: one completed run per person per day.
        // It is what makes a second submit a 409 instead of a second XP payout.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.DailyQuizId })
            .IsUnique()
            .HasDatabaseName("ix_quiz_attempts_user_id_daily_quiz_id");

        // "My quiz history, newest first", which the account page will need later.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.CompletedAt })
            .HasDatabaseName("ix_quiz_attempts_user_id_completed_at");

        builder.HasIndex(attempt => attempt.DailyQuizId)
            .HasDatabaseName("ix_quiz_attempts_daily_quiz_id");

        // The leaderboard's period queries ask "every attempt since a Nepal day or week
        // boundary, summed per user". The history index above starts at user_id and cannot
        // serve that seek, so without this one the aggregate scans the whole attempt table
        // -- the exact query the leaderboard exists to avoid.
        builder.HasIndex(attempt => attempt.CompletedAt)
            .HasDatabaseName("ix_quiz_attempts_completed_at");
    }
}
