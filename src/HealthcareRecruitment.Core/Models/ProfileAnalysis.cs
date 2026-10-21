namespace HealthcareRecruitment.Core.Models;

/// <summary>Result of matching a candidate (now: their resume, via the AI Career Assistant) against current vacancies.</summary>
public class ProfileAnalysisResult
{
    /// <summary>One-paragraph overview of how the profile fits the current market.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Skills the candidate already has that employers are asking for.</summary>
    public List<string> Strengths { get; set; } = [];

    /// <summary>Skills or qualifications the candidate should develop to widen their options.</summary>
    public List<string> SkillsGaps { get; set; } = [];

    public List<MatchedVacancy> Matches { get; set; } = [];
}

public class MatchedVacancy
{
    /// <summary>Id of the vacancy in our database — must be one of the ids the API supplied to the model.</summary>
    public int VacancyId { get; set; }

    /// <summary>0–100 suitability score.</summary>
    public int MatchScore { get; set; }

    /// <summary>Why this vacancy suits the candidate.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Populated server-side from the database after validation, not by the model.</summary>
    public Vacancy? Vacancy { get; set; }
}
