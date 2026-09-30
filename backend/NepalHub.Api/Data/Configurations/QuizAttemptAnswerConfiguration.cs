using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class QuizAttemptAnswerConfiguration : IEntityTypeConfiguration<QuizAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAttemptAnswer> builder)
    {
        builder.ToTable("quiz_attempt_answers");

        builder.HasKey(answer => new { answer.AttemptId, answer.QuestionId });
        builder.Property(answer => answer.IsCorrect).HasDefaultValue(false).IsRequired();

        builder.HasOne(answer => answer.Attempt)
            .WithMany(attempt => attempt.Answers)
            .HasForeignKey(answer => answer.AttemptId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("quiz_attempt_answers_fk_attempt_id");

        builder.HasOne(answer => answer.Question)
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("quiz_attempt_answers_fk_question_id");

        builder.HasOne(answer => answer.SelectedOption)
            .WithMany()
            .HasForeignKey(answer => answer.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("quiz_attempt_answers_fk_selected_option_id");

        // Lets the staff delete rule answer "has this question ever been played?" with an
        // index seek rather than a scan.
        builder.HasIndex(answer => answer.QuestionId)
            .HasDatabaseName("ix_quiz_attempt_answers_question_id");
    }
}
