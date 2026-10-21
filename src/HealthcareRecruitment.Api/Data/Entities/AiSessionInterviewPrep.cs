namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>Interview-prep questions generated for one vacancy within an <see cref="AiSession"/>.
/// One row per vacancy the user opened "Interview prep" for during that session.</summary>
public class AiSessionInterviewPrep
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public AiSession? Session { get; set; }

    public int VacancyId { get; set; }
    public HealthcareRecruitment.Core.Models.Vacancy? Vacancy { get; set; }

    /// <summary>Serialized <c>List&lt;InterviewQuestion&gt;</c>.</summary>
    public string QuestionsJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
