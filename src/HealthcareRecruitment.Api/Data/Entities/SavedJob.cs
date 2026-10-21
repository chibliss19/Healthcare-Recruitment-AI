using HealthcareRecruitment.Core.Models;

namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>A user's bookmark on a vacancy, persisted server-side (replaces client-only LocalStorage bookmarks for signed-in users).</summary>
public class SavedJob
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }

    public DateTime SavedAt { get; set; }
}
