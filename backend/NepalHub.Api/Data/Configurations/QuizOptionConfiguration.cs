using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class QuizOptionConfiguration : IEntityTypeConfiguration<QuizOption>
{
    public void Configure(EntityTypeBuilder<QuizOption> builder)
    {
        builder.ToTable("quiz_options");

        builder.HasKey(option => option.Id);
        builder.Property(option => option.Id).ValueGeneratedNever();
        builder.Property(option => option.Position).IsRequired();
        builder.Property(option => option.Text).HasMaxLength(300).IsRequired();
        builder.Property(option => option.IsCorrect).HasDefaultValue(false).IsRequired();

        builder.HasOne(option => option.Question)
            .WithMany(question => question.Options)
            .HasForeignKey(option => option.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("quiz_options_fk_question_id");

        // A question is rewritten wholesale on every staff save, so the natural key that
        // has to hold is "one row per position within a question".
        builder.HasIndex(option => new { option.QuestionId, option.Position })
            .IsUnique()
            .HasDatabaseName("ix_quiz_options_question_id_position");
    }
}
