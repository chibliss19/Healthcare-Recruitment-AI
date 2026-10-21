using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthcareRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacanciesController(AppDbContext db) : ControllerBase
{
    /// <summary>Search and filter vacancies with pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<VacancySearchResponse>> Search(
        [FromQuery] string? search,
        [FromQuery] string? location,
        [FromQuery] string? source,
        [FromQuery] decimal? minSalary,
        [FromQuery] decimal? maxSalary,
        [FromQuery] string? sort = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Vacancies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                v.Title.Contains(term) ||
                v.Employer.Contains(term) ||
                (v.Description != null && v.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(v => v.Location.Contains(location.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(v => v.Source == source);
        }

        if (minSalary is not null)
        {
            query = query.Where(v => v.SalaryMax >= minSalary || (v.SalaryMax == null && v.SalaryMin >= minSalary));
        }

        if (maxSalary is not null)
        {
            query = query.Where(v => v.SalaryMin <= maxSalary);
        }

        query = sort switch
        {
            "salary" => query.OrderByDescending(v => v.SalaryMax ?? v.SalaryMin ?? 0),
            "title" => query.OrderBy(v => v.Title),
            _ => query.OrderByDescending(v => v.PostedDate ?? DateTime.MinValue),
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new VacancySearchResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>Distinct locations/sources for the dashboard filter dropdowns.</summary>
    [HttpGet("filters")]
    public async Task<ActionResult<VacancyFilterOptions>> Filters(CancellationToken ct)
    {
        return new VacancyFilterOptions
        {
            Locations = await db.Vacancies.Select(v => v.Location).Distinct().OrderBy(l => l).ToListAsync(ct),
            Sources = await db.Vacancies.Select(v => v.Source).Distinct().OrderBy(s => s).ToListAsync(ct),
        };
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Vacancy>> GetById(int id, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        return vacancy is null ? NotFound() : vacancy;
    }
}
