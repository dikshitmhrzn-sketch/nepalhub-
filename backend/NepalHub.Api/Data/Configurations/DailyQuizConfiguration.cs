using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class DailyQuizConfiguration : IEntityTypeConfiguration<DailyQuiz>
{
    public void Configure(EntityTypeBuilder<DailyQuiz> builder)
    {
        builder.ToTable("daily_quizzes");

        builder.HasKey(quiz => quiz.Id);
        builder.Property(quiz => quiz.Id).ValueGeneratedNever();
        builder.Property(quiz => quiz.QuizDate).HasColumnType("date").IsRequired();
        builder.Property(quiz => quiz.QuestionCount).IsRequired();

        builder.Property(quiz => quiz.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        // One quiz per date. The service relies on this to make a repeated materialisation
        // a no-op even if two visitors race on the first request of the day.
        builder.HasIndex(quiz => quiz.QuizDate)
            .IsUnique()
            .HasDatabaseName("ix_daily_quizzes_quiz_date");
    }
}
