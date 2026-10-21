namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>
/// One frozen "pass" through the AI Career Assistant — resume snapshot, the recommendations
/// generated from it, and the chat about them, all bundled together. "New session" or
/// "Re-upload" always starts a brand new one; nothing carries over from the previous session.
/// History lists these and restores the exact snapshot, not a freshly regenerated one.
/// </summary>
public class AiSession
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>The resume text this session was built from — frozen even if the user's
    /// account-level resume is later replaced by a different session's re-upload.</summary>
    public string ResumeSnapshot { get; set; } = string.Empty;

    /// <summary>Serialized <c>ResumeSummaryResponse</c> — frozen at generation time.</summary>
    public string ResumeSummaryJson { get; set; } = string.Empty;

    /// <summary>Serialized <c>ProfileAnalysisResult</c> (without the Vacancy nav property —
    /// re-attached from the database at read time) — frozen at generation time.</summary>
    public string RecommendationsJson { get; set; } = string.Empty;

    /// <summary>Indicates whether the session is pinned to the top of the history list.</summary>
    public bool IsPinned { get; set; } = false;

    public List<AiSessionInterviewPrep> InterviewPreps { get; set; } = [];
    public List<AiSessionChatTurn> ChatTurns { get; set; } = [];
}