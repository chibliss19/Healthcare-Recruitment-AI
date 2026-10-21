namespace HealthcareRecruitment.Api.Services.Ai;

/// <summary>
/// Shared "call the model, demand strict JSON, reject and retry on malformed output" loop,
/// used by all three AI Career Assistant calls (resume summary, recommendations, interview
/// prep) so none of them has to reimplement the guardrail.
/// </summary>
public class AiJsonRunner(IEnumerable<IAiJsonClient> clients, ILogger<AiJsonRunner> logger)
{
    private const int MaxAttemptsPerProvider = 2;

    public bool HasConfiguredProvider => clients.Any(c => c.IsConfigured);

    /// <param name="parseAndValidate">Throws on malformed/invalid output to trigger a retry.</param>
    public async Task<T> RunAsync<T>(
        string systemPrompt,
        string userPrompt,
        Func<string, T> parseAndValidate,
        CancellationToken ct)
    {
        Exception? lastError = null;
        foreach (var client in clients.Where(c => c.IsConfigured))
        {
            for (var attempt = 1; attempt <= MaxAttemptsPerProvider; attempt++)
            {
                try
                {
                    var json = await client.GenerateJsonAsync(systemPrompt, userPrompt, ct);
                    var result = parseAndValidate(json);
                    logger.LogInformation("AI JSON call served by {Provider} (attempt {Attempt})", client.Name, attempt);
                    return result;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    logger.LogWarning(ex, "{Provider} attempt {Attempt} rejected: {Reason}", client.Name, attempt, ex.Message);
                }
            }
        }

        throw new InvalidOperationException("The AI service did not return a valid response.", lastError);
    }

    /// <summary>Strips the code-fence wrapping some models add despite instructions not to.</summary>
    public static string StripCodeFence(string json)
    {
        var cleaned = json.Trim();
        if (!cleaned.StartsWith("```"))
        {
            return cleaned;
        }

        cleaned = cleaned.Trim('`', '\n', '\r');
        return cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase) ? cleaned[4..] : cleaned;
    }
}
