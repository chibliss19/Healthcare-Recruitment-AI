namespace HealthcareRecruitment.Api.Services.Ai;

/// <summary>An LLM provider that returns a raw JSON document for a prompt pair.</summary>
public interface IAiJsonClient
{
    string Name { get; }

    bool IsConfigured { get; }

    /// <summary>Requests strictly-JSON output; the caller validates and retries on malformed responses.</summary>
    Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
