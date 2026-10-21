using System.Text.Json;
using HealthcareRecruitment.Core.Models;
using Microsoft.JSInterop;

namespace HealthcareRecruitment.Client.Services;

/// <summary>
/// Client-side bookmarking backed by browser Local Storage — no server-side
/// accounts or user data. Whole vacancies are stored so the bookmarks page
/// still works if a listing later drops out of the aggregated feed.
/// </summary>
public class BookmarkService(IJSRuntime js)
{
    private const string StorageKey = "healthcare-jobs.bookmarks";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public event Action? Changed;

    public async Task<List<Vacancy>> GetAllAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Vacancy>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<HashSet<int>> GetBookmarkedIdsAsync() =>
        (await GetAllAsync()).Select(v => v.Id).ToHashSet();

    /// <summary>Adds the vacancy if missing, removes it if present; returns true when now bookmarked.</summary>
    public async Task<bool> ToggleAsync(Vacancy vacancy)
    {
        var bookmarks = await GetAllAsync();
        var existing = bookmarks.FirstOrDefault(v => v.Id == vacancy.Id);
        bool nowBookmarked;
        if (existing is null)
        {
            bookmarks.Add(vacancy);
            nowBookmarked = true;
        }
        else
        {
            bookmarks.Remove(existing);
            nowBookmarked = false;
        }

        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(bookmarks, JsonOptions));
        Changed?.Invoke();
        return nowBookmarked;
    }
}
