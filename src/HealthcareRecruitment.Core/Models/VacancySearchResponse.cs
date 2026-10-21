namespace HealthcareRecruitment.Core.Models;

public class VacancySearchResponse
{
    public List<Vacancy> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}

/// <summary>Distinct filter values for the dashboard dropdowns.</summary>
public class VacancyFilterOptions
{
    public List<string> Locations { get; set; } = [];
    public List<string> Sources { get; set; } = [];
}
