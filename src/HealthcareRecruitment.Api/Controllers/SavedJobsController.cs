using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Data.Entities;
using HealthcareRecruitment.Api.Services.Auth;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthcareRecruitment.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SavedJobsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SavedJobResponse>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return await db.SavedJobs.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Vacancy)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => new SavedJobResponse { VacancyId = s.VacancyId, Vacancy = s.Vacancy!, SavedAt = s.SavedAt })
            .ToListAsync(ct);
    }

    [HttpPost("{vacancyId:int}")]
    public async Task<ActionResult<SavedJobResponse>> Save(int vacancyId, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vacancyId, ct);
        if (vacancy is null)
        {
            return NotFound("Vacancy not found.");
        }

        var existing = await db.SavedJobs.FirstOrDefaultAsync(s => s.UserId == userId && s.VacancyId == vacancyId, ct);
        if (existing is not null)
        {
            return new SavedJobResponse { VacancyId = vacancyId, Vacancy = vacancy, SavedAt = existing.SavedAt };
        }

        var savedJob = new SavedJob { UserId = userId, VacancyId = vacancyId, SavedAt = DateTime.UtcNow };
        db.SavedJobs.Add(savedJob);
        await db.SaveChangesAsync(ct);

        return new SavedJobResponse { VacancyId = vacancyId, Vacancy = vacancy, SavedAt = savedJob.SavedAt };
    }

    [HttpDelete("{vacancyId:int}")]
    public async Task<IActionResult> Remove(int vacancyId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var existing = await db.SavedJobs.FirstOrDefaultAsync(s => s.UserId == userId && s.VacancyId == vacancyId, ct);
        if (existing is null)
        {
            return NoContent();
        }

        db.SavedJobs.Remove(existing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
