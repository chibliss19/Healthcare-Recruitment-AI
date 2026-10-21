namespace HealthcareRecruitment.Core.Models;

/// <summary>
/// A healthcare job vacancy aggregated from an external source (Adzuna, Careerjet).
/// Deliberately contains no PII fields — only public listing data.
/// </summary>
public class Vacancy
{
    public int Id { get; set; }

    /// <summary>Identifier of the listing on the source API.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Which API this listing came from, e.g. "Adzuna" or "Careerjet".</summary>
    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Employer { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    /// <summary>Short listing description/snippet as provided by the source.</summary>
    public string? Description { get; set; }

    /// <summary>Link to the original listing so users can apply at the source.</summary>
    public string? Url { get; set; }

    public DateTime? PostedDate { get; set; }

    /// <summary>When the aggregation service last saw this listing.</summary>
    public DateTime FetchedAt { get; set; }

    /// <summary>Normalised title+employer+location key used for cross-source deduplication.</summary>
    public string DedupeKey { get; set; } = string.Empty;
    // ... your existing properties (Id, Title, Description, etc.)

    // Stores the vector float array from Ollama (e.g., nomic-embed-text)
    public float[]? Embedding { get; set; }

}
