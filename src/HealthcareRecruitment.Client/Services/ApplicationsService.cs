using System.Net.Http.Json;
using HealthcareRecruitment.Core.Models;

namespace HealthcareRecruitment.Client.Services;

/// <summary>Server-backed application draft/submit flow — only usable when signed in.</summary>
public class ApplicationsService(HttpClient http)
{
    public async Task<List<ApplicationResponse>> GetAllAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<ApplicationResponse>>("api/applications") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<ApplicationResponse?> GetByVacancyAsync(int vacancyId)
    {
        var response = await http.GetAsync($"api/applications/{vacancyId}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ApplicationResponse>()
            : null;
    }

    public async Task<(bool Success, string? Error, ApplicationResponse? Result)> SaveDraftAsync(int vacancyId, string? coverLetter, string? phone)
    {
        var response = await http.PutAsJsonAsync($"api/applications/{vacancyId}", new ApplicationUpsertRequest
        {
            CoverLetter = coverLetter,
            Phone = phone,
        });

        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        return (true, null, await response.Content.ReadFromJsonAsync<ApplicationResponse>());
    }

    public async Task<(bool Success, string? Error, ApplicationResponse? Result)> SubmitAsync(int vacancyId)
    {
        var response = await http.PostAsync($"api/applications/{vacancyId}/submit", null);
        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        return (true, null, await response.Content.ReadFromJsonAsync<ApplicationResponse>());
    }
}
