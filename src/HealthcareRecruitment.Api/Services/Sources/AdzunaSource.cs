using System.Text.Json;
using System.Text.Json.Serialization;
using HealthcareRecruitment.Core.Models;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Sources;

public class AdzunaOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    /// <summary>Adzuna country code; "gb" covers Northern Ireland.</summary>
    public string Country { get; set; } = "gb";
    public string Where { get; set; } = "Northern Ireland";
    public string Category { get; set; } = "healthcare-nursing-jobs";
    public int ResultsPerPage { get; set; } = 50;
    public int MaxPages { get; set; } = 3;
}

public class AdzunaSource(HttpClient http, IOptions<AdzunaOptions> options, ILogger<AdzunaSource> logger) : IVacancySource
{
    private readonly AdzunaOptions _options = options.Value;

    public string Name => "Adzuna";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AppId) && !string.IsNullOrWhiteSpace(_options.AppKey);

    public async Task<IReadOnlyList<Vacancy>> FetchAsync(CancellationToken cancellationToken)
    {
        var results = new List<Vacancy>();
        for (var page = 1; page <= _options.MaxPages; page++)
        {
            var url = $"https://api.adzuna.com/v1/api/jobs/{_options.Country}/search/{page}" +
                      $"?app_id={Uri.EscapeDataString(_options.AppId)}" +
                      $"&app_key={Uri.EscapeDataString(_options.AppKey)}" +
                      $"&results_per_page={_options.ResultsPerPage}" +
                      $"&where={Uri.EscapeDataString(_options.Where)}" +
                      $"&category={Uri.EscapeDataString(_options.Category)}" +
                      "&content-type=application/json";

            // Adzuna sends a non-standard "utf8" charset (instead of "utf-8") in its Content-Type
            // header, which GetFromJsonAsync's encoding sniffing rejects. Read the raw bytes instead.
            using var httpResponse = await http.GetAsync(url, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();
            var bytes = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var response = JsonSerializer.Deserialize<AdzunaResponse>(bytes);
            if (response?.Results is not { Count: > 0 })
            {
                break;
            }

            foreach (var job in response.Results)
            {
                if (string.IsNullOrWhiteSpace(job.Id) || string.IsNullOrWhiteSpace(job.Title))
                {
                    continue;
                }

                var employer = job.Company?.DisplayName ?? "Unknown employer";
                var location = job.Location?.DisplayName ?? _options.Where;
                results.Add(new Vacancy
                {
                    ExternalId = job.Id,
                    Source = Name,
                    Title = job.Title,
                    Employer = employer,
                    Location = location,
                    // Adzuna returns 0 rather than omitting the field when a bound is unknown.
                    SalaryMin = job.SalaryMin is > 0 ? job.SalaryMin : null,
                    SalaryMax = job.SalaryMax is > 0 ? job.SalaryMax : null,
                    Description = job.Description,
                    Url = job.RedirectUrl,
                    PostedDate = job.Created,
                    DedupeKey = VacancyNormalizer.BuildDedupeKey(job.Title, employer, location),
                });
            }

            if (response.Results.Count < _options.ResultsPerPage)
            {
                break;
            }
        }

        logger.LogInformation("Adzuna returned {Count} listings", results.Count);
        return results;
    }

    private sealed class AdzunaResponse
    {
        [JsonPropertyName("results")]
        public List<AdzunaJob> Results { get; set; } = [];
    }

    private sealed class AdzunaJob
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company")]
        public AdzunaCompany? Company { get; set; }

        [JsonPropertyName("location")]
        public AdzunaLocation? Location { get; set; }

        [JsonPropertyName("salary_min")]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salary_max")]
        public decimal? SalaryMax { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("redirect_url")]
        public string? RedirectUrl { get; set; }

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }
    }

    private sealed class AdzunaCompany
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class AdzunaLocation
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
