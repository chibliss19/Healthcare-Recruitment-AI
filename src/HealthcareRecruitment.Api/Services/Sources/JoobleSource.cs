using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthcareRecruitment.Core.Models;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Sources;

public class JoobleOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Location { get; set; } = "Belfast";
    
    public List<string> KeywordList { get; set; } = 
    [
        "healthcare", 
        "nursing", 
        "nurse", 
        "care assistant", 
        "support worker", 
        "residential carer"
    ];
    
    public int PageSize { get; set; } = 50; 
    public int MaxPages { get; set; } = 5; 
}

public class JoobleSource(HttpClient http, IOptions<JoobleOptions> options, ILogger<JoobleSource> logger) : IVacancySource
{
    private readonly JoobleOptions _options = options.Value;

    public string Name => "Jooble";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<Vacancy>> FetchAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(">>> Jooble FetchAsync CALLED. IsConfigured: {IsConfigured}, Key: {Key}", 
            IsConfigured, 
            string.IsNullOrWhiteSpace(_options.ApiKey) ? "EMPTY" : "PRESENT");

        var results = new List<Vacancy>();

        if (!IsConfigured)
        {
            logger.LogWarning("Jooble API Key is missing in appsettings.json.");
            return results;
        }

        // Jooble requires the API key directly in the URL path.
        var url = $"https://uk.jooble.org/api?key={_options.ApiKey}";

        foreach (var keyword in _options.KeywordList)
        {
            logger.LogInformation("Fetching Jooble jobs for keyword: {Keyword}", keyword);

            for (var page = 1; page <= _options.MaxPages; page++)
            {
                var requestBody = new
                {
                    keywords = keyword,
                    location = _options.Location,
                    radius = "80",
                    page = page.ToString()
                };

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    using var httpResponse = await http.SendAsync(request, cancellationToken);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorDetails = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                        logger.LogWarning("Jooble API request failed for '{Keyword}' page {Page}: {StatusCode}. Details: {Details}", 
                            keyword, page, httpResponse.StatusCode, errorDetails);
                        break;
                    }

                    var responseString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    
                    var response = JsonSerializer.Deserialize<JoobleResponse>(responseString, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    // ✅ FIXED: Correct block scoping for empty checks and diagnostics
                    if (response?.Jobs == null || response.Jobs.Count == 0)
                    {
                        logger.LogInformation("Jooble returned 0 jobs for keyword '{Keyword}' on page {Page}.", keyword, page);
                        break; // No more listings for this keyword
                    }

                    foreach (var job in response.Jobs)
                    {
                        if (string.IsNullOrWhiteSpace(job.Title) || string.IsNullOrWhiteSpace(job.Link)) 
                            continue;

                        var employerName = string.IsNullOrWhiteSpace(job.Company) ? "Unknown employer" : job.Company;
                        var location = string.IsNullOrWhiteSpace(job.Location) ? _options.Location : job.Location;

                        DateTime? postedDate = null;
                        if (!string.IsNullOrWhiteSpace(job.Updated) && DateTime.TryParse(job.Updated, out var parsedDate))
                        {
                            postedDate = parsedDate;
                        }

                        results.Add(new Vacancy
                        {
                            ExternalId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job.Link)))[..32],
                            Source = Name,
                            Title = job.Title,
                            Employer = employerName,
                            Location = location,
                            Description = string.IsNullOrWhiteSpace(job.Snippet) ? "No description provided." : job.Snippet,
                            Url = job.Link,
                            PostedDate = postedDate,
                            DedupeKey = VacancyNormalizer.BuildDedupeKey(job.Title, employerName, location),
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An exception occurred while fetching Jooble jobs for keyword '{Keyword}'", keyword);
                    break;
                }
            }
        }

        var uniqueResults = results.DistinctBy(v => v.DedupeKey).ToList();
        logger.LogInformation("Jooble aggregation complete. Found {Count} unique listings.", uniqueResults.Count);
        return uniqueResults;
    }

    private class JoobleResponse
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("jobs")]
        public List<JoobleJob> Jobs { get; set; } = [];
    }

    private class JoobleJob
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }

        [JsonPropertyName("salary")]
        public string? Salary { get; set; }

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("updated")]
        public string? Updated { get; set; }
    }
}