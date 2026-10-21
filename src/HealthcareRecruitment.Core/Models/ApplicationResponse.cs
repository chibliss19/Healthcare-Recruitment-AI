namespace HealthcareRecruitment.Core.Models;

public class ApplicationResponse
{
    public int VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = new();
    public ApplicationStatus Status { get; set; }
    public string? CoverLetter { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>Body for saving a draft. Submitting is a separate call — see POST /api/applications/{vacancyId}/submit.</summary>
public class ApplicationUpsertRequest
{
    public string? CoverLetter { get; set; }
    public string? Phone { get; set; }
}
