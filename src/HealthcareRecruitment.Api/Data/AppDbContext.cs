using HealthcareRecruitment.Api.Data.Entities;
using HealthcareRecruitment.Core.Models;
using Microsoft.EntityFrameworkCore;
using UserEntity = HealthcareRecruitment.Api.Data.Entities.User;

namespace HealthcareRecruitment.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<AiSession> AiSessions => Set<AiSession>();
    public DbSet<AiSessionInterviewPrep> AiSessionInterviewPreps => Set<AiSessionInterviewPrep>();
    public DbSet<AiSessionChatTurn> AiSessionChatTurns => Set<AiSessionChatTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vacancy>(entity =>
        {
            entity.Property(v => v.ExternalId).HasMaxLength(200);
            entity.Property(v => v.Source).HasMaxLength(50);
            entity.Property(v => v.Title).HasMaxLength(300);
            entity.Property(v => v.Employer).HasMaxLength(300);
            entity.Property(v => v.Location).HasMaxLength(300);
            
            // FIXED: Reduced from 900 to 255 to fit inside MySQL's 3072-byte index limit
            entity.Property(v => v.DedupeKey).HasMaxLength(255); 
            
            entity.Property(v => v.SalaryMin).HasPrecision(18, 2);
            entity.Property(v => v.SalaryMax).HasPrecision(18, 2);

            // NEW: Configures the float[] Embedding array to map safely to MySQL via JSON serialization
            entity.Property(v => v.Embedding)
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonSerializer.Deserialize<float[]>(v, (System.Text.Json.JsonSerializerOptions?)null)
                );

            // One row per listing per source; DedupeKey catches the same job cross-source.
            entity.HasIndex(v => new { v.Source, v.ExternalId }).IsUnique();
            entity.HasIndex(v => v.DedupeKey);
            entity.HasIndex(v => v.PostedDate);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.ResumeText).HasMaxLength(20000);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<SavedJob>(entity =>
        {
            entity.Property(s => s.SavedAt).IsRequired();

            entity.HasOne(s => s.User)
                .WithMany(u => u.SavedJobs)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Vacancy)
                .WithMany()
                .HasForeignKey(s => s.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can only save a given vacancy once.
            entity.HasIndex(s => new { s.UserId, s.VacancyId }).IsUnique();
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.Property(a => a.CoverLetter).HasMaxLength(4000);
            entity.Property(a => a.Phone).HasMaxLength(50);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(a => a.User)
                .WithMany(u => u.Applications)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Vacancy)
                .WithMany()
                .HasForeignKey(a => a.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);

            // One application per user per vacancy — edited in place while Draft.
            entity.HasIndex(a => new { a.UserId, a.VacancyId }).IsUnique();
        });

        modelBuilder.Entity<AiSession>(entity =>
        {
            entity.Property(s => s.ResumeSnapshot).HasMaxLength(20000).IsRequired();
            entity.Property(s => s.ResumeSummaryJson).IsRequired();
            entity.Property(s => s.RecommendationsJson).IsRequired();

            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Explicitly map relationships with cascade delete enabled so deleting a session removes child rows cleanly
            entity.HasMany(s => s.InterviewPreps)
                .WithOne(p => p.Session)
                .HasForeignKey(p => p.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.ChatTurns)
                .WithOne(t => t.Session)
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.UserId, s.UpdatedAt });
        });

        modelBuilder.Entity<AiSessionInterviewPrep>(entity =>
        {
            entity.Property(p => p.QuestionsJson).IsRequired();

            entity.HasOne(p => p.Session)
                .WithMany(s => s.InterviewPreps)
                .HasForeignKey(p => p.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Vacancy)
                .WithMany()
                .HasForeignKey(p => p.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);

            // One set of interview-prep questions per vacancy per session — re-clicking just shows the same one.
            entity.HasIndex(p => new { p.SessionId, p.VacancyId }).IsUnique();
        });

        modelBuilder.Entity<AiSessionChatTurn>(entity =>
        {
            entity.Property(t => t.Role).HasMaxLength(20).IsRequired();
            entity.Property(t => t.Content).HasMaxLength(4000).IsRequired();

            entity.HasOne(t => t.Session)
                .WithMany(s => s.ChatTurns)
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => new { t.SessionId, t.CreatedAt });
        });
    }
}