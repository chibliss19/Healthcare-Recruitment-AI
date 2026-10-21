using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Ai;

public class OpenRouterOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>OpenRouter routes this to Gemini under the hood — no direct Gemini API key needed.</summary>
    public string Model { get; set; } = "google/gemini-3-flash-preview";

    /// <summary>Lets the model reason before producing the final JSON answer.</summary>
    public bool ReasoningEnabled { get; set; } = true;
}

/// <summary>Sole AI provider: OpenRouter's chat completions API, routed to Gemini, with JSON-mode output.</summary>
public class OpenRouterClient(HttpClient http, IOptions<OpenRouterOptions> options) : IAiJsonClient
{
    private readonly OpenRouterOptions _options = options.Value;

    public string Name => "OpenRouter";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            response_format = new { type = "json_object" },
            reasoning = new { enabled = _options.ReasoningEnabled },
            temperature = 0.2,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty response from OpenRouter");

        // The "content" field is always the model's final answer; reasoning (if any) lives
        // separately in reasoning_details and is not part of the JSON we need to parse.
        return payload.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("OpenRouter response contained no message content");
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = [];
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
