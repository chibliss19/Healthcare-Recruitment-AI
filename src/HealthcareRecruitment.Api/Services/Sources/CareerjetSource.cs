using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthcareRecruitment.Core.Models;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Sources;

public class CareerjetOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string AffId { get; set; } = string.Empty;
    public string Locale { get; set; } = "en_GB";
    
    public List<string> KeywordList { get; set; } = 
    [
        "healthcare", 
        "nursing", 
        "nurse", 
        "care assistant", 
        "support worker", 
        "residential carer"
    ];
    
    public string Location { get; set; } = "Northern Ireland";
    public int PageSize { get; set; } = 50;
    public int MaxPages { get; set; } = 5; 
}

public class CareerjetSource(HttpClient http, IOptions<CareerjetOptions> options, ILogger<CareerjetSource> logger) : IVacancySource
{
    private readonly CareerjetOptions _options = options.Value;

    public string Name => "Careerjet";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<Vacancy>> FetchAsync(CancellationToken cancellationToken)
    {
        var results = new List<Vacancy>();

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("Careerjet ApiKey is missing in appsettings.json.");
            return results;
        }

        // v4 API requires Basic Auth where username is the API key and password is blank
        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:"));
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 HealthcareRecruitmentApp/1.0";
        
        // This MUST be the IP address you whitelisted in the Careerjet Dashboard!
        var publicIp = "81.111.106.35"; 

        foreach (var keyword in _options.KeywordList)
        {
            logger.LogInformation("Fetching Careerjet jobs for keyword: {Keyword}", keyword);

            for (var page = 1; page <= _options.MaxPages; page++)
            {
                // Using the mandatory v4 endpoint
                var url = "https://search.api.careerjet.net/v4/query" +
                          $"?locale_code={Uri.EscapeDataString(_options.Locale)}" +
                          $"&keywords={Uri.EscapeDataString(keyword)}" +
                          $"&location={Uri.EscapeDataString(_options.Location)}" +
                          $"&page_size={_options.PageSize}" +
                          $"&page={page}" +
                          $"&user_ip={publicIp}" + 
                          $"&user_agent={Uri.EscapeDataString(userAgent)}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.Referrer = new Uri("https://www.ulster.ac.uk");

                using var httpResponse = await http.SendAsync(request, cancellationToken);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorDetails = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning("Careerjet API request failed for '{Keyword}' page {Page}: {StatusCode}. Details: {Details}", 
                        keyword, page, httpResponse.StatusCode, errorDetails);
                    break; 
                }

                var bytes = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                var response = JsonSerializer.Deserialize<CareerjetResponse>(bytes);
                
                if (response?.Jobs is not { Count: > 0 })
                {
                    break;
                }

                foreach (var job in response.Jobs)
                {
                    if (string.IsNullOrWhiteSpace(job.Title) || string.IsNullOrWhiteSpace(job.Url)) continue;

                    var employerName = string.IsNullOrWhiteSpace(job.Company) ? "Unknown employer" : job.Company;
                    var location = string.IsNullOrWhiteSpace(job.Locations) ? _options.Location : job.Locations;
                    
                    DateTime? postedDate = null;
                    if (!string.IsNullOrWhiteSpace(job.Date) && DateTime.TryParse(job.Date, out var parsedDate))
                    {
                        postedDate = parsedDate;
                    }

                    results.Add(new Vacancy
                    {
                        ExternalId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job.Url)))[..32],
                        Source = Name, 
                        Title = job.Title,
                        Employer = employerName, 
                        Location = location,
                        SalaryMin = job.SalaryMin,
                        SalaryMax = job.SalaryMax,
                        Description = job.Description,
                        Url = job.Url,
                        PostedDate = postedDate,
                        DedupeKey = VacancyNormalizer.BuildDedupeKey(job.Title, employerName, location),
                    });
                }

                if (page >= response.Pages) break;
            }
        }

        var uniqueResults = results.DistinctBy(v => v.DedupeKey).ToList();
        logger.LogInformation("Careerjet aggregation complete. Found {Count} unique listings.", uniqueResults.Count);
        return uniqueResults;
    }

    private sealed class CareerjetResponse
    {
        [JsonPropertyName("jobs")]
        public List<CareerjetJob> Jobs { get; set; } = [];

        [JsonPropertyName("pages")]
        public int Pages { get; set; }
    }

    private sealed class CareerjetJob
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("locations")]
        public string? Locations { get; set; }

        [JsonPropertyName("salary_min")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salary_max")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? SalaryMax { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }
}