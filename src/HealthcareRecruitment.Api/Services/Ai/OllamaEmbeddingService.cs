using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HealthcareRecruitment.Api.Services.Ai;

public class OllamaEmbeddingService(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Pull the base URL from appsettings.json (defaults to local Ollama port)
        var ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        
        var requestPayload = new OllamaEmbedRequest
        {
            Model = "nomic-embed-text",
            Prompt = text
        };

        var response = await httpClient.PostAsJsonAsync($"{ollamaUrl}/api/embeddings", requestPayload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: cancellationToken);
        
        return result?.Embedding ?? throw new InvalidOperationException("Failed to retrieve embeddings from local Ollama instance.");
    }
}

public class OllamaEmbedRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "nomic-embed-text";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbedResponse
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];
}