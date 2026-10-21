using System.Net.Http.Json;
using HealthcareRecruitment.Core.Models;

namespace HealthcareRecruitment.Client.Services;

/// <summary>Server-backed saved jobs — only usable when signed in (AuthService attaches the bearer token).</summary>
public class SavedJobsService(HttpClient http)
{
    public async Task<List<SavedJobResponse>> GetAllAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<SavedJobResponse>>("api/savedjobs") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<HashSet<int>> GetSavedIdsAsync() =>
        (await GetAllAsync()).Select(s => s.VacancyId).ToHashSet();

    public async Task<bool> SaveAsync(int vacancyId)
    {
        var response = await http.PostAsync($"api/savedjobs/{vacancyId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveAsync(int vacancyId)
    {
        var response = await http.DeleteAsync($"api/savedjobs/{vacancyId}");
        return response.IsSuccessStatusCode;
    }
}
