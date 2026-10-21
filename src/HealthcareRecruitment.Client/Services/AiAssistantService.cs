using System.Net.Http.Json;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace HealthcareRecruitment.Client.Services;

/// <summary>Client for the resume-driven AI Career Assistant. A "session" bundles the resume
/// summary, recommendations, interview prep, and chat together — uploading always starts a new one.</summary>
public class AiAssistantService(HttpClient http)
{
    private const long MaxUploadBytes = 5 * 1024 * 1024; // 5 MB, matches the API's RequestSizeLimit

    public async Task<(bool Success, string? Error, AiSessionDetail? Result)> UploadResumeAndCreateSessionAsync(
        IBrowserFile file,
        string personalBio,
        string skills,
        string certifications,
        int yearsOfExperience,
        decimal minHourlySalary,
        List<string> preferredLocations,
        string fullName,
        string gender,
        string maritalStatus)
    {
        using var content = new MultipartFormDataContent();
        
        // 1. Add file content
        using var stream = file.OpenReadStream(MaxUploadBytes);
        using var fileContent = new StreamContent(stream);
        content.Add(fileContent, "file", file.Name);

        // 2. Add custom candidate personalization & demographic fields
        content.Add(new StringContent(fullName ?? string.Empty), "fullName");
        content.Add(new StringContent(gender ?? string.Empty), "gender");
        content.Add(new StringContent(maritalStatus ?? string.Empty), "maritalStatus");
        content.Add(new StringContent(personalBio ?? string.Empty), "personalBio");
        content.Add(new StringContent(skills ?? string.Empty), "skills");
        content.Add(new StringContent(certifications ?? string.Empty), "certifications");
        content.Add(new StringContent(yearsOfExperience.ToString()), "yearsOfExperience");
        content.Add(new StringContent(minHourlySalary.ToString()), "minHourlySalary");

        // 3. Add individual location entries for multi-filtering binding
        if (preferredLocations != null)
        {
            foreach (var location in preferredLocations)
            {
                content.Add(new StringContent(location), "preferredLocations");
            }
        }

        var response = await http.PostAsync("api/ai-assistant/sessions", content);
        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        return (true, null, await response.Content.ReadFromJsonAsync<AiSessionDetail>());
    }

    public async Task<List<AiSessionSummary>> ListSessionsAsync()
    {
        var response = await http.GetAsync("api/ai-assistant/sessions");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AiSessionSummary>>() ?? []
            : [];
    }

    public async Task<AiSessionDetail?> GetSessionAsync(int sessionId)
    {
        var response = await http.GetAsync($"api/ai-assistant/sessions/{sessionId}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AiSessionDetail>()
            : null;
    }

    public async Task ToggleSessionPinAsync(int sessionId)
    {
        var response = await http.PutAsync($"api/ai-assistant/sessions/{sessionId}/toggle-pin", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSessionAsync(int sessionId)
    {
        var response = await http.DeleteAsync($"api/ai-assistant/sessions/{sessionId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<(bool Success, string? Error, InterviewPrepResponse? Result)> GetInterviewPrepAsync(int sessionId, int vacancyId)
    {
        var response = await http.PostAsync($"api/ai-assistant/sessions/{sessionId}/interview-prep/{vacancyId}", null);
        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        return (true, null, await response.Content.ReadFromJsonAsync<InterviewPrepResponse>());
    }

    public async Task<(bool Success, string? Error, string? Reply)> SendMessageAsync(int sessionId, string message)
    {
        var response = await http.PostAsJsonAsync($"api/ai-assistant/sessions/{sessionId}/messages",
            new SendSessionMessageRequest { Message = message });

        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        var result = await response.Content.ReadFromJsonAsync<InterviewChatResponse>();
        return (true, null, result?.Reply);
    }

    // ======================================================================
    // Generate Supporting Statement / Cover Letter
    // ======================================================================
    public async Task<(bool Success, string? Error, string? Statement)> GenerateStatementAsync(int vacancyId)
    {
        var request = new StatementRequestDto { TargetVacancyId = vacancyId.ToString() };
        var response = await http.PostAsJsonAsync("api/ai-assistant/generate-statement", request);

        if (!response.IsSuccessStatusCode)
        {
            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        var result = await response.Content.ReadFromJsonAsync<StatementResponseDto>();
        return (true, null, result?.Statement);
    }

    // Re-evaluate matches for a session
    public async Task<(bool success, string? error, ProfileAnalysisResult? result)> ReEvaluateSessionAsync(int sessionId)
    {
        try
        {
            var response = await http.PostAsync($"api/ai-assistant/sessions/{sessionId}/re-evaluate", null);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ProfileAnalysisResult>();
                return (true, null, data);
            }
            return (false, "Failed to re-evaluate matches.", null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }
}