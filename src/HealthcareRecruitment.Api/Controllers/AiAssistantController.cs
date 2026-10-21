using System.Text.Json;
using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Services.Ai;
using HealthcareRecruitment.Api.Services.Auth;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthcareRecruitment.Api.Controllers;

/// <summary>
/// The resume-driven AI Career Assistant. Everything here requires a signed-in account.
/// A "session" is one complete pass — resume, recommendations, interview prep, and chat — bundled
/// together; uploading a resume always starts a brand new one, nothing carries over automatically.
/// </summary>
[Authorize]
[ApiController]
[Route("api/ai-assistant")]
public class AiAssistantController(
    AppDbContext db, 
    AiAssistantService assistant, 
    AiJsonRunner aiJsonRunner, 
    ILogger<AiAssistantController> logger) : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024; // 5 MB limit for document uploads

    [HttpPost("sessions")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<AiSessionDetail>> UploadAndCreateSession(
        IFormFile file,
        [FromForm] string? personalBio,
        [FromForm] string? skills,
        [FromForm] string? certifications,
        [FromForm] int yearsOfExperience,
        [FromForm] decimal minHourlySalary,
        [FromForm] List<string>? preferredLocations,
        [FromForm] string? fullName,
        [FromForm] string? gender,
        [FromForm] string? maritalStatus,
        CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest("Please choose a valid file.");

        if (!assistant.HasConfiguredProvider)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "No AI provider is configured. Set OpenRouter:ApiKey in the API settings.");
        }

        string resumeText;
        try
        {
            await using var stream = file.OpenReadStream();
            resumeText = ResumeTextExtractor.Extract(stream, file.FileName);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }

        var userId = User.GetUserId();

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.ResumeText = resumeText;
        user.ResumeUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            return await assistant.CreateCustomizedSessionAsync(
                userId, 
                resumeText, 
                personalBio ?? string.Empty, 
                skills ?? string.Empty, 
                certifications ?? string.Empty, 
                yearsOfExperience, 
                minHourlySalary, 
                preferredLocations ?? [], 
                fullName ?? string.Empty, 
                gender ?? string.Empty, 
                maritalStatus ?? string.Empty, 
                ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Customized session creation failed for user {UserId}", userId);
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<AiSessionSummary>>> ListSessions(CancellationToken ct)
    {
        return await assistant.ListSessionsAsync(User.GetUserId(), ct);
    }

    [HttpGet("sessions/{sessionId:int}")]
    public async Task<ActionResult<AiSessionDetail>> GetSession(int sessionId, CancellationToken ct)
    {
        try { return await assistant.GetSessionAsync(User.GetUserId(), sessionId, ct); }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [HttpPut("sessions/{sessionId:int}/toggle-pin")]
    public async Task<IActionResult> TogglePin(int sessionId, CancellationToken ct)
    {
        try
        {
            await assistant.ToggleSessionPinAsync(User.GetUserId(), sessionId, ct);
            return Ok();
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [HttpDelete("sessions/{sessionId:int}")]
    public async Task<IActionResult> DeleteSession(int sessionId, CancellationToken ct)
    {
        try
        {
            await assistant.DeleteSessionAsync(User.GetUserId(), sessionId, ct);
            return Ok();
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [HttpPost("sessions/{sessionId:int}/interview-prep/{vacancyId:int}")]
    public async Task<ActionResult<InterviewPrepResponse>> GetOrGenerateInterviewPrep(int sessionId, int vacancyId, CancellationToken ct)
    {
        if (!assistant.HasConfiguredProvider)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No AI provider is configured.");
        }

        try
        {
            return await assistant.GetOrGenerateInterviewPrepAsync(User.GetUserId(), sessionId, vacancyId, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Interview prep generation failed for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    [HttpPost("sessions/{sessionId:int}/messages")]
    public async Task<ActionResult<InterviewChatResponse>> SendMessage(int sessionId, SendSessionMessageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Message payload is required.");

        if (!assistant.HasConfiguredProvider)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No AI provider is configured.");
        }

        try
        {
            return await assistant.SendSessionMessageAsync(User.GetUserId(), sessionId, request.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Session chat failed for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    // ==============================================================================
    // TAILORED SUPPORTING STATEMENT GENERATOR
    // ==============================================================================

    [HttpPost("generate-statement")]
    public async Task<IActionResult> GenerateStatement([FromBody] StatementRequestDto request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TargetVacancyId))
        {
            return BadRequest("Target vacancy ID is required.");
        }

        var userId = User.GetUserId();

        if (!assistant.HasConfiguredProvider)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No AI provider is configured.");
        }

        try
        {
            var result = await assistant.GenerateStatementAsync(userId, request.TargetVacancyId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate AI supporting statement for user {UserId} and vacancy {VacancyId}", userId, request.TargetVacancyId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate the supporting statement. Please try again.");
        }
    }

    // Re-evaluate session matches endpoint
    [HttpPost("sessions/{sessionId:int}/re-evaluate")]
    public async Task<ActionResult<ProfileAnalysisResult>> ReEvaluateSession(int sessionId, CancellationToken ct)
    {
        try
        {
            int userId = User.GetUserId();
            var result = await assistant.ReEvaluateSessionAsync(userId, sessionId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}