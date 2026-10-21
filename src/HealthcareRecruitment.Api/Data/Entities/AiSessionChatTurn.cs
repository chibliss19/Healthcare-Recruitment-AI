namespace HealthcareRecruitment.Api.Data.Entities;

/// <summary>One message in an <see cref="AiSession"/>'s chat — session-wide, not scoped to a
/// single vacancy, so the user can ask about any job surfaced in that session.</summary>
public class AiSessionChatTurn
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public AiSession? Session { get; set; }

    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
