using HealthcareRecruitment.Core.Models;

namespace HealthcareRecruitment.Api.Services.Sources;

/// <summary>A single external vacancy provider (Adzuna, Careerjet, ...).</summary>
public interface IVacancySource
{
    string Name { get; }

    /// <summary>True when the source has the configuration (API keys) it needs to run.</summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<Vacancy>> FetchAsync(CancellationToken cancellationToken);
}
