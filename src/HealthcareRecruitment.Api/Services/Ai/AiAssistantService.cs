using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;
using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Data.Entities;
using HealthcareRecruitment.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthcareRecruitment.Api.Services.Ai;

/// <summary>
/// The resume-driven AI Career Assistant. A "session" is one frozen pass through the whole
/// assistant — resume summary, recommendations, every interview-prep set generated, and the
/// chat about them, all bundled together. Nothing carries over between sessions; a new upload
/// always starts a brand new one, and History restores a past session exactly as it was rather
/// than regenerating it.
/// </summary>
public class AiAssistantService(AppDbContext db, AiJsonRunner runner, OllamaEmbeddingService embeddingService)
{
    private const int MaxVacanciesInPrompt = 300;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool HasConfiguredProvider => runner.HasConfiguredProvider;

    public async Task<AiSessionDetail> CreateCustomizedSessionAsync(
        int userId, 
        string resumeText, 
        string personalBio,
        string skills,
        string certifications,
        int yearsOfExperience,
        decimal minHourlySalary,
        List<string> preferredLocations,
        string fullName,
        string gender,
        string maritalStatus,
        CancellationToken ct)
    {
        // Embed personal info context cleanly into the evaluation pipelines
        var enrichedBio = $"Full Name: {fullName} | Gender: {gender} | Marital Status: {maritalStatus} | {personalBio}";

        var summary = await SummarizeResumeAsync(resumeText, enrichedBio, skills, certifications, yearsOfExperience, ct);
        var recommendations = await RecommendVacanciesCustomizedAsync(resumeText, enrichedBio, skills, certifications, yearsOfExperience, minHourlySalary, preferredLocations, ct);

        var now = DateTime.UtcNow;
        var session = new AiSession
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            ResumeSnapshot = resumeText,
            ResumeSummaryJson = JsonSerializer.Serialize(summary, JsonOptions),
            RecommendationsJson = SerializeRecommendationsForStorage(recommendations),
            IsPinned = false,
        };
        db.AiSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return new AiSessionDetail
        {
            Id = session.Id,
            CreatedAt = session.CreatedAt,
            ResumeSummary = summary,
            Recommendations = recommendations,
            InterviewPreps = [],
            ChatTurns = [],
        };
    }

    public async Task<List<AiSessionSummary>> ListSessionsAsync(int userId, CancellationToken ct)
    {
        var sessions = await db.AiSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsPinned)
            .ThenByDescending(s => s.UpdatedAt)
            .Select(s => new { s.Id, s.CreatedAt, s.UpdatedAt, s.ResumeSummaryJson, s.IsPinned })
            .ToListAsync(ct);

        return sessions.Select(s => new AiSessionSummary
        {
            Id = s.Id,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            IsPinned = s.IsPinned,
            Preview = ExtractPreview(s.ResumeSummaryJson),
        }).ToList();
    }

    public async Task ToggleSessionPinAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.AiSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        session.IsPinned = !session.IsPinned;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteSessionAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.AiSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        db.AiSessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AiSessionDetail> GetSessionAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.AiSessions.AsNoTracking()
            .Include(s => s.InterviewPreps)
            .Include(s => s.ChatTurns)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        var resumeSummary = JsonSerializer.Deserialize<ResumeSummaryResponse>(session.ResumeSummaryJson, JsonOptions) ?? new();
        var recommendations = JsonSerializer.Deserialize<ProfileAnalysisResult>(session.RecommendationsJson, JsonOptions) ?? new();

        var vacancyIds = recommendations.Matches.Select(m => m.VacancyId)
            .Concat(session.InterviewPreps.Select(p => p.VacancyId))
            .Distinct()
            .ToList();
        var vacancies = await db.Vacancies.AsNoTracking()
            .Where(v => vacancyIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        foreach (var match in recommendations.Matches)
        {
            vacancies.TryGetValue(match.VacancyId, out var vacancy);
            match.Vacancy = vacancy;
        }

        var preps = session.InterviewPreps.Select(p => new InterviewPrepResponse
        {
            VacancyId = p.VacancyId,
            Questions = JsonSerializer.Deserialize<List<InterviewQuestion>>(p.QuestionsJson, JsonOptions) ?? [],
        }).ToList();

        var chatTurns = session.ChatTurns.OrderBy(t => t.CreatedAt)
            .Select(t => new InterviewChatMessage
            {
                Role = Enum.TryParse<ChatRole>(t.Role, true, out var role) ? role : ChatRole.User,
                Content = t.Content,
            })
            .ToList();

        return new AiSessionDetail
        {
            Id = session.Id,
            CreatedAt = session.CreatedAt,
            ResumeSummary = resumeSummary,
            Recommendations = recommendations,
            InterviewPreps = preps,
            ChatTurns = chatTurns,
        };
    }

    public async Task<InterviewPrepResponse> GetOrGenerateInterviewPrepAsync(int userId, int sessionId, int vacancyId, CancellationToken ct)
    {
        var session = await db.AiSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        var existing = await db.AiSessionInterviewPreps.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.VacancyId == vacancyId, ct);
        if (existing is not null)
        {
            return new InterviewPrepResponse
            {
                VacancyId = vacancyId,
                Questions = JsonSerializer.Deserialize<List<InterviewQuestion>>(existing.QuestionsJson, JsonOptions) ?? [],
            };
        }

        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vacancyId, ct)
            ?? throw new InvalidOperationException("Vacancy not found.");

        var result = await GenerateInterviewPrepAsync(session.ResumeSnapshot, vacancy, ct);

        db.AiSessionInterviewPreps.Add(new AiSessionInterviewPrep
        {
            SessionId = sessionId,
            VacancyId = vacancyId,
            QuestionsJson = JsonSerializer.Serialize(result.Questions, JsonOptions),
            CreatedAt = DateTime.UtcNow,
        });
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return result;
    }

    public async Task<InterviewChatResponse> SendSessionMessageAsync(int userId, int sessionId, string message, CancellationToken ct)
    {
        var session = await db.AiSessions
            .Include(s => s.ChatTurns)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        var recommendations = JsonSerializer.Deserialize<ProfileAnalysisResult>(session.RecommendationsJson, JsonOptions) ?? new();
        var vacancyIds = recommendations.Matches.Select(m => m.VacancyId).ToList();
        var vacancies = await db.Vacancies.AsNoTracking()
            .Where(v => vacancyIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        const string systemPrompt = """
            You are a warm, supportive, and conversational AI career mentor helping a candidate with their Northern Ireland healthcare job search and interview prep. 
            Respond with ONLY a single JSON object — no markdown, no code fences, no free text — matching exactly this schema:
            { "reply": string }
            """;

        var sb = new StringBuilder();
        sb.AppendLine("CANDIDATE RESUME");
        sb.AppendLine(session.ResumeSnapshot);
        sb.AppendLine();
        sb.AppendLine("RECOMMENDED JOBS");
        foreach (var match in recommendations.Matches)
        {
            if (vacancies.TryGetValue(match.VacancyId, out var v))
            {
                sb.AppendLine($"id={v.Id} | {v.Title} | {v.Employer} | {v.Location} | match {match.MatchScore}% | {match.Reason}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("CONVERSATION SO FAR");
        foreach (var turn in session.ChatTurns.OrderBy(t => t.CreatedAt))
        {
            sb.AppendLine($"{turn.Role}: {turn.Content}");
        }
        sb.AppendLine();
        sb.AppendLine($"CANDIDATE'S NEW MESSAGE: {message}");

        var result = await runner.RunAsync(systemPrompt, sb.ToString(), json =>
        {
            var cleaned = AiJsonRunner.StripCodeFence(json);
            var parsed = JsonSerializer.Deserialize<InterviewChatResponse>(cleaned, JsonOptions)
                ?? throw new FormatException("AI returned an empty JSON document");

            if (string.IsNullOrWhiteSpace(parsed.Reply))
            {
                throw new FormatException("AI response is missing the reply field");
            }

            return parsed;
        }, ct);

        var now = DateTime.UtcNow;
        session.ChatTurns.Add(new AiSessionChatTurn { SessionId = sessionId, Role = "user", Content = message, CreatedAt = now });
        session.ChatTurns.Add(new AiSessionChatTurn { SessionId = sessionId, Role = "assistant", Content = result.Reply, CreatedAt = now });
        session.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return result;
    }

    public async Task<StatementResponseDto> GenerateStatementAsync(int userId, string targetVacancyId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || string.IsNullOrWhiteSpace(user.ResumeText))
        {
            throw new InvalidOperationException("Candidate resume data not found. Please upload a resume first.");
        }

        int.TryParse(targetVacancyId, out int jobId);
        var targetJob = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => 
            (jobId != 0 && v.Id == jobId) || v.ExternalId == targetVacancyId, ct);

        if (targetJob == null)
        {
            throw new InvalidOperationException($"The target vacancy '{targetVacancyId}' could not be found in the database.");
        }

        const string systemPrompt = """
            You are an expert healthcare career advisor writing a natural, humanized job application supporting statement.
            Respond with ONLY a single JSON object — no markdown, no code fences, no free text — matching exactly this schema:
            {
              "statement": string
            }
            """;

        var userPrompt = $"""
            CANDIDATE BACKGROUND DATA:
            {user.ResumeText}

            TARGET JOB:
            Title: {targetJob.Title}
            Employer: {targetJob.Employer}
            Description: {targetJob.Description}

            CRITICAL WRITING INSTRUCTIONS:
            - Write in the first person ("I").
            - Explicitly map the candidate's actual skills to the specific requirements in the TARGET JOB.
            - Do NOT invent experience or certifications the candidate does not have.
            - Maintain a grounded, genuine human tone or candidate writing style. Strictly AVOID AI cliches (e.g., "delve", "testament", "multifaceted", "synergy", "navigate").
            - Write in UK English throughout, matching how an applicant in Northern Ireland's health and social care (HSC) sector would actually write: British spelling ("organisation" not "organization", "recognised" not "recognized", "specialise" not "specialize", "programme" not "program", "CV" not "resume"), and natural UK phrasing. Do not use American spelling, vocabulary, or idiom anywhere in the statement.
            """;

        return await runner.RunAsync(systemPrompt, userPrompt, json =>
        {
            var cleaned = AiJsonRunner.StripCodeFence(json);
            var parsed = JsonSerializer.Deserialize<StatementResponseDto>(cleaned, JsonOptions)
                ?? throw new FormatException("AI returned an empty JSON document");

            if (string.IsNullOrWhiteSpace(parsed.Statement))
            {
                throw new FormatException("AI response is missing the statement field");
            }

            return parsed;
        }, ct);
    }
    private Task<ResumeSummaryResponse> SummarizeResumeAsync(string resumeText, string personalBio, string skills, string certifications, int yearsOfExperience, CancellationToken ct)
    {
        const string systemPrompt = """
            You are an expert healthcare careers advisor. You will receive a candidate's personal profile statement, stated skills, certifications, years of experience, and resume text.
            
            CRITICAL INSTRUCTIONS FOR THE 'summary' FIELD:
            - Write a highly dynamic, unique, and engaging professional summary.
            - DO NOT just copy or repeat the opening lines of the candidate's resume (e.g., strictly avoid defaulting to phrases like "Compassionate and reliable...").
            - Synthesize their actual experience, unique skills, and background into a fresh, objective 2-3 sentence overview.
            - Vary your sentence structure, tone, and vocabulary every single time to ensure no two summaries sound exactly alike you can use the candidate's actual writing style.

            Respond with ONLY a single JSON object — no markdown, no code fences, no free text — matching exactly this schema:
            {
              "summary": string,
              "skills": [],
              "highlights": []
            }
            """;

        var userPrompt = $"""
            PERSONAL BIO: {personalBio}
            STATED SKILLS: {skills}
            CERTIFICATIONS/QUALIFICATIONS: {certifications}
            YEARS OF EXPERIENCE: {yearsOfExperience}
            RESUME TEXT:
            {resumeText}
            """;

        return runner.RunAsync(systemPrompt, userPrompt, json =>
        {
            var cleaned = AiJsonRunner.StripCodeFence(json);
            var result = JsonSerializer.Deserialize<ResumeSummaryResponse>(cleaned, JsonOptions)
                ?? throw new FormatException("AI returned an empty JSON document");

            if (string.IsNullOrWhiteSpace(result.Summary))
            {
                throw new FormatException("AI response is missing the summary field");
            }

            return result;
        }, ct);
    }

    
    

    private async Task<ProfileAnalysisResult> RecommendVacanciesCustomizedAsync(
        string resumeText, 
        string personalBio,
        string skills,
        string certifications,
        int yearsOfExperience,
        decimal minHourlySalary,
        List<string> preferredLocations,
        CancellationToken ct)
    {
        var candidateEmbeddingText = $"Bio: {personalBio}\nSkills: {skills}\nCertifications: {certifications}\nExperience: {yearsOfExperience} years\nResume: {resumeText}";
        float[] userEmbedding = [];
        try
        {
            userEmbedding = await embeddingService.GetEmbeddingAsync(candidateEmbeddingText, ct);
        }
        catch { }

        var query = db.Vacancies.AsNoTracking();
        if (minHourlySalary > 0)
        {
            query = query.Where(v => (v.SalaryMax >= minHourlySalary) || (v.SalaryMin >= minHourlySalary));
        }

        var allVacancies = await query.ToListAsync(ct);

        if (preferredLocations != null && preferredLocations.Count > 0)
        {
            allVacancies = allVacancies
                .Where(v => v.Location != null && preferredLocations.Any(loc => 
                    v.Location.Contains(loc, StringComparison.OrdinalIgnoreCase) ||
                    (loc.Contains("Londonderry", StringComparison.OrdinalIgnoreCase) && v.Location.Contains("Derry", StringComparison.OrdinalIgnoreCase)) ||
                    (loc.StartsWith("County ", StringComparison.OrdinalIgnoreCase) && v.Location.Contains(loc.Replace("County ", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        if (allVacancies.Count == 0) allVacancies = await db.Vacancies.AsNoTracking().ToListAsync(ct);
        if (allVacancies.Count == 0) throw new InvalidOperationException("No vacancies available to match against.");

        List<Vacancy> rankedVacancies;
        if (userEmbedding.Length > 0 && allVacancies.Any(v => v.Embedding is { Length: > 0 }))
        {
            rankedVacancies = allVacancies
                .Select(v => new 
                {
                    Vacancy = v,
                    Score = v.Embedding is { Length: > 0 } jobEmb ? TensorPrimitives.CosineSimilarity(userEmbedding, jobEmb) : 0f
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Vacancy)
                .Take(MaxVacanciesInPrompt)
                .ToList();
        }
        else
        {
            rankedVacancies = allVacancies.OrderByDescending(v => v.PostedDate ?? DateTime.MinValue).Take(MaxVacanciesInPrompt).ToList();
        }

        var validIds = rankedVacancies.Select(v => v.Id).ToHashSet();

        const string systemPrompt = """
            You are a careers advisor for the Northern Ireland healthcare job market.
            Respond with ONLY a single JSON object — no markdown, no code fences, no free text — matching exactly this schema:
            {
              "summary": string,
              "strengths": string[],
              "skillsGaps": string[],
              "matches": [
                {
                  "vacancyId": number,
                  "matchScore": number,
                  "reason": string
                }
              ]
            }
            The "matches" array must contain exactly 5 entries whenever the supplied vacancy pool contains at least 5 — never return fewer than 5 unless the pool itself has fewer than 5 vacancies, in which case return all of them.
            """;

        var sb = new StringBuilder();
        sb.AppendLine("CANDIDATE PERSONAL PROFILE");
        sb.AppendLine($"Bio: {personalBio}");
        sb.AppendLine($"Skills: {skills}");
        sb.AppendLine($"Certifications/Qualifications: {certifications}");
        sb.AppendLine($"Years of Experience: {yearsOfExperience}");
        sb.AppendLine($"Minimum Hourly Salary: £{minHourlySalary}");
        sb.AppendLine($"Preferred Locations: {string.Join(", ", preferredLocations)}");
        sb.AppendLine();
        sb.AppendLine("RESUME TEXT");
        sb.AppendLine(resumeText);
        sb.AppendLine();
        sb.AppendLine("SEMANTICALLY MATCHED VACANCIES");
        foreach (var v in rankedVacancies)
        {
            var salary = v.SalaryMin is not null || v.SalaryMax is not null ? $"£{v.SalaryMin:N0}-£{v.SalaryMax:N0}" : "salary not stated";
            sb.AppendLine($"id={v.Id} | {v.Title} | {v.Employer} | {v.Location} | {salary} | {v.Description}");
        }

        var result = await runner.RunAsync(systemPrompt, sb.ToString(), json =>
        {
            var cleaned = AiJsonRunner.StripCodeFence(json);
            var parsed = JsonSerializer.Deserialize<ProfileAnalysisResult>(cleaned, JsonOptions)
                ?? throw new FormatException("AI returned an empty JSON document");

            if (string.IsNullOrWhiteSpace(parsed.Summary)) throw new FormatException("AI response is missing the summary field");

            var invalid = parsed.Matches.Where(m => !validIds.Contains(m.VacancyId)).ToList();
            if (invalid.Count > 0) throw new FormatException($"AI referenced unknown vacancy ids.");

            foreach (var match in parsed.Matches) match.MatchScore = Math.Clamp(match.MatchScore, 0, 100);

            parsed.Matches = parsed.Matches.GroupBy(m => m.VacancyId).Select(g => g.First()).OrderByDescending(m => m.MatchScore).Take(5).ToList();
            return parsed;
        }, ct);

        var byId = rankedVacancies.ToDictionary(v => v.Id);
        foreach (var match in result.Matches)
        {
            if (byId.TryGetValue(match.VacancyId, out var vac)) match.Vacancy = vac;
        }

        return result;
    }

    private async Task<InterviewPrepResponse> GenerateInterviewPrepAsync(string resumeText, Vacancy vacancy, CancellationToken ct)
    {
        const string systemPrompt = """
            You are an interview coach. Respond with ONLY a single JSON object — no markdown, no code fences, no free text — matching exactly this schema:
            {
              "questions": [
                {
                  "question": string,
                  "suggestedAnswerAngle": string,
                  "category": string
                }
              ]
            }
            The "category" field must be exactly one of "Technical", "Behavioural", "Situational", "Clinical" — spelled and capitalised exactly as shown, British spelling ("Behavioural", not "Behavioral").
            """;

        var userPrompt = $"""
            CANDIDATE RESUME
            {resumeText}

            VACANCY
            {vacancy.Title} | {vacancy.Employer} | {vacancy.Location}
            {vacancy.Description}
            """;

        var result = await runner.RunAsync(systemPrompt, userPrompt, json =>
        {
            var cleaned = AiJsonRunner.StripCodeFence(json);
            var parsed = JsonSerializer.Deserialize<InterviewPrepResponse>(cleaned, JsonOptions)
                ?? throw new FormatException("AI returned an empty JSON document");

            if (parsed.Questions.Count == 0) throw new FormatException("AI response contained no questions");
            return parsed;
        }, ct);

        result.VacancyId = vacancy.Id;
        return result;
    }

    private static string SerializeRecommendationsForStorage(ProfileAnalysisResult result)
    {
        var forStorage = new ProfileAnalysisResult
        {
            Summary = result.Summary,
            Strengths = result.Strengths,
            SkillsGaps = result.SkillsGaps,
            Matches = result.Matches.Select(m => new MatchedVacancy
            {
                VacancyId = m.VacancyId,
                MatchScore = m.MatchScore,
                Reason = m.Reason,
            }).ToList(),
        };

        return JsonSerializer.Serialize(forStorage, JsonOptions);
    }

    private static string ExtractPreview(string resumeSummaryJson)
    {
        try
        {
            var summary = JsonSerializer.Deserialize<ResumeSummaryResponse>(resumeSummaryJson, JsonOptions);
            var text = summary?.Summary;
            if (string.IsNullOrWhiteSpace(text)) return "Session";
            return text.Length > 100 ? text[..100] + "…" : text;
        }
        catch (JsonException)
        {
            return "Session";
        }
    }

    // Re-evaluate session matches with updated backend context
    public async Task<ProfileAnalysisResult> ReEvaluateSessionAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.AiSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Session not found.");

        var recommendations = await RecommendVacanciesCustomizedAsync(
            session.ResumeSnapshot, 
            string.Empty, 
            string.Empty, 
            string.Empty, 
            0, 
            0m, 
            [], 
            ct);

        session.RecommendationsJson = SerializeRecommendationsForStorage(recommendations);
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return recommendations;
    }
}