namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>
/// An account holder. Introduced specifically to support saved jobs and in-app
/// applications — kept separate from the anonymous, ephemeral AI profile wizard,
/// which stores no PII and no identity. This is a server-only entity (never
/// serialized directly to the client) because of <see cref="PasswordHash"/>.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Hashed via ASP.NET Core's <c>PasswordHasher&lt;User&gt;</c>. Never the plain password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Plain text extracted from an uploaded resume (PDF/Word). Powers the AI assistant's
    /// personalization — kept only for signed-in accounts, same PII boundary as the rest of this entity.</summary>
    public string? ResumeText { get; set; }

    public DateTime? ResumeUpdatedAt { get; set; }

    // Email Verification Properties
    public bool IsEmailVerified { get; set; } = false;
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiresAt { get; set; }

    // Forgot Password Properties
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public List<SavedJob> SavedJobs { get; set; } = [];

    public List<JobApplication> Applications { get; set; } = [];
}