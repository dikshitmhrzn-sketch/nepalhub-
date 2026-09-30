using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Configurations;

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("quiz_questions");

        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).ValueGeneratedNever();
        builder.Property(question => question.Text).HasMaxLength(600).IsRequired();
        builder.Property(question => question.Explanation).HasMaxLength(1500).IsRequired();
        builder.Property(question => question.Category)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(question => question.IsPublished).HasDefaultValue(false).IsRequired();

        builder.Property(question => question.CreatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(question => question.UpdatedAt)
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne(question => question.CreatedByUser)
            .WithMany()
            .HasForeignKey(question => question.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("quiz_questions_fk_created_by_user_id");

        // The daily selection always reads "published questions, oldest first", because
        // the rotation walks the id sequence to keep each day different from the last.
        builder.HasIndex(question => new { question.IsPublished, question.Id })
            .HasDatabaseName("ix_quiz_questions_is_published_id");

        builder.HasIndex(question => question.Category)
            .HasDatabaseName("ix_quiz_questions_category");

        builder.HasIndex(question => question.CreatedByUserId)
            .HasDatabaseName("ix_quiz_questions_created_by_user_id");
    }
}
