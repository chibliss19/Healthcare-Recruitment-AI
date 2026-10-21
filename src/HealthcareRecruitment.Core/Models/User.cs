namespace HealthcareRecruitment.Core.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? ResumeText { get; set; }

    // Personalized Onboarding Preferences for AI Recommendations
    public string? HealthcareSkills { get; set; }
    public string? HealthcareCertifications { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal MinHourlySalaryExpectation { get; set; }
    public string? PreferredLocationsJson { get; set; }
}