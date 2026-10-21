using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HealthcareRecruitment.Core.Models;

// --------------------------------------------------------
// ENUMS (Replaces magic strings to prevent typos)
// --------------------------------------------------------
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatRole
{
    User,
    Assistant,
    System
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionCategory
{
    Technical,
    Behavioural,
    Situational,
    Clinical
}

// --------------------------------------------------------
// RESUME & PROFILE ANALYSIS
// --------------------------------------------------------
/// <summary>"Here's what we know about you" — the AI's read of an uploaded resume.</summary>
public class ResumeSummaryResponse
{
    public string Summary { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public List<string> Highlights { get; set; } = [];
}

// --------------------------------------------------------
// INTERVIEW PREPARATION
// --------------------------------------------------------
public class InterviewPrepResponse
{
    public int VacancyId { get; set; }
    
    [Required]
    public string JobTitle { get; set; } = string.Empty;
    
    public List<InterviewQuestion> Questions { get; set; } = [];
}

public class InterviewQuestion
{
    [Required]
    public string Question { get; set; } = string.Empty;

    /// <summary>Suggested talking point grounded in the candidate's own resume — not a scripted answer.</summary>
    public string SuggestedAnswerAngle { get; set; } = string.Empty;

    /// <summary>Categorizes the question type for UI filtering/tagging.</summary>
    public QuestionCategory Category { get; set; } = QuestionCategory.Behavioural;
}

// --------------------------------------------------------
// CHAT SESSION MODELS
// --------------------------------------------------------
/// <summary>One turn in an AI session's chat.</summary>
public class InterviewChatMessage
{
    public ChatRole Role { get; set; } = ChatRole.User; 
    
    [Required, MinLength(1)]
    public string Content { get; set; } = string.Empty;
}

public class InterviewChatResponse
{
    public string Reply { get; set; } = string.Empty;
}

public class SendSessionMessageRequest
{
    [Required(ErrorMessage = "Message cannot be empty.")]
    public string Message { get; set; } = string.Empty;
}

// --------------------------------------------------------
// GENERATION DTOs (Cover Letter & Statements)
// --------------------------------------------------------
public class StatementRequestDto
{
    [Required]
    public string TargetVacancyId { get; set; } = string.Empty;
}

public class StatementResponseDto
{
    public string? Statement { get; set; }
}

public class CoverLetterRequestDto
{
    [Required]
    public string JobTitle { get; set; } = string.Empty;
    [Required]
    public string CompanyName { get; set; } = string.Empty;
    [Required]
    public string ApplicantName { get; set; } = string.Empty;
    public string KeySkills { get; set; } = string.Empty;
}

// --------------------------------------------------------
// SESSION HISTORY & STATE
// --------------------------------------------------------
/// <summary>One saved AI Career Assistant session, listed without its full content — for the History sidebar.</summary>
public class AiSessionSummary
{
    public int Id { get; set; }
    
    [Required]
    public string Title { get; set; } = "New Session"; // Added so the sidebar has a bold header
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>First sentence of the resume summary, for display in the history list.</summary>
    [MaxLength(150)]
    public string Preview { get; set; } = string.Empty;
    
    public bool IsPinned { get; set; }
}

/// <summary>A session restored exactly as it was: the resume summary, the recommendations, every
/// interview-prep set generated, and the full chat — nothing regenerated, all frozen at the time.</summary>
public class AiSessionDetail
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public ResumeSummaryResponse ResumeSummary { get; set; } = new();
    
    // Assuming ProfileAnalysisResult is defined elsewhere in your Core.Models
    public ProfileAnalysisResult Recommendations { get; set; } = new();
    
    public List<InterviewPrepResponse> InterviewPreps { get; set; } = [];
    public List<InterviewChatMessage> ChatTurns { get; set; } = [];
}