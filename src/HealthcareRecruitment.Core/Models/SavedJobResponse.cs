namespace HealthcareRecruitment.Core.Models;

public class SavedJobResponse
{
    public int VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = new();
    public DateTime SavedAt { get; set; }
}
