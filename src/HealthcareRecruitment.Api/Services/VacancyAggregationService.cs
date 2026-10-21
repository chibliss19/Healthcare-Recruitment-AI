using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Services.Sources;
using HealthcareRecruitment.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services;

public class AggregationOptions
{
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>Listings not seen from their source for this many days are removed.</summary>
    public int StaleAfterDays { get; set; } = 14;
}

/// <summary>
/// Polls all configured vacancy sources on a schedule, deduplicates listings
/// (per-source by external id, cross-source by title+employer+location key)
/// and upserts them into SQL Server. A source failing — or being down — never
/// interrupts the loop: previously stored vacancies keep being served.
/// </summary>
public class VacancyAggregationService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IVacancySource> sources,
    IOptions<AggregationOptions> options,
    ILogger<VacancyAggregationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Real sources take precedence: the sample source only runs when nothing else is configured.
        var realSources = sources.Where(s => s is not SampleDataSource && s.IsConfigured).ToList();
        var activeSources = realSources.Count > 0
            ? realSources
            : sources.Where(s => s is SampleDataSource && s.IsConfigured).ToList();

        if (activeSources.Count == 0)
        {
            logger.LogWarning("No vacancy sources are configured; aggregation service is idle");
            return;
        }

        logger.LogInformation("Aggregating from: {Sources}", string.Join(", ", activeSources.Select(s => s.Name)));

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.PollIntervalMinutes));
        do
        {
            foreach (var source in activeSources)
            {
                try
                {
                    var listings = await source.FetchAsync(stoppingToken);
                    await UpsertAsync(source.Name, listings, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // HTTP retries/backoff happen inside the resilient HttpClient; if the
                    // source is still down after that, skip it until the next cycle.
                    logger.LogError(ex, "Fetching from {Source} failed; serving cached vacancies", source.Name);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task UpsertAsync(string sourceName, IReadOnlyList<Vacancy> listings, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Within-batch dedupe: keep the first listing per external id and per composite key.
        var batch = listings
            .GroupBy(v => v.ExternalId)
            .Select(g => g.First())
            .GroupBy(v => v.DedupeKey)
            .Select(g => g.First())
            .ToList();

        var existingForSource = await db.Vacancies
            .Where(v => v.Source == sourceName)
            .ToDictionaryAsync(v => v.ExternalId, ct);

        // Cross-source dedupe: a composite key already stored by another source wins.
        var otherSourceKeys = (await db.Vacancies
                .Where(v => v.Source != sourceName)
                .Select(v => v.DedupeKey)
                .ToListAsync(ct))
            .ToHashSet();

        var added = 0;
        var updated = 0;
        foreach (var listing in batch)
        {
            if (existingForSource.TryGetValue(listing.ExternalId, out var existing))
            {
                existing.Title = listing.Title;
                existing.Employer = listing.Employer;
                existing.Location = listing.Location;
                existing.SalaryMin = listing.SalaryMin;
                existing.SalaryMax = listing.SalaryMax;
                existing.Description = listing.Description;
                existing.Url = listing.Url;
                existing.PostedDate = listing.PostedDate;
                existing.DedupeKey = listing.DedupeKey;
                existing.FetchedAt = now;
                updated++;
            }
            else if (!otherSourceKeys.Contains(listing.DedupeKey))
            {
                listing.FetchedAt = now;
                db.Vacancies.Add(listing);
                added++;
            }
        }

        // Drop listings the source stopped returning a while ago (likely filled/expired).
        var staleBefore = now.AddDays(-options.Value.StaleAfterDays);
        var stale = await db.Vacancies
            .Where(v => v.Source == sourceName && v.FetchedAt < staleBefore)
            .ToListAsync(ct);
        db.Vacancies.RemoveRange(stale);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "{Source}: {Added} added, {Updated} updated, {Removed} stale removed",
            sourceName, added, updated, stale.Count);
    }
}
