using HealthcareRecruitment.Core.Models;

namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>
/// An application to a vacancy that lives inside the app: the user can save it as a
/// draft and resume later, then submit. Submission is recorded in our own database only —
/// Adzuna/Careerjet expose no API to submit an application on the applicant's behalf, so
/// the vacancy's original external link remains where they'd actually complete it with
/// the employer if required.
/// </summary>
public class JobApplication
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int VacancyId { get; set; }
    public HealthcareRecruitment.Core.Models.Vacancy? Vacancy { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public string? CoverLetter { get; set; }

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
}