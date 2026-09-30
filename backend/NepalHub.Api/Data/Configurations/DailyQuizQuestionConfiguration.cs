using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class DailyQuizQuestionConfiguration : IEntityTypeConfiguration<DailyQuizQuestion>
{
    public void Configure(EntityTypeBuilder<DailyQuizQuestion> builder)
    {
        builder.ToTable("daily_quiz_questions");

        // A question can appear at most once in a given day's quiz, and a quiz cannot list
        // the same question twice. Both are the same constraint.
        builder.HasKey(link => new { link.DailyQuizId, link.QuestionId });

        builder.Property(link => link.Position).IsRequired();

        builder.HasOne(link => link.DailyQuiz)
            .WithMany(quiz => quiz.Questions)
            .HasForeignKey(link => link.DailyQuizId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("daily_quiz_questions_fk_daily_quiz_id");

        // Restrict rather than cascade: a question that has been played is part of somebody's
        // history, and the staff delete rule refuses to remove it anyway.
        builder.HasOne(link => link.Question)
            .WithMany()
            .HasForeignKey(link => link.QuestionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("daily_quiz_questions_fk_question_id");

        // Reading a day's quiz is always "position order for this quiz".
        builder.HasIndex(link => new { link.DailyQuizId, link.Position })
            .HasDatabaseName("ix_daily_quiz_questions_daily_quiz_id_position");

        builder.HasIndex(link => link.QuestionId)
            .HasDatabaseName("ix_daily_quiz_questions_question_id");
    }
}
