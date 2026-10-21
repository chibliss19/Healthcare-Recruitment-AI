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
public class ApplicationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ApplicationResponse>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Vacancy)
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => ToResponse(a))
            .ToListAsync(ct);
    }

    [HttpGet("{vacancyId:int}")]
    public async Task<ActionResult<ApplicationResponse>> GetByVacancy(int vacancyId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var application = await db.Applications.AsNoTracking()
            .Include(a => a.Vacancy)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.VacancyId == vacancyId, ct);

        return application is null ? NotFound() : ToResponse(application);
    }

    /// <summary>Create or update the draft for a vacancy. A submitted application can no longer be edited.</summary>
    [HttpPut("{vacancyId:int}")]
    public async Task<ActionResult<ApplicationResponse>> SaveDraft(int vacancyId, ApplicationUpsertRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vacancyId, ct);
        if (vacancy is null)
        {
            return NotFound("Vacancy not found.");
        }

        var application = await db.Applications.FirstOrDefaultAsync(a => a.UserId == userId && a.VacancyId == vacancyId, ct);
        var now = DateTime.UtcNow;

        if (application is null)
        {
            application = new JobApplication
            {
                UserId = userId,
                VacancyId = vacancyId,
                Status = ApplicationStatus.Draft,
                CreatedAt = now,
            };
            db.Applications.Add(application);
        }
        else if (application.Status == ApplicationStatus.Submitted)
        {
            return Conflict("This application has already been submitted and can no longer be edited.");
        }

        application.CoverLetter = request.CoverLetter;
        application.Phone = request.Phone;
        application.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        application.Vacancy = vacancy;
        return ToResponse(application);
    }

    [HttpPost("{vacancyId:int}/submit")]
    public async Task<ActionResult<ApplicationResponse>> Submit(int vacancyId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var application = await db.Applications.Include(a => a.Vacancy)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.VacancyId == vacancyId, ct);

        if (application is null)
        {
            return NotFound("Save a draft before submitting.");
        }

        if (application.Status == ApplicationStatus.Submitted)
        {
            return Conflict("Already submitted.");
        }

        var now = DateTime.UtcNow;
        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAt = now;
        application.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return ToResponse(application);
    }

    private static ApplicationResponse ToResponse(JobApplication a) => new()
    {
        VacancyId = a.VacancyId,
        Vacancy = a.Vacancy!,
        Status = a.Status,
        CoverLetter = a.CoverLetter,
        Phone = a.Phone,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        SubmittedAt = a.SubmittedAt,
    };
}
