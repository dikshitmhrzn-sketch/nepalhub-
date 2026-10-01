using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Province> Provinces => Set<Province>();

    public DbSet<District> Districts => Set<District>();

    public DbSet<Calculator> Calculators => Set<Calculator>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();

    public DbSet<QuizOption> QuizOptions => Set<QuizOption>();

    public DbSet<DailyQuiz> DailyQuizzes => Set<DailyQuiz>();

    public DbSet<DailyQuizQuestion> DailyQuizQuestions => Set<DailyQuizQuestion>();

    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    public DbSet<QuizAttemptAnswer> QuizAttemptAnswers => Set<QuizAttemptAnswer>();

    public DbSet<StudentResource> StudentResources => Set<StudentResource>();

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IdentityUserRole<Guid>>(builder =>
        {
            builder.ToTable("user_roles");
            builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
            builder.HasIndex(userRole => userRole.RoleId).HasDatabaseName("ix_user_roles_role_id");
            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userRole => userRole.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_roles_fk_user_id");
            builder
                .HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(userRole => userRole.RoleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_roles_fk_role_id");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(builder =>
        {
            builder.ToTable("user_claims");
            builder.HasKey(claim => claim.Id);
            builder.HasIndex(claim => claim.UserId).HasDatabaseName("ix_user_claims_user_id");
            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(claim => claim.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_claims_fk_user_id");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(builder =>
        {
            builder.ToTable("user_logins");
            builder.HasKey(login => new { login.LoginProvider, login.ProviderKey });
            builder.HasIndex(login => login.UserId).HasDatabaseName("ix_user_logins_user_id");
            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(login => login.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_logins_fk_user_id");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(builder =>
        {
            builder.ToTable("role_claims");
            builder.HasKey(claim => claim.Id);
            builder.HasIndex(claim => claim.RoleId).HasDatabaseName("ix_role_claims_role_id");
            builder
                .HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(claim => claim.RoleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("role_claims_fk_role_id");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(builder =>
        {
            builder.ToTable("user_tokens");
            builder.HasKey(token => new { token.UserId, token.LoginProvider, token.Name });
            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_tokens_fk_user_id");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
